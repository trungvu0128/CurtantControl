import hashlib
import importlib.util
import io
import json
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


def bundle(job, extra=None, tamper=False):
    version = '0.11.0'
    top = 'CurtantControl-Final-v' + version + '-pi-buster-arm'
    files = {'VERSION.txt': version.encode(), 'setup.py': b'pass', 'rollback.py': b'pass',
             'keycheck/install.sh': b'pass', 'payloads/buster-arm/CurtantControl': b'exe'}
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
            with patch.object(Path,'home',return_value=home), patch.object(updates.sys,'argv',['apply-update.py',str(archive),'0.11.0','100',str(status)]), patch.object(updates.os,'geteuid',return_value=1000,create=True), patch.object(updates.subprocess,'check_output',return_value='pi'), patch.object(updates.subprocess,'run'), patch.object(updates.subprocess,'Popen'), patch.object(updates,'extract_verified',return_value=base), patch.object(updates,'verify_device',side_effect=lambda b:events.append('verify')), patch.object(updates,'wait_parent',side_effect=wait), patch.object(updates,'install',side_effect=lambda b,e:events.append('install')), patch.object(updates.os,'chdir'), patch.object(updates.os,'execv',side_effect=Restart) as restart:
                with self.assertRaises(Restart): updates.main()
                self.assertEqual(events,['verify','wait','install'])
                self.assertEqual(restart.call_args.args[0],str(app))

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
