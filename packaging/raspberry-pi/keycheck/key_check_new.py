#!/usr/bin/env python3
"""Pi Buster/Python 3.7: BCM24 shutdown, BCM25 gracefully close CurtantControl.

Only these two inputs are owned. No relay, motor or I2C access. GPIO24 cannot wake Pi 4.
"""
import logging
import os
from pathlib import Path
import signal
import sys
import subprocess
import threading
import time

LOG = logging.getLogger("keycheck")
SHUTDOWN_PIN = 24  # Physical pin 18
CLOSE_PIN = 25     # Physical pin 22
APP_PATH = Path('/home/pi/.local/share/CurtantControl/app/CurtantControl')
_launched_app = None


def toggle_app():
    global _launched_app
    try:
        # The helper remains alive while the centered startup indicator is visible.
        if _launched_app is not None and _launched_app.poll() is None:
            LOG.info("Startup indicator is active; duplicate press skipped")
            return
        if startup_in_progress():
            LOG.info("Desktop autostart is in progress; button press skipped")
            return
        if app_pids():
            close_app()
            return
        _launched_app = subprocess.Popen([sys.executable, "-u", str(Path(__file__).resolve()),
            "--starting-overlay"], cwd=str(APP_PATH.parent),
            stdin=subprocess.DEVNULL, start_new_session=True)
        LOG.info("Showing startup indicator pid=%s", _launched_app.pid)
    except (OSError, subprocess.SubprocessError):
        LOG.exception("Could not start CurtantControl")


def has_app_window(pids):
    windows = subprocess.run(["wmctrl", "-lp"], capture_output=True, text=True,
                             check=True, timeout=3)
    return any(len(parts) >= 3 and parts[2].isdigit() and int(parts[2]) in pids
               for parts in (line.split(None, 4) for line in windows.stdout.splitlines()))


def startup_state(process, elapsed, visible):
    if process.poll() is not None:
        return "failed"
    if visible:
        return "ready"
    return "timeout" if elapsed >= 60 else "waiting"


def create_overlay(message, height=240):
    import tkinter as tk
    root = tk.Tk()
    root.title("Curtant Control")
    root.overrideredirect(True)
    root.attributes("-topmost", True)
    width = 440
    root.geometry("{}x{}+{}+{}".format(width, height,
        (root.winfo_screenwidth() - width) // 2, (root.winfo_screenheight() - height) // 2))
    frame = tk.Frame(root, bg="#20222B", highlightbackground="#F2AA7E", highlightthickness=2)
    frame.pack(fill="both", expand=True)
    content = tk.Frame(frame, bg="#20222B")
    content.place(relx=0.5, rely=0.5, anchor="center", width=420)
    try:
        path = Path(__file__).with_name('123click-logo.png')
        try:
            from PIL import Image, ImageTk
            with Image.open(str(path)) as original:
                # Same optical viewport as BrandLogo.tight in the desktop app.
                image = original.crop((220, 148, 1940, 558)) if original.size == (2172, 724) else original.copy()
                image.thumbnail((150, 40), Image.LANCZOS)
                logo = ImageTk.PhotoImage(image)
        except ImportError:
            logo = tk.PhotoImage(file=str(path))
            factor = max(1, (logo.width() + 189) // 190)
            logo = logo.subsample(factor, factor)
        root.brand_logo = logo
        tk.Label(content, image=logo, bg="#20222B").pack(pady=(0, 6))
    except (tk.TclError, OSError):
        LOG.warning("Brand logo unavailable; using camera icon")
        icon = tk.Canvas(content, width=100, height=80, bg="#20222B", highlightthickness=0)
        icon.pack(pady=(0, 6))
        icon.create_rectangle(14, 24, 86, 68, outline="#F2AA7E", width=3)
        icon.create_rectangle(30, 15, 53, 24, outline="#F2AA7E", width=3)
        icon.create_oval(36, 31, 66, 61, outline="#F2AA7E", width=3)
        icon.create_oval(72, 30, 78, 36, fill="#F2AA7E", outline="")
    tk.Label(content, text="Curtant Control", bg="#20222B", fg="#F5F3F6",
             font=("DejaVu Sans", 19, "bold")).pack()
    label = tk.Label(content, text=message, bg="#20222B", fg="#F2AA7E",
                     font=("DejaVu Sans", 12), wraplength=400, justify="center", anchor="center")
    label.pack(pady=8)
    return root, content, label


def show_stop_confirmation():
    import tkinter as tk
    root, frame, label = create_overlay("Stop Curtant Control?", height=300)
    confirmed = [False]
    def cancel(event=None):
        root.destroy()
    def confirm():
        confirmed[0] = True
        root.destroy()
    row = tk.Frame(frame, bg="#20222B")
    row.pack(pady=(12, 0))
    cancel_button = tk.Button(row, text="Cancel", command=cancel, bg="#44464F",
        fg="#F5F3F6", activebackground="#555864", activeforeground="#FFFFFF",
        font=("DejaVu Sans", 12, "bold"), relief="flat", width=11, pady=9,
        highlightthickness=0, cursor="hand2")
    cancel_button.pack(side="left", padx=(0, 12))
    tk.Button(row, text="Stop", command=confirm, bg="#F2AA7E", fg="#19191F",
        activebackground="#FFC49D", activeforeground="#19191F",
        font=("DejaVu Sans", 12, "bold"), relief="flat", width=11, pady=9,
        highlightthickness=0, cursor="hand2").pack(side="left")
    root.bind("<Escape>", cancel)
    root.protocol("WM_DELETE_WINDOW", cancel)
    root.after(100, cancel_button.focus_set)
    root.mainloop()
    return 0 if confirmed[0] else 1


def acquire_startup_lock():
    import fcntl
    path = Path.home() / '.cache' / 'curtantcontrol-startup.lock'
    path.parent.mkdir(parents=True, exist_ok=True)
    handle = path.open('a')
    try:
        fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
        return handle
    except BlockingIOError:
        handle.close()
        return None


def startup_in_progress():
    if os.name != 'posix':
        return False
    handle = acquire_startup_lock()
    if handle is None:
        return True
    handle.close()
    return False


def show_starting_overlay():
    # Separate process: Tk stays on its main thread and never blocks GPIO input.
    import tkinter as tk
    startup_lock = acquire_startup_lock()
    if startup_lock is None:
        LOG.info("Another startup indicator owns the launch; skipping duplicate")
        return
    if app_pids():
        startup_lock.close()
        return
    root, frame, label = create_overlay("Starting...")
    process = [None]
    started = [0.0]
    def show_error(message):
        LOG.error(message)
        label.config(text=message, fg="#FF9BA9")
        tk.Button(frame, text="Close", command=root.destroy).pack()
    def observe():
        try:
            visible = has_app_window({process[0].pid})
        except (OSError, subprocess.SubprocessError):
            visible = False
        state = startup_state(process[0], time.monotonic() - started[0], visible)
        if state == "ready":
            LOG.info("CurtantControl window appeared; hiding startup indicator")
            root.destroy()
        elif state == "failed":
            show_error("App could not start. Please check the service log.")
        elif state == "timeout":
            show_error("Startup is taking too long. Please check the service log.")
        else:
            label.config(text="Starting" + "." * (1 + int((time.monotonic() - started[0]) * 2) % 3))
            root.after(250, observe)
    def launch():
        try:
            if not APP_PATH.is_file() or not os.access(str(APP_PATH), os.X_OK):
                show_error("App file is missing or cannot be executed.")
                return
            # Recheck after showing the indicator to avoid an autostart race.
            if app_pids():
                root.destroy()
                return
            process[0] = subprocess.Popen([str(APP_PATH)], cwd=str(APP_PATH.parent),
                stdin=subprocess.DEVNULL, start_new_session=True)
            LOG.info("Started CurtantControl pid=%s", process[0].pid)
            started[0] = time.monotonic()
            root.after(250, observe)
        except (OSError, subprocess.SubprocessError):
            LOG.exception("App launch failed")
            show_error("App could not start. Please check the service log.")
    root.after(100, launch)
    try:
        root.mainloop()
    finally:
        startup_lock.close()


class DebouncedButton:
    """Arm only after release; one press per stable high-to-low transition."""
    def __init__(self, initial, now, delay=0.08):
        self.raw = self.stable = bool(initial)
        self.changed = now
        self.delay = delay
        self.armed = bool(initial)

    def update(self, level, now):
        level = bool(level)
        if level != self.raw:
            self.raw, self.changed = level, now
        if level == self.stable or now - self.changed < self.delay:
            return False
        self.stable = level
        if level:
            self.armed = True
            return False
        pressed, self.armed = self.armed, False
        return pressed


def request_shutdown():
    command = ["/sbin/shutdown", "-h", "now"]
    if os.geteuid() != 0:
        command = ["sudo", "-n"] + command
    result = subprocess.run(command, capture_output=True, text=True, timeout=15)
    if result.returncode:
        LOG.error("Shutdown failed: %s", result.stderr.strip())
        return False
    LOG.info("System shutdown requested")
    return True


def app_pids(proc=Path("/proc")):
    result = set()
    for entry in proc.iterdir():
        if not entry.name.isdigit():
            continue
        try:
            # Match apphost executable, never Chromium, Python or title substrings.
            executable = os.readlink(str(entry / "exe"))
            if executable.endswith(" (deleted)"):
                executable = executable[:-10]
            if Path(executable).name == "CurtantControl":
                result.add(int(entry.name))
        except OSError:
            pass
    return result


def close_app():
    try:
        LOG.info("Opening styled Stop confirmation")
        answer = subprocess.run([sys.executable, "-u", str(Path(__file__).resolve()),
                                 "--confirm-stop"], stdin=subprocess.DEVNULL)
        if answer.returncode != 0:
            LOG.info("Stop cancelled or confirmation unavailable (code=%s)", answer.returncode)
            return
        pids = app_pids()
        windows = subprocess.run(["wmctrl", "-lp"], capture_output=True, text=True,
                                 check=True, timeout=5)
        closed = 0
        for line in windows.stdout.splitlines():
            parts = line.split(None, 4)
            if len(parts) >= 3 and parts[2].isdigit() and int(parts[2]) in pids:
                # WM_DELETE_WINDOW via the window manager; lets Avalonia run Closing/DisposeAsync.
                subprocess.run(["wmctrl", "-ic", parts[0]], check=True, timeout=5)
                closed += 1
        LOG.info("Requested graceful close for %s CurtantControl window(s)", closed)
        if not closed:
            LOG.warning("No matching window. App may be stopped or DISPLAY/XAUTHORITY is incorrect.")
    except Exception:
        LOG.exception("Could not show popup or close CurtantControl; check Tk and X11 session")


def main():
    import RPi.GPIO as GPIO
    os.environ.setdefault("DISPLAY", ":0")
    os.environ.setdefault("XAUTHORITY", str(Path.home() / ".Xauthority"))
    stop = threading.Event()
    for sig in (signal.SIGTERM, signal.SIGINT):
        signal.signal(sig, lambda *_: stop.set())
    pins = [SHUTDOWN_PIN, CLOSE_PIN]
    configured = []
    close_worker = None
    shutdown_pending = False
    try:
        GPIO.setmode(GPIO.BCM)
        for pin in pins:
            GPIO.setup(pin, GPIO.IN, pull_up_down=GPIO.PUD_UP)
            configured.append(pin)
        buttons = {pin: DebouncedButton(GPIO.input(pin), time.monotonic(),
            delay=0.04 if pin == CLOSE_PIN else 0.08) for pin in pins}
        LOG.info("Listening: shutdown BCM24/pin18, close CurtantControl BCM25/pin22")
        while not stop.wait(0.01):
            now = time.monotonic()
            levels = {pin: GPIO.input(pin) for pin in pins}
            if bool(levels[CLOSE_PIN]) != buttons[CLOSE_PIN].raw:
                LOG.info("App button raw: %s", "released" if levels[CLOSE_PIN] else "pressed")
            pressed = {pin: buttons[pin].update(levels[pin], now) for pin in pins}
            if pressed[SHUTDOWN_PIN] and not shutdown_pending:
                try:
                    shutdown_pending = request_shutdown()
                except (OSError, subprocess.SubprocessError):
                    LOG.exception("Shutdown request failed; release and press to retry")
            elif pressed[CLOSE_PIN] and not shutdown_pending:
                if close_worker is None or not close_worker.is_alive():
                    LOG.info("App button accepted; checking CurtantControl")
                    close_worker = threading.Thread(target=toggle_app, daemon=True)
                    close_worker.start()
                else:
                    LOG.info("App button ignored: previous action or popup is still open")
    finally:
        if configured:
            GPIO.cleanup(configured)


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    os.environ.setdefault("DISPLAY", ":0")
    os.environ.setdefault("XAUTHORITY", str(Path.home() / ".Xauthority"))
    if sys.argv[1:] in (["--starting-overlay"], ["--start-app"]):
        show_starting_overlay()
    elif sys.argv[1:] == ["--confirm-stop"]:
        sys.exit(show_stop_confirmation())
    else:
        main()
