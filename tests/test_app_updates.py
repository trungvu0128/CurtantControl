import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
from unittest.mock import patch
from types import SimpleNamespace

spec = importlib.util.spec_from_file_location('apply_update', Path(__file__).resolve().parents[1] / 'packaging/raspberry-pi/apply-update.py')
updates = importlib.util.module_from_spec(spec)
spec.loader.exec_module(updates)


def bundle(job, extra=None, tamper=False, contents=None):
    version = '0.11.0'
    top = 'CurtantControl-Final-v' + version + '-pi-buster-arm'
    files = {'VERSION.txt': version.encode(), 'setup.py': b'pass', 'rollback.py': b'pass',
             'keycheck/install.sh': b'pass', 'payloads/buster-arm/CurtantControl': b'exe'}
    files.update(contents or {})
    for directory in ('keycheck', 'payloads/buster-arm'):
        manifest = {k[len(directory)+1:]: hashlib.sha256(v).hexdigest() for k,v in files.items() if k.startswith(directory+'/')}
        files[directory+'/SHA256SUMS.json'] = json.dumps(manifest).encode()
    files['SHA256SUMS.json'] = json.dumps({k:hashlib.sha256(v).hexdigest() for k,v in files.items() if not k.endswith('SHA256SUMS.json')}).encode()
    if tamper:
        files['setup.py'] = b'changed'
    archive = job / (top + '.tar.gz')
    with tarfile.open(archive, 'w:gz') as tar:
        for name,data in files.items():
            member=tarfile.TarInfo(top+'/'+name); member.size=len(data)
            tar.addfile(member,io.BytesIO(data))
        if extra is not None:
            tar.addfile(extra)
    Path(str(archive)+'.sha256').write_text(updates.file_hash(archive)+'  '+archive.name+'\n')
    return archive


class UpdateWorkerTests(unittest.TestCase):
    def test_manual_cleanup_hook_does_not_hide_setup_changes(self):
        path = Path(__file__).resolve().parents[1] / 'scripts/build-small-update.py'
        builder_spec = importlib.util.spec_from_file_location('build_small', path)
        builder = importlib.util.module_from_spec(builder_spec)
        builder_spec.loader.exec_module(builder)
        with tempfile.TemporaryDirectory() as temp:
            old, new = Path(temp) / 'old', Path(temp) / 'new'
            for directory in (old, new):
                directory.mkdir()
            (old / 'install.sh').write_bytes(b'#!/bin/sh\nset -eu\nexec python3 setup.py "$@"\n')
            script = b'#!/bin/sh\nset -eu\npython3 setup.py "$@"\n' + builder.MANUAL_CLEANUP + b'echo cleanup\n'
            (new / 'install.sh').write_bytes(script)
            self.assertEqual(builder.files_for_patch(old, new), ({}, {}))
            (new / 'install.sh').write_bytes(script.replace(b'python3 setup.py', b'python3 setup.py --skip-packages'))
            with self.assertRaisesRegex(ValueError, 'Full update required'):
                builder.files_for_patch(old, new)

    def test_full_cleanup_keeps_original_snapshot_and_unrelated_files(self):
        with tempfile.TemporaryDirectory() as temp:
            home = Path(temp)
            archive = bundle(home)
            (home / 'job').mkdir()
            app_bundle = updates.extract_verified(archive, '0.11.0', home / 'job')
            archive = archive.rename(app_bundle.with_name(archive.name))
            checksum = Path(str(archive) + '.sha256')
            checksum.write_text(updates.file_hash(archive) + '  ' + archive.name + '\n')
            snapshot = home / 'CurtantControl-Rollback-20261121-100000-000000.tar.gz'
            snapshot.write_bytes(b'backup data')
            (app_bundle / 'rollback.sh').write_text('rollback wrapper')
            (app_bundle / '.rollback-target').write_text(str(snapshot))
            (app_bundle / 'personal.txt').write_text('keep me')
            (app_bundle / 'setup.py').write_text('locally edited')
            with patch.object(Path, 'home', return_value=home):
                updates.clean_manual_install(app_bundle)
            recovery = home / '.local/share/CurtantControl/recovery/CurtantControl-Rollback-20261121-100000-000000'
            self.assertEqual((recovery / '.rollback-target').read_text(), str(snapshot))
            self.assertTrue((recovery / 'rollback.py').exists())
            self.assertTrue(snapshot.exists())
            self.assertFalse(archive.exists())
            self.assertFalse(checksum.exists())
            self.assertFalse((app_bundle / 'keycheck/install.sh').exists())
            self.assertEqual((app_bundle / 'setup.py').read_text(), 'locally edited')
            self.assertTrue((app_bundle / 'personal.txt').exists())

    def test_cache_cleanup_requires_completed_jobs_and_keeps_recovery(self):
        with tempfile.TemporaryDirectory() as temp:
            home = Path(temp)
            cache = home / '.cache/CurtantControl/updates'
            for name, phase in (('a' * 32, 'complete'), ('b' * 32, 'failed'), ('c' * 32, 'installing'), ('personal', 'complete')):
                job = cache / name
                job.mkdir(parents=True)
                status = job / 'status.json'
                status.write_text(json.dumps({'phase': phase, 'version': '0.11.0'}))
                os.utime(status, (1, 1))
            with patch.object(Path, 'home', return_value=home):
                full = updates.extract_verified(bundle(cache / ('a' * 32)), '0.11.0', cache / ('a' * 32))
                snapshot = home / 'CurtantControl-Rollback-20261121-100000-000000.tar.gz'
                snapshot.write_bytes(b'snapshot')
                (full / '.rollback-target').write_text(str(snapshot))
                (full / 'rollback.sh').write_text('rollback')
                updates.clean_completed_updates()
            self.assertEqual({p.name for p in cache.iterdir()}, {'b' * 32, 'c' * 32, 'personal'})
            self.assertTrue(snapshot.exists())
            self.assertEqual(list(home.glob('.local/share/CurtantControl/recovery/*/.rollback-target'))[0].read_text(), str(snapshot))

    def test_manual_small_cleanup_only_removes_matching_extracted_files(self):
        from zipfile import ZipFile

        with tempfile.TemporaryDirectory() as temp:
            folder = Path(temp)
            archive = folder / 'CurtantControl-Update-v0.11.2-to-v0.11.3-pi-buster-arm.zip'
            with ZipFile(archive, 'w') as package:
                package.writestr('apply-update.py', 'helper')
                package.writestr('patch.json', '{}')
                package.extractall(folder / 'update-0.11.3')
            checksum = Path(str(archive) + '.sha256')
            checksum.write_text('checksum')
            unrelated = folder / 'update-0.11.3/notes.txt'
            unrelated.write_text('keep')
            updates.finish_manual_small(archive)
            self.assertFalse(archive.exists())
            self.assertFalse(checksum.exists())
            self.assertEqual(list(unrelated.parent.iterdir()), [unrelated])

    def test_small_update_touches_only_changed_app_files_and_restores_on_write_failure(self):
        from zipfile import ZipFile

        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            app = root / 'app'
            app.mkdir()
            changes = {'CurtantControl.dll': b'new app', 'VERSION.txt': b'0.11.1-buster\n'}
            original = {'CurtantControl': b'exe', 'libcoreclr.so': b'runtime',
                        'CurtantControl.dll': b'old app', 'VERSION.txt': b'0.11.0-buster\n'}
            for name, content in original.items():
                (app / name).write_bytes(content)
            (app / 'photobooth.json').write_bytes(b'local config')
            archive = root / 'CurtantControl-Update-v0.11.0-to-v0.11.1-pi-buster-arm.zip'
            manifest = {'from': '0.11.0', 'to': '0.11.1',
                        'before': {name: updates.file_hash(app / name) for name in original},
                        'after': {name: hashlib.sha256(content).hexdigest() for name, content in changes.items()}}
            with ZipFile(archive, 'w') as package:
                package.writestr('patch.json', json.dumps(manifest))
                for name, content in changes.items():
                    package.writestr(name, content)
            Path(str(archive) + '.sha256').write_text(updates.file_hash(archive) + '  ' + archive.name + '\n')
            (app / 'libcoreclr.so').write_bytes(b'different runtime')
            with self.assertRaises(updates.IncompatibleUpdate):
                updates.prepare_small(archive, '0.11.1', app)
            (app / 'libcoreclr.so').write_bytes(original['libcoreclr.so'])
            prepared = updates.prepare_small(archive, '0.11.1', app)
            replace = updates.os.replace

            def fail_version(source, destination):
                if Path(source).parent.name == 'staged' and Path(destination).name == 'VERSION.txt':
                    raise PermissionError('Failed to replace version')
                replace(source, destination)

            with patch.object(updates, 'running_executables', return_value=[]):
                with patch.object(updates.os, 'replace', side_effect=fail_version):
                    with self.assertRaises(PermissionError):
                        updates.install_small(app, prepared)
                self.assertEqual((app / 'CurtantControl.dll').read_bytes(), original['CurtantControl.dll'])
                backup = updates.install_small(app, prepared)
            self.assertEqual({p.name for p in backup.iterdir()}, set(changes))
            for name, content in dict(original, **changes).items():
                self.assertEqual((app / name).read_bytes(), content)
            self.assertEqual((app / 'photobooth.json').read_bytes(), b'local config')

    def test_device_and_missing_library_checks_fail_before_handoff(self):
        def read(path,*args,**kwargs):
            return 'Raspberry Pi 4 Model B' if path.name=='model' else 'ID=raspbian\nVERSION_ID="10"\n'
        with patch.object(updates.subprocess,'check_output',return_value='armhf\n'), patch.object(Path,'read_text',read), patch.object(updates.subprocess,'run',return_value=SimpleNamespace(returncode=0,stdout='ok')) as run:
            updates.verify_device(Path('/bundle'))
            self.assertEqual(run.call_count,4)
        with patch.object(updates.subprocess,'check_output',return_value='arm64\n'):
            with self.assertRaises(ValueError): updates.verify_device(Path('/bundle'))
        with patch.object(updates.subprocess,'check_output',return_value='armhf\n'), patch.object(Path,'read_text',read), patch.object(updates.subprocess,'run',return_value=SimpleNamespace(returncode=0,stdout='lib missing => not found')):
            with self.assertRaisesRegex(ValueError,'system library'): updates.verify_device(Path('/bundle'))

    def test_worker_signals_ready_then_waits_for_parent_before_install_and_restart(self):
        class Restart(Exception): pass
        with tempfile.TemporaryDirectory() as temp:
            home=Path(temp); job=home/'.cache/CurtantControl/updates/job'; job.mkdir(parents=True)
            archive=bundle(job); status=job/'status.json'; base=job/'verified'; base.mkdir()
            app=home/'.local/share/CurtantControl/app/CurtantControl'; app.parent.mkdir(parents=True); app.write_bytes(b'app')
            events=[]
            def wait(pid,**kwargs):
                self.assertEqual(json.loads(status.read_text())['phase'],'ready')
                events.append('wait')
            with patch.object(Path,'home',return_value=home), patch.object(updates.sys,'argv',['apply-update.py',str(archive),'0.11.0','100',str(status)]), patch.object(updates.os,'geteuid',return_value=1000,create=True), patch.object(updates.subprocess,'check_output',return_value='pi'), patch.object(updates.subprocess,'run',return_value=SimpleNamespace(returncode=0)), patch.object(updates.subprocess,'Popen'), patch.object(updates,'extract_verified',return_value=base), patch.object(updates,'verify_device',side_effect=lambda b:events.append('verify')), patch.object(updates,'wait_parent',side_effect=wait), patch.object(updates,'install',side_effect=lambda b,e:events.append('install')), patch.object(updates.os,'chdir'), patch.object(updates.os,'execv',side_effect=Restart) as restart:
                with self.assertRaises(Restart): updates.main()
                self.assertEqual(events,['verify','wait','install'])
                self.assertEqual(restart.call_args.args[0],str(app))

    def test_full_update_reports_sudo_denial_before_closing_app(self):
        with tempfile.TemporaryDirectory() as temp:
            home = Path(temp)
            job = home / '.cache/CurtantControl/updates/job'
            job.mkdir(parents=True)
            archive = bundle(job)
            status = job / 'status.json'
            argv = ['apply-update.py', str(archive), '0.11.0', '100', str(status)]
            denial = SimpleNamespace(returncode=1, stderr='sudo: a password is required\n')
            with patch.object(Path, 'home', return_value=home), \
                 patch.object(updates.sys, 'argv', argv), \
                 patch.object(updates.os, 'geteuid', return_value=1000, create=True), \
                 patch.object(updates.subprocess, 'check_output', return_value='pi'), \
                 patch.object(updates.subprocess, 'run', return_value=denial) as run, \
                 patch.object(updates, 'verify_device'), \
                 patch.object(updates, 'extract_verified') as extract, \
                 patch.object(updates, 'wait_parent') as wait, \
                 patch.object(updates, 'install') as install:
                self.assertEqual(updates.main(), 1)
            self.assertEqual(run.call_args.args[0], ['/usr/bin/sudo', '-n', '-v'])
            extract.assert_called_once()
            wait.assert_not_called()
            install.assert_not_called()
            result = json.loads(status.read_text(encoding='utf-8'))
            self.assertEqual(result['phase'], 'failed')
            self.assertIn('sudo: a password is required', result['message'])
            self.assertIn('run sh install.sh', result['message'])
            self.assertIn(str(archive), result['message'])

    def test_stamped_full_update_runs_as_desktop_user_and_preserves_settings(self):
        import runpy
        import shutil

        repo = Path(__file__).resolve().parents[1]
        stamp = runpy.run_path(str(repo / 'scripts/stamp-update-worker.py'))['stamp']
        with tempfile.TemporaryDirectory() as temp:
            home = Path(temp).resolve()
            job = home / '.cache/CurtantControl/updates' / ('b' * 32)
            job.mkdir(parents=True)
            package = home / 'package'
            payload = package / 'payloads/buster-arm'
            payload.mkdir(parents=True)
            worker = payload / 'apply-update.py'
            shutil.copyfile(updates.__file__, worker)
            contents = {}
            for name in updates.SYSTEM_FILES:
                source = repo / ('packaging/raspberry-pi' if name.startswith('keycheck/') else 'packaging/bundle') / name
                target = package / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(source.read_bytes())
                contents[name] = target.read_bytes()
            stamp(package)
            revision = runpy.run_path(str(worker))['SYSTEM_REVISION']
            self.assertEqual(revision, updates.system_revision(package))
            for name in ('start.sh', 'diagnose.sh', 'libcoreclr.so', 'colors.json'):
                contents['payloads/buster-arm/' + name] = b'{}'
            archive = bundle(job, contents=contents)
            status = job / 'status.json'
            override = home / 'override.conf'
            override.write_bytes(contents['keycheck/override.conf'])
            (home / 'r1').mkdir()
            for name in ('key_check_new.py', '123click-logo.png'):
                (home / 'r1' / name).write_bytes(contents['keycheck/' + name])
            app = home / '.local/share/CurtantControl/app'
            app.mkdir(parents=True)
            (app / 'CurtantControl').write_bytes(b'original app')
            (app / 'colors.json').write_bytes(b'{"custom":true}')
            calls = []

            def systemctl(command, **kwargs):
                self.assertEqual(command[0], 'systemctl')
                self.assertIn(command[1], ('is-active', 'is-enabled'))
                calls.append(command[1])
                return SimpleNamespace(returncode=0)

            def wait(pid, **kwargs):
                self.assertEqual(json.loads(status.read_text())['phase'], 'ready')
                self.assertEqual((app / 'CurtantControl').read_bytes(), b'original app')

            with patch.object(Path, 'home', return_value=home), \
                 patch.object(updates, 'SYSTEM_REVISION', revision), \
                 patch.object(updates, 'KEYCHECK_OVERRIDE', override), \
                 patch.object(updates.os, 'geteuid', return_value=1000, create=True), \
                 patch.object(updates.subprocess, 'check_output', return_value='pi'), \
                 patch.object(updates.subprocess, 'run', side_effect=systemctl), \
                 patch.object(updates.subprocess, 'Popen'), patch.object(updates, 'verify_device'), \
                 patch.object(updates, 'lock_app_startup'), patch.object(updates, 'running_executables', return_value=[]), \
                 patch.object(updates, 'wait_parent', side_effect=wait), patch.object(updates, 'restart_app') as restart:
                updates.main([str(archive), '0.11.0', '100', str(status)])
                restart.assert_called_once()
                result = json.loads(status.read_text())
                self.assertEqual(result['phase'], 'complete')
                self.assertEqual(calls, ['is-active', 'is-enabled'])
                backup = Path(result['appBackup'])
                self.assertEqual((backup / 'CurtantControl').read_bytes(), b'original app')
                self.assertEqual((app / 'CurtantControl').read_bytes(), b'exe')
                self.assertEqual((app / 'colors.json').read_bytes(), b'{"custom":true}')
                os.utime(status, (1, 1))
                updates.clean_completed_updates()
                self.assertFalse(job.exists())
                self.assertTrue(backup.is_dir())
                with patch.object(updates.subprocess, 'run', return_value=SimpleNamespace(returncode=1)):
                    self.assertFalse(updates.can_update_app_only(package))
                override.write_bytes(b'custom')
                self.assertFalse(updates.can_update_app_only(package))
                override.write_bytes(contents['keycheck/override.conf'])
                (package / 'setup.py').write_bytes(b'new system setup')
                self.assertFalse(updates.can_update_app_only(package))

    def test_full_update_distinguishes_sudo_signal_from_password_failure(self):
        interrupted = SimpleNamespace(returncode=-1, stderr='sudo was interrupted\n')
        with patch.object(updates.subprocess, 'run', return_value=interrupted):
            with self.assertRaisesRegex(ValueError, 'Signal|signal') as error:
                updates.verify_full_update_sudo(Path('/tmp/downloaded-full.tar.gz'))
        self.assertIn('sudo was interrupted', str(error.exception))
        self.assertIn('update service log', str(error.exception))

    def test_verified_archive_and_version(self):
        with tempfile.TemporaryDirectory() as temp:
            job=Path(temp); archive=bundle(job)
            base=updates.extract_verified(archive,'0.11.0',job)
            self.assertEqual((base/'setup.py').read_bytes(),b'pass')

    def test_traversal_links_and_corrupt_manifest_are_rejected_before_install(self):
        for case in ('traversal','link','manifest'):
            with tempfile.TemporaryDirectory() as temp:
                job=Path(temp); extra=None
                if case=='traversal': extra=tarfile.TarInfo('../outside')
                if case=='link':
                    extra=tarfile.TarInfo('CurtantControl-Final-v0.11.0-pi-buster-arm/link')
                    extra.type=tarfile.SYMTYPE; extra.linkname='/etc'
                archive=bundle(job,extra,case=='manifest')
                with self.assertRaises(ValueError): updates.extract_verified(archive,'0.11.0',job)
                self.assertFalse((job.parent/'outside').exists())

    def test_wrong_outer_checksum_and_low_disk_are_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            job=Path(temp); archive=bundle(job)
            Path(str(archive)+'.sha256').write_text('0'*64+'  '+archive.name+'\n')
            with self.assertRaises(ValueError): updates.extract_verified(archive,'0.11.0',job)
            self.assertFalse((job/'extracted').exists())
        with tempfile.TemporaryDirectory() as temp:
            job=Path(temp); archive=bundle(job)
            with patch.object(updates.shutil,'disk_usage',return_value=type('Disk',(),{'free':0})()):
                with self.assertRaisesRegex(ValueError,'free space'): updates.extract_verified(archive,'0.11.0',job)

    def test_parent_timeout_never_starts_install(self):
        with patch.object(Path,'exists',return_value=True):
            with self.assertRaisesRegex(ValueError,'No installation'): updates.wait_parent(100,seconds=0)
        with tempfile.TemporaryDirectory() as temp:
            marker=Path(temp)/'cancelled'; marker.touch()
            with self.assertRaisesRegex(ValueError,'cancelled'):
                updates.wait_parent(999999999,cancelled=marker)

    def test_failed_install_restores_snapshot_and_restarts_keycheck(self):
        with tempfile.TemporaryDirectory() as temp:
            base=Path(temp)/'bundle'; base.mkdir()
            (base/'.rollback-target').write_text('/home/pi/backup.tar.gz')
            failure=subprocess.CalledProcessError(1,['setup.py'])
            with patch.object(updates.subprocess,'run',side_effect=[failure,None,None]) as run:
                with self.assertRaises(subprocess.CalledProcessError): updates.install(base,{'PATH':'/usr/bin'})
                self.assertIn('restore',run.call_args_list[1].args[0])
                self.assertEqual(run.call_args_list[2].args[0],['/usr/bin/sudo','-n','systemctl','restart','keycheck.service'])
                self.assertIn('sudo -n', (base.parent/'bin/sudo').read_text())

    def test_status_is_atomic_and_persists_for_diagnostics(self):
        with tempfile.TemporaryDirectory() as temp:
            home=Path(temp)
            with patch.object(Path,'home',return_value=home):
                status=home/'job/status.json'; updates.report(status,'ready','ok','0.11.0')
                self.assertEqual(json.loads(status.read_text())['phase'],'ready')
                self.assertEqual((home/'.local/share/CurtantControl/update-status.json').read_bytes(),status.read_bytes())
                self.assertFalse(list(home.rglob('*.tmp')))


if __name__=='__main__': unittest.main()
