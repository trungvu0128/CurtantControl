import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch, Mock

SCRIPT = Path(__file__).resolve().parents[1] / "packaging/raspberry-pi/keycheck/key_check_new.py"
spec = importlib.util.spec_from_file_location("key_check_new", SCRIPT)
keycheck = importlib.util.module_from_spec(spec)
spec.loader.exec_module(keycheck)


class KeycheckTests(unittest.TestCase):
    def test_installer_uses_linux_line_endings(self):
        content = (SCRIPT.parent / "install.sh").read_bytes()
        self.assertNotIn(b"\r", content)
        self.assertTrue(content.startswith(b"#!/bin/sh\nset -eu\n"))

    def test_bounce_and_hold_only_fire_once(self):
        button = keycheck.DebouncedButton(True, 0)
        self.assertFalse(button.update(False, .01))
        self.assertFalse(button.update(True, .02))
        self.assertFalse(button.update(False, .03))
        self.assertTrue(button.update(False, .12))
        self.assertFalse(button.update(False, 5))

    def test_boot_held_button_waits_for_release(self):
        button = keycheck.DebouncedButton(False, 0)
        self.assertFalse(button.update(False, 1))
        button.update(True, 2)
        button.update(True, 2.2)
        button.update(False, 3)
        self.assertTrue(button.update(False, 3.2))

    def test_app_button_accepts_50ms_press(self):
        button = keycheck.DebouncedButton(True, 0, delay=.04)
        self.assertFalse(button.update(False, .01))
        self.assertTrue(button.update(False, .06))
        self.assertFalse(button.update(False, .20))

    def test_stopped_app_starts_once_without_popup(self):
        process = Mock(pid=123)
        process.poll.return_value = None
        with patch.object(keycheck, '_launched_app', None), patch.object(keycheck, 'app_pids', return_value=set()), patch.object(
            keycheck.Path, 'is_file', return_value=True
        ), patch.object(keycheck.os, 'access', return_value=True), patch.object(
            keycheck.subprocess, 'Popen', return_value=process
        ) as launch, patch.object(keycheck, 'close_app') as close:
            keycheck.toggle_app()
            keycheck.toggle_app()
            launch.assert_called_once()
            close.assert_not_called()

    def test_startup_window_readiness_and_failure(self):
        process = Mock()
        process.poll.return_value = None
        self.assertEqual(keycheck.startup_state(process, 2, False), 'waiting')
        self.assertEqual(keycheck.startup_state(process, 2, True), 'ready')
        self.assertEqual(keycheck.startup_state(process, 61, False), 'timeout')
        process.poll.return_value = 1
        self.assertEqual(keycheck.startup_state(process, 2, False), 'failed')

    def test_startup_indicator_blocks_close_popup_even_after_app_process_appears(self):
        process = Mock()
        process.poll.return_value = None
        with patch.object(keycheck, '_launched_app', process), patch.object(keycheck, 'app_pids', return_value={123}), patch.object(keycheck, 'close_app') as close:
            keycheck.toggle_app()
            close.assert_not_called()

    def test_window_ready_matches_process_not_other_windows(self):
        with patch.object(keycheck.subprocess, 'run', return_value=subprocess.CompletedProcess([], 0, '0x01 0 123 host App')):
            self.assertTrue(keycheck.has_app_window({123}))
            self.assertFalse(keycheck.has_app_window({999}))

    def test_boot_startup_blocks_close_popup_from_button(self):
        with patch.object(keycheck, '_launched_app', None), patch.object(keycheck, 'startup_in_progress', return_value=True), patch.object(keycheck, 'close_app') as close, patch.object(keycheck.subprocess, 'Popen') as launch:
            keycheck.toggle_app()
            close.assert_not_called()
            launch.assert_not_called()

    def test_running_app_shows_close_instead_of_starting_second_copy(self):
        with patch.object(keycheck, 'app_pids', return_value={123}), patch.object(
            keycheck, 'close_app'
        ) as close, patch.object(keycheck.subprocess, 'Popen') as launch:
            keycheck.toggle_app()
            close.assert_called_once()
            launch.assert_not_called()

    def test_shutdown_uses_noninteractive_sudo_and_reports_failure(self):
        with patch.object(keycheck.os, "geteuid", return_value=1000, create=True), patch.object(
            keycheck.subprocess, "run", return_value=subprocess.CompletedProcess([], 1, "", "denied")
        ) as run:
            self.assertFalse(keycheck.request_shutdown())
            self.assertEqual(run.call_args[0][0], ["sudo", "-n", "/sbin/shutdown", "-h", "now"])

    def test_executable_matching_does_not_target_chromium_or_other_names(self):
        with tempfile.TemporaryDirectory() as folder:
            proc = Path(folder)
            for pid in (1, 2, 3):
                (proc / str(pid)).mkdir()
            names = {"1": "/app/CurtantControl", "2": "/usr/bin/chromium", "3": "/app/CurtantControl-other"}
            with patch.object(keycheck.os, "readlink", side_effect=lambda p: names[Path(p).parent.name]):
                self.assertEqual(keycheck.app_pids(proc), {1})

    def test_confirmation_then_close_only_matching_window_pid(self):
        responses = [subprocess.CompletedProcess([], 0), subprocess.CompletedProcess([], 0,
            "0x01 0 123 host App\n0x02 0 999 host Chromium\n"), subprocess.CompletedProcess([], 0)]
        with patch.object(keycheck, "app_pids", return_value={123}), patch.object(
            keycheck.subprocess, "run", side_effect=responses
        ) as run:
            keycheck.close_app()
            self.assertEqual(run.call_args_list[0][0][0][-1], '--confirm-stop')
            self.assertEqual(run.call_args_list[-1][0][0], ["wmctrl", "-ic", "0x01"])
            self.assertEqual(run.call_count, 3)

    def test_cancel_or_popup_failure_never_requests_close(self):
        for code in (1, 2):
            with patch.object(keycheck.subprocess, "run", return_value=subprocess.CompletedProcess([], code)) as run:
                keycheck.close_app()
                self.assertEqual(run.call_count, 1)

    def test_missing_app_does_not_close_other_windows(self):
        with patch.object(keycheck, 'app_pids', return_value=set()), patch.object(
            keycheck.subprocess, 'run', side_effect=[subprocess.CompletedProcess([], 0),
                subprocess.CompletedProcess([], 0, '0x01 0 999 host Other')]
        ) as run:
            keycheck.close_app()
            self.assertEqual(run.call_count, 2)


class AutostartTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        module_spec = importlib.util.spec_from_file_location('configure_autostart', SCRIPT.parent / 'configure_autostart.py')
        cls.module = importlib.util.module_from_spec(module_spec)
        module_spec.loader.exec_module(cls.module)

    def test_existing_launcher_keeps_metadata_and_unrelated_entries(self):
        with tempfile.TemporaryDirectory() as folder:
            directory = Path(folder)
            app = directory / 'curtantcontrol.desktop'
            app.write_text('[Desktop Entry]\nName=Custom name\nExec=' + self.module.APP + '\nTerminal=false\n', encoding='utf-8')
            other = directory / 'other.desktop'
            other.write_text('[Desktop Entry]\nExec=/usr/bin/other\n', encoding='utf-8')
            before = other.read_bytes()
            self.module.configure(directory)
            self.assertIn('Exec=' + self.module.LAUNCH, app.read_text())
            self.assertIn('Name=Custom name', app.read_text())
            self.assertEqual(other.read_bytes(), before)
            self.assertEqual(len(list(directory.glob('*.bak-*'))), 1)
            self.module.configure(directory)
            self.assertEqual(len(list(directory.glob('*.bak-*'))), 1)

    def test_missing_launcher_created_and_action_section_not_rewritten(self):
        text = '[Desktop Entry]\nExec=/usr/bin/other\n[Desktop Action test]\nExec=' + self.module.APP
        self.assertIsNone(self.module.rewrite_desktop(text))
        with tempfile.TemporaryDirectory() as folder:
            directory = Path(folder)
            self.module.configure(directory)
            self.assertIn('--start-app', (directory / 'curtantcontrol-starting.desktop').read_text())

    def test_desktop_menu_launchers_migrate_to_startup_indicator(self):
        with tempfile.TemporaryDirectory() as folder:
            home=Path(folder)
            desktop=home/'Desktop'
            menu=home/'.local/share/applications'
            desktop.mkdir(); menu.mkdir(parents=True)
            path=desktop/'CurtantControl.desktop'
            text='[Desktop Entry]\nName=CurtantControl\nExec=/bin/sh "'+str(Path(self.module.APP).parent/'start.sh')+'"\nTerminal=false\n'
            path.write_bytes(text.encode())
            (menu/'com.curtantcontrol.Photobooth.desktop').write_bytes(text.encode())
            with patch.dict('os.environ',{'XDG_DATA_HOME':str(home/'.local/share')}):
                self.module.configure_shortcuts(home,desktop)
            self.assertIn('--start-app',path.read_text())
            self.assertIn('--start-app',(menu/'com.curtantcontrol.Photobooth.desktop').read_text())
            self.assertEqual(len(list(desktop.glob('*.bak-*'))),0)
            self.assertEqual(len(list((home/'.local/share/CurtantControl/shortcut-backups').glob('*.bak-*'))),2)


if __name__ == "__main__":
    unittest.main()
