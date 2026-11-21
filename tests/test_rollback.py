import importlib.util
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch, Mock
from types import SimpleNamespace

spec = importlib.util.spec_from_file_location('rollback', Path(__file__).resolve().parents[1] / 'packaging/raspberry-pi/rollback.py')
rollback = importlib.util.module_from_spec(spec)
spec.loader.exec_module(rollback)


class RollbackTests(unittest.TestCase):
    def test_install_stops_old_services_and_apps_before_backup(self):
        events = []
        def service(*args, **kwargs):
            events.append(args)
            return SimpleNamespace(returncode=0)
        with patch.object(rollback,'service',side_effect=service), patch.object(rollback,'stop_existing_services',side_effect=lambda: events.append('stop-services')), patch.object(rollback,'stop_current_apps',side_effect=lambda uid: events.append(('stop-apps',uid))):
            self.assertTrue(rollback.prepare_install(SimpleNamespace(pw_uid=1000)))
        self.assertEqual(events, [('daemon-reload',), ('is-enabled','--quiet','keycheck.service'), 'stop-services', ('stop-apps',1000)])

    def test_legacy_requires_original_files_and_unambiguous_launcher(self):
        names = ['home/pi/r1/key_check1.py', 'home/pi/r1/model_1/app_model_1.py',
                 'home/pi/r1/cr_192_168_137_3.sh']
        self.assertEqual(rollback.legacy_files(names), names[-1])
        with self.assertRaises(ValueError):
            rollback.legacy_files(names[1:])
        with self.assertRaises(ValueError):
            rollback.legacy_files(names[:2] + ['home/pi/r1/cr_a.sh', 'home/pi/r1/cr_b.sh'])

    def test_legacy_config_restores_old_keycheck_and_browser_not_new_app(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            auto = root / 'home/pi/.config/autostart'; auto.mkdir(parents=True)
            (auto/'new.desktop').write_text('[Desktop Entry]\nExec=/usr/bin/python3 /home/pi/r1/key_check_new.py --start-app\n')
            (auto/'old.desktop').write_text('[Desktop Entry]\nExec=/home/pi/r1/cr_192_168_137_3.sh\nHidden=true\n')
            (auto/'other.desktop').write_text('[Desktop Entry]\nExec=unrelated\n')
            rc = root/'etc/rc.local'; rc.parent.mkdir(parents=True)
            rc.write_text('#!/bin/sh\nsu pi -c "python3 /home/pi/r1/model_1/app_model_1.py"\nexit 0\n')
            drop = root/'etc/systemd/system/keycheck.service.d'; drop.mkdir(parents=True)
            (drop/'override.conf').write_text('ExecStart=key_check_new.py')
            desktop, backend = rollback.configure_legacy(root, 'home/pi/r1/cr_192_168_137_3.sh')
            self.assertIn('Exec=/bin/sh /home/pi/r1/cr_192_168_137_3.sh', desktop.read_text())
            self.assertFalse((auto/'new.desktop').exists())
            self.assertFalse((auto/'old.desktop').exists())
            self.assertTrue((auto/'other.desktop').exists())
            self.assertIn('key_check1.py', (drop.parent/'keycheck.service').read_text())
            self.assertNotIn('key_check_new.py', (drop.parent/'keycheck.service').read_text())
            self.assertFalse(drop.exists())
            self.assertIn('app_model_1.py', backend.read_text())
            self.assertIn('# CurtantControl legacy rollback manages startup: su pi', rc.read_text())

    def test_legacy_missing_originals_aborts_before_stop(self):
        with tempfile.TemporaryDirectory() as temp:
            with patch.object(rollback.subprocess, 'run') as run:
                with self.assertRaises(ValueError):
                    rollback.rollback_legacy(Path(temp), None, SimpleNamespace(pw_uid=1000, pw_gid=1000))
                run.assert_not_called()

    def test_legacy_stops_current_then_starts_old_services_and_launcher(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for name in ('key_check1.py', 'model_1/app_model_1.py', 'cr_192_168_137_3.sh'):
                p = root/'home/pi/r1'/name; p.parent.mkdir(parents=True, exist_ok=True); p.write_text('old')
            events = []
            def run(args, **kwargs):
                events.append(args)
                return SimpleNamespace(returncode=0, stdout='loaded\n', stderr='')
            with patch.object(rollback.subprocess,'run',side_effect=run), patch.object(rollback,'stop_current_apps',side_effect=lambda uid: events.append(['stop-app'])), patch.object(rollback.os,'chown',create=True), patch.object(rollback.subprocess,'Popen') as launch:
                rollback.rollback_legacy(root, None, SimpleNamespace(pw_uid=1000,pw_gid=1000))
                self.assertEqual(events[0], ['systemctl', 'daemon-reload'])
                self.assertLess(events.index(['systemctl','stop','keycheck.service']), events.index(['stop-app']))
                self.assertIn(['systemctl','start','camrazr-legacy-backend.service','keycheck.service'], events)
                self.assertEqual(launch.call_args.args[0][-2:], ['/bin/sh','/home/pi/r1/cr_192_168_137_3.sh'])

    def test_missing_legacy_service_is_skipped_but_real_stop_errors_propagate(self):
        def run(args, **kwargs):
            if args[1] == 'show':
                return SimpleNamespace(returncode=0, stdout='not-found\n' if args[-1].startswith('camrazr') else 'loaded\n', stderr='')
            return SimpleNamespace(returncode=0)
        with patch.object(rollback.subprocess,'run',side_effect=run) as execute:
            rollback.stop_existing_services()
            self.assertIn(unittest.mock.call(['systemctl','stop','keycheck.service'], timeout=45, check=True), execute.call_args_list)
            self.assertFalse(any(c.args[0] == ['systemctl','stop','camrazr-legacy-backend.service'] for c in execute.call_args_list))
        import subprocess
        def fail_stop(args, **kwargs):
            if args[1] == 'show':
                return SimpleNamespace(returncode=0, stdout='loaded\n', stderr='')
            raise subprocess.CalledProcessError(1, args)
        with patch.object(rollback.subprocess,'run',side_effect=fail_stop):
            with self.assertRaises(subprocess.CalledProcessError):
                rollback.stop_existing_services()

    def test_service_timeout_is_reported_and_bounded(self):
        import subprocess
        with patch.object(rollback.subprocess,'run',side_effect=subprocess.TimeoutExpired('systemctl',45)) as run:
            with self.assertRaisesRegex(ValueError, '45 seconds'):
                rollback.service('stop','keycheck.service',check=True)
            self.assertEqual(run.call_args.kwargs['timeout'],45)

    def test_restores_app_legacy_buttons_state_and_startup(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            root = base / 'root'
            originals = {
                'home/pi/r1/key_check1.py': b'old buttons',
                'home/pi/r1/model_1/app_model_1.py': b'old python app',
                'home/pi/.local/share/CurtantControl/app/CurtantControl': b'old binary',
                'home/pi/.local/share/CurtantControl/razr.json': b'{"selected": 2}',
                'etc/rc.local': b'old startup',
                'etc/systemd/system/keycheck.service': b'old service',
            }
            for name, content in originals.items():
                p = root / name
                p.parent.mkdir(parents=True, exist_ok=True)
                p.write_bytes(content)
            archive = rollback.save(root, base / 'backup.tar.gz', True)
            for name in originals:
                (root / name).write_bytes(b'new version')
            new_script = root / 'home/pi/r1/key_check_new.py'
            new_script.write_bytes(b'new buttons')
            new_state = root / (rollback.STATE + '/camera-button-selection.json')
            new_state.write_bytes(b'new state')
            metadata = rollback.restore(root, archive)
            self.assertTrue(metadata['keycheck_enabled'])
            for name, content in originals.items():
                self.assertEqual((root / name).read_bytes(), content)
            self.assertFalse(new_script.exists())
            self.assertFalse(new_state.exists())
            self.assertTrue(list(new_state.parent.glob('*.before-rollback-*')))

    def test_rejects_path_outside_targets_before_changes(self):
        with tempfile.TemporaryDirectory() as temp:
            archive = Path(temp) / 'bad.tar.gz'
            with tarfile.open(archive, 'w:gz') as t:
                data = json.dumps({'format': 1, 'paths': ['etc/passwd'], 'present': []}).encode()
                m = tarfile.TarInfo('rollback.json'); m.size = len(data)
                t.addfile(m, io.BytesIO(data))
            with tarfile.open(archive) as t:
                with self.assertRaises(ValueError):
                    rollback.validate(t)

    def test_restores_missing_service_without_new_override(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / 'root'; root.mkdir()
            archive = rollback.save(root, Path(temp) / 'backup.tar.gz', False)
            service = root / 'etc/systemd/system/keycheck.service.d/override.conf'
            service.parent.mkdir(parents=True)
            service.write_text('new override')
            result = rollback.restore(root, archive)
            self.assertFalse(service.exists())
            self.assertFalse(result['keycheck_enabled'])


if __name__ == '__main__':
    unittest.main()
