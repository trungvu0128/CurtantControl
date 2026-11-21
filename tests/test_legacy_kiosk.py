import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from types import SimpleNamespace

spec = importlib.util.spec_from_file_location('legacy_kiosk', Path(__file__).resolve().parents[1] / 'packaging/bundle/legacy_kiosk.py')
kiosk = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kiosk)


class KioskTests(unittest.TestCase):
    def test_reinstall_disables_rollback_backend_service(self):
        calls=[]
        def run(args, **kwargs):
            calls.append(args)
            return SimpleNamespace(returncode=0, stdout='loaded\n', stderr='')
        with patch.object(kiosk.sys,'platform','linux'), patch.object(kiosk.sys,'argv',['legacy_kiosk.py','--system-startup']), patch.object(kiosk.os,'geteuid',return_value=0,create=True), patch.object(kiosk.subprocess,'run',side_effect=run), patch.object(Path,'is_file',return_value=False):
            kiosk.main()
        self.assertIn(['systemctl','disable','--now','camrazr-legacy-backend.service'],calls)

    def test_known_script_and_explicit_kiosk_only(self):
        self.assertTrue(kiosk.legacy_command("/home/pi/r1/cr_192_168_137_3.sh # old"))
        self.assertTrue(kiosk.legacy_command("sh /home/pi/r1/camrazr-client.sh"))
        self.assertTrue(kiosk.legacy_command("chromium-browser --kiosk http://localhost:5000"))
        self.assertFalse(kiosk.legacy_command("chromium-browser https://example.com"))
        self.assertFalse(kiosk.legacy_command("python3 /home/pi/r1/key_check1.py"))

    def test_autostart_backup_and_idempotence(self):
        with tempfile.TemporaryDirectory() as temp:
            home = Path(temp)
            path = home / '.config/autostart/camrazr-kiosk.desktop'
            path.parent.mkdir(parents=True)
            original = '[Desktop Entry]\nExec=/home/pi/r1/cr_192_168_137_3.sh\nHidden=false\n[Desktop Action Other]\nExec=keep\n'
            path.write_text(original)
            unrelated = path.with_name('browser.desktop')
            unrelated.write_text('[Desktop Entry]\nExec=chromium https://example.com\n')
            kiosk.disable_user_startup(home)
            self.assertIn('Hidden=true', path.read_text())
            self.assertIn('[Desktop Action Other]\nExec=keep', path.read_text())
            backups = list(path.parent.glob('*.before-curtant-*'))
            self.assertEqual(len(backups), 1)
            self.assertEqual(backups[0].read_text(), original)
            kiosk.disable_user_startup(home)
            self.assertEqual(len(list(path.parent.glob('*.before-curtant-*'))), 1)
            self.assertNotIn('Hidden=true', unrelated.read_text())

    def test_rc_local_preserves_other_startup_and_comments(self):
        text = '#!/bin/sh -e\n# chromium --kiosk old\nsu pi -c "/home/pi/r1/cr_192_168_137_3.sh"\nsu pi -c "python3 /home/pi/r1/model_1/app_model_1.py"\nexit 0\n'
        revised = kiosk.rewrite_lines(text)
        self.assertIn('# CurtantControl disabled old kiosk: su pi', revised)
        self.assertIn('# CurtantControl disabled old kiosk: su pi -c "python3', revised)
        self.assertIsNone(kiosk.rewrite_lines(revised))

    def test_only_main_kiosk_process_matches(self):
        self.assertTrue(kiosk.kiosk_process(['/usr/lib/chromium/chromium', '--kiosk', 'http://localhost:5000']))
        self.assertFalse(kiosk.kiosk_process(['chromium', '--type=renderer', '--kiosk']))
        self.assertFalse(kiosk.kiosk_process(['chromium', 'https://example.com']))


if __name__ == '__main__':
    unittest.main()
