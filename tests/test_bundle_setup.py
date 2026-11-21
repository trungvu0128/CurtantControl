"""Host-only checks: do not run apt, sudo, GPIO access or the Linux executable."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch, Mock

spec = importlib.util.spec_from_file_location('bundle_setup', Path(__file__).resolve().parents[1] / 'packaging/bundle/setup.py')
setup = importlib.util.module_from_spec(spec)
spec.loader.exec_module(setup)


class BundleSetupChecks(unittest.TestCase):
    def test_buster_mirror_repair_is_scoped_and_idempotent(self):
        text = ('deb http://mirrors.aliyun.com/raspbian/raspbian buster main contrib non-free rpi\n'
                'deb-src [signed-by=/keys/raspbian.gpg] https://mirrors.aliyun.com/raspbian/ buster main\n'
                '# deb http://mirrors.aliyun.com/raspbian/raspbian buster main\n'
                'deb http://mirrors.aliyun.com/raspbian/raspbian bookworm main\n'
                'deb http://archive.raspberrypi.org/debian buster main\n')
        result = setup.repair_buster_source_text(text)
        self.assertEqual(result.count('https://legacy.raspbian.org/raspbian/'), 2)
        self.assertIn('[signed-by=/keys/raspbian.gpg]', result)
        self.assertIn('# deb http://mirrors.aliyun.com', result)
        self.assertIn('raspbian/raspbian bookworm main', result)
        self.assertIn('archive.raspberrypi.org', result)
        self.assertEqual(setup.repair_buster_source_text(result), result)
        with tempfile.TemporaryDirectory() as folder:
            apt = Path(folder)
            (apt / 'sources.list.d').mkdir()
            path = apt / 'sources.list.d/raspbian.list'
            path.write_text(text)
            self.assertEqual(setup.repair_buster_sources({'ID':'debian', 'VERSION_ID':'12'}, apt), [])
            self.assertEqual(path.read_text(), text)
            self.assertEqual(setup.repair_buster_sources({'ID':'raspbian', 'VERSION_ID':'10'}, apt), [path])
            backups = list(path.parent.glob('*.curtant-backup-*'))
            self.assertEqual(len(backups), 1)
            self.assertEqual(backups[0].read_text(), text)
            self.assertEqual(setup.repair_buster_sources({'ID':'raspbian', 'VERSION_ID':'10'}, apt), [])

    def test_select_by_userspace_and_os(self):
        for version, arch, expected in [('10', 'armhf', 'buster-arm'), ('12', 'armhf', 'modern-arm'),
                                        ('12', 'arm64', 'modern-arm64'), ('13', 'arm64', 'modern-arm64')]:
            self.assertEqual(setup.select_payload({'ID': 'raspbian', 'VERSION_ID': version}, arch), expected)
        for version, arch in [('10', 'arm64'), ('11', 'armhf'), ('13', 'amd64')]:
            with self.assertRaises(ValueError):
                setup.select_payload({'ID': 'debian', 'VERSION_ID': version}, arch)

    def test_packages_do_not_mix_os_versions(self):
        buster = setup.packages({'VERSION_ID': '10'})
        self.assertIn('libicu63', buster)
        self.assertIn('libgcc1', buster)
        self.assertNotIn('libgcc-s1', buster)
        self.assertIn('libicu72', setup.packages({'VERSION_ID': '12'}))
        self.assertIn('libgpiod3', setup.packages({'VERSION_ID': '13'}))

    def test_checksum_detects_tampering_and_traversal(self):
        with tempfile.TemporaryDirectory() as folder:
            payload = Path(folder)
            (payload / 'file').write_bytes(b'good')
            manifest = payload / 'SHA256SUMS.json'
            manifest.write_text(json.dumps({'file': hashlib.sha256(b'good').hexdigest()}))
            setup.verify_payload(payload)
            (payload / 'file').write_bytes(b'bad')
            with self.assertRaises(ValueError):
                setup.verify_payload(payload)
            manifest.write_text(json.dumps({'../file': 'invalid'}))
            with self.assertRaises(ValueError):
                setup.verify_payload(payload)

    def test_fresh_install_and_update_preserve_config_and_backup(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            payload = root / 'payload'
            payload.mkdir()
            for name in ('CurtantControl', 'start.sh', 'diagnose.sh'):
                (payload / name).write_text('new')
            (payload / 'photobooth.json').write_text('{"SimulateRelays":true}')
            destination = root / 'installed' / 'app'
            self.assertIsNone(setup.stage_install(payload, destination))
            custom = b'{ "SimulateRelays": false, "ActiveLow": false }\n'
            (destination / 'photobooth.json').write_bytes(custom)
            custom_palette = b'{ "Colors": [], "Slots": [] }\n'
            (destination / 'colors.json').write_bytes(custom_palette)
            (destination / 'CurtantControl').write_text('old')
            backup = setup.stage_install(payload, destination)
            self.assertEqual((destination / 'photobooth.json').read_bytes(), custom)
            self.assertEqual((backup / 'photobooth.json').read_bytes(), custom)
            self.assertEqual((destination / 'colors.json').read_bytes(), custom_palette)
            self.assertEqual((backup / 'colors.json').read_bytes(), custom_palette)
            self.assertEqual((backup / 'CurtantControl').read_text(), 'old')
            self.assertEqual((destination / 'CurtantControl').read_text(), 'new')
            (destination / 'photobooth.json').write_text('broken')
            with self.assertRaises(ValueError):
                setup.stage_install(payload, destination)
            self.assertEqual((destination / 'photobooth.json').read_text(), 'broken')

    def test_integrated_button_dependencies_are_opt_in(self):
        base = setup.packages({'VERSION_ID':'10'})
        combined = setup.packages({'VERSION_ID':'10'}, include_keycheck=True)
        self.assertNotIn('python3-rpi.gpio', base)
        for package in ('python3-rpi.gpio','python3-tk','python3-pil','python3-pil.imagetk','wmctrl'):
            self.assertIn(package, combined)

    def test_current_camera_options_and_external_user_state_survive_update(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            payload = root / 'payload'
            payload.mkdir()
            for name in ('CurtantControl','start.sh','diagnose.sh'):
                (payload/name).write_bytes(b'new')
            (payload/'razr2-options.json').write_bytes(b'[{"Name":"Default","Color":"gray"}]')
            app = root/'installed/app'
            setup.stage_install(payload,app)
            custom = b'[{"Name":"Custom","Color":"#123456"}]'
            (app/'razr2-options.json').write_bytes(custom)
            state = app.parent/'razr.json'
            selection = app.parent/'camera-button-selection.json'
            state.write_bytes(b'{"SelectedCameraButtonId":102}')
            selection.write_bytes(b'{"Version":1,"SelectedId":102}')
            backup=setup.stage_install(payload,app)
            self.assertEqual((app/'razr2-options.json').read_bytes(),custom)
            self.assertEqual((backup/'razr2-options.json').read_bytes(),custom)
            self.assertEqual(state.read_bytes(),b'{"SelectedCameraButtonId":102}')
            self.assertEqual(selection.read_bytes(),b'{"Version":1,"SelectedId":102}')

    def test_integrated_preflight_is_read_only_and_verifies_both_payloads(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder)
            for directory in (root/'payloads/buster-arm',root/'keycheck'):
                directory.mkdir(parents=True)
                name='install.sh' if directory.name=='keycheck' else 'CurtantControl'
                (directory/name).write_bytes(b'payload')
                (directory/'SHA256SUMS.json').write_text(json.dumps({name:hashlib.sha256(b'payload').hexdigest()}))
            model=Path('/proc/device-tree/model')
            original_exists=Path.exists
            original_read=Path.read_text
            def exists(path): return True if path==model else original_exists(path)
            def read(path,*args,**kwargs): return 'Raspberry Pi 4 Model B' if path==model else original_read(path,*args,**kwargs)
            def output(args,**kwargs): return 'armhf' if args[0]=='dpkg' else 'pi'
            before={str(p.relative_to(root)):p.read_bytes() for p in root.rglob('*') if p.is_file()}
            with patch.object(setup,'__file__',str(root/'setup.py')), patch.object(setup.sys,'argv',['setup.py','--check']), patch.object(setup.sys,'platform','linux'), patch.object(setup.os,'geteuid',return_value=1000,create=True), patch.object(setup,'os_info',return_value={'ID':'raspbian','VERSION_ID':'10','PRETTY_NAME':'Buster'}), patch.object(setup.subprocess,'check_output',side_effect=output), patch.object(setup,'app_running',return_value=False), patch.object(Path,'home',return_value=root/'home'), patch.object(Path,'exists',exists), patch.object(Path,'read_text',read), patch.object(setup,'run') as run:
                setup.main()
                run.assert_not_called()
            after={str(p.relative_to(root)):p.read_bytes() for p in root.rglob('*') if p.is_file()}
            self.assertEqual(before,after)


class AutomaticRollbackTests(unittest.TestCase):
    def test_snapshot_is_verified_before_pointer_is_written_and_reused(self):
        with tempfile.TemporaryDirectory() as folder:
            base = Path(folder)
            events = []
            def invoke(args):
                events.append(args[2])
                self.assertFalse((base / '.rollback-target').exists())
                if args[2] == 'backup-install':
                    Path(args[3]).write_bytes(b'backup')
            with patch.object(Path, 'home', return_value=base), patch.object(setup, 'run', side_effect=invoke):
                archive = setup.ensure_rollback_backup(base)
            self.assertEqual(events, ['backup-install', 'check'])
            self.assertEqual((base / '.rollback-target').read_text().strip(), str(archive))
            with patch.object(setup, 'run') as run:
                self.assertEqual(setup.ensure_rollback_backup(base), archive)
                self.assertEqual([c.args[0][2] for c in run.call_args_list], ['check','stop-apps'])

    def test_failed_backup_does_not_record_restore_target(self):
        with tempfile.TemporaryDirectory() as folder:
            base = Path(folder)
            with patch.object(Path, 'home', return_value=base), patch.object(setup, 'run', side_effect=OSError('full disk')):
                with self.assertRaises(OSError):
                    setup.ensure_rollback_backup(base)
            self.assertFalse((base / '.rollback-target').exists())

    def test_missing_baseline_is_not_replaced_with_partial_install(self):
        with tempfile.TemporaryDirectory() as folder:
            base = Path(folder)
            (base / '.rollback-target').write_text(str(base / 'missing.tar.gz'))
            with patch.object(setup, 'run') as run:
                with self.assertRaises(ValueError):
                    setup.ensure_rollback_backup(base)
                run.assert_not_called()


if __name__ == '__main__':
    unittest.main()
