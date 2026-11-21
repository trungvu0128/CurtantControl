# Keycheck v6 for the existing Pi

Run sh install.sh as pi in the extracted folder (without sudo). Requires the
existing keycheck.service and RPi.GPIO. Installs wmctrl/python3-tk, preserves
key_check1.py and backs up the replacement script/drop-in before restarting.
Target: Python3.7, Pi user pi, X11 DISPLAY=:0 and /home/pi/.Xauthority.

BCM24 / physical18 requests Pi shutdown; it cannot wake Pi4. BCM25 / physical22:
- No CurtantControl process: centered camera icon + animated Starting...;
  launches /home/pi/.local/share/CurtantControl/app/CurtantControl once.
- Running app: centered dark/camera/peach Stop CurtantControl? confirmation,
  same shared widget/style as Starting. Cancel or Escape dismisses immediately;
  Stop requests normal window close by executable PID, never force-kills.
- Startup indicator busy: repeated app-button presses are ignored.

v6 replaces legacy PyAutoGUI dialogs entirely with Tk helper processes; no
second cancellation acknowledgement and no PyAutoGUI dependency. GPIO processing
remains independent of popup mainloops. Uses40ms app debounce,80ms shutdown,
10ms polling; boot-held switches must be released before triggering.
Startup window appearance hides the indicator. Missing executable/early exit/
60s timeout shows an error with Close; a slow app is not killed on timeout.
No GPIO2/3, relays, motor outputs or C# binary changes.

Inspect: sudo journalctl -fu keycheck.service
Stop duplicate manually started old key_check1.py copies. Shutdown uses sudo -n
and existing pi permissions; installer does not modify sudo policy. Apps started
by the service inherit its cgroup and may stop on service restart; close normally
before updating. Host tests do not verify actual Pi GPIO or X11 popup placement.

Rollback: sudo systemctl edit keycheck.service, replace ExecStart with:
[Service]
ExecStart=
ExecStart=/usr/bin/python3 /home/pi/r1/key_check1.py
Then sudo systemctl daemon-reload; sudo systemctl restart keycheck.service.
If an older custom override existed, restore its timestamped backup instead.


v7 also configures desktop-login autostart through --start-app (startup-only,
never a close action). configure_autostart.py updates direct installed-app Exec
entries under ~/.config/autostart with timestamped backups, preserving metadata
and unrelated entries. If none exists, creates curtantcontrol-starting.desktop.
Custom shell-script launchers are not rewritten; inspect them for duplicate direct
launches if the popup still does not appear. A per-user flock spans the startup
indicator: desktop login and button cannot launch duplicates, and the GPIO button
cannot show Stop while the desktop-start indicator is active. Autostart operates
when pi's graphical desktop logs in, not before a graphical session exists.
Run sh install.sh then test after logout/login or reboot. Original keycheck.service
is still used for GPIO; --start-app does not initialize GPIO at all.


Brand update/v8: Starting and Stop share the supplied123Click transparent logo;
installer copies123click-logo.png beside the script and adds Pillow/ImageTk for
smooth scaling. Falls back to Tk image rendering or the camera vector if unavailable.
Included in the app0.8.19 small-update ZIP under keycheck/; run sh keycheck/install.sh
from the extracted update folder to update these separate physical-button popups.


0.8.22 bundled brand refresh: Starting/Stop crop the same artwork viewport used
by BrandLogo.tight before scaling to240x60. This removes transparent margins and
keeps the shared logo visibly larger. Tk-only fallback retains full framing.
No action or confirmation semantics changed.


v9 popup layout: logo/title/message/buttons form one content group centered both
vertically and horizontally in the existing440x240/440x300 windows. Display name
is Curtant Control, confirmation Stop Curtant Control?. Executable/path/service
names stay unchanged. Cancel/Stop width/font/padding and all button actions remain
unchanged. Shared layout also centers Starting/error content.
