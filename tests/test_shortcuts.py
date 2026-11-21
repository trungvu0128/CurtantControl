import importlib.util
from pathlib import Path
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('shortcuts',Path(__file__).resolve().parents[1]/'packaging/raspberry-pi/install-shortcut.py')
shortcuts=importlib.util.module_from_spec(spec)
spec.loader.exec_module(shortcuts)


class ShortcutTests(unittest.TestCase):
    def test_repeat_install_keeps_existing_file_and_one_icon(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); desktop=root/'Desktop'; backups=root/'backups'; app=root/'CurtantControl/app'
            path=shortcuts.keep_one_shortcut(desktop,'CurtantControl.desktop',app,backups)
            before=path.stat().st_mtime_ns
            shortcuts.keep_one_shortcut(desktop,'CurtantControl.desktop',app,backups)
            self.assertEqual(path.stat().st_mtime_ns,before)
            self.assertEqual(len(list(desktop.iterdir())),1)
            self.assertFalse(backups.exists())

    def test_duplicate_icons_and_old_backup_files_move_off_desktop(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); desktop=root/'Desktop'; desktop.mkdir(); app=root/'CurtantControl/app'; backups=root/'backups'
            for name in ('CurtantControl.desktop','Copy.desktop','CurtantControl.desktop.bak-old'):
                (desktop/name).write_text(shortcuts.desktop_entry(app))
            other=desktop/'browser.desktop'; other.write_text('[Desktop Entry]\nExec=chromium https://example.com\n')
            shortcuts.keep_one_shortcut(desktop,'CurtantControl.desktop',app,backups)
            self.assertEqual(sorted(p.name for p in desktop.iterdir()),['CurtantControl.desktop','browser.desktop'])
            self.assertEqual(len(list(backups.iterdir())),2)

    def test_starting_helper_is_not_reset_on_install(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); desktop=root/'Desktop'; desktop.mkdir()
            path=desktop/'CurtantControl.desktop'
            original='[Desktop Entry]\nExec=/usr/bin/python3 /home/pi/r1/key_check_new.py --start-app\n'
            path.write_text(original)
            shortcuts.keep_one_shortcut(desktop,path.name,root/'CurtantControl/app',root/'backups')
            self.assertEqual(path.read_text(),original)

    def test_does_not_replace_unrelated_canonical_file(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); path=root/'CurtantControl.desktop'
            path.write_text('[Desktop Entry]\nExec=unrelated\n')
            with self.assertRaises(ValueError):
                shortcuts.keep_one_shortcut(root,path.name,root/'app',root/'backups')


if __name__=='__main__':
    unittest.main()
