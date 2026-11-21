#!/bin/sh
set -eu
cd "$(dirname "$0")"
test "$(id -un)" = pi || { echo 'Run as pi: sh install.sh (without sudo)'; exit 1; }
/usr/bin/python3 -c 'import RPi.GPIO' || { echo 'Install python3-rpi.gpio first.'; exit 1; }
/usr/bin/python3 -m py_compile key_check_new.py
case "${1-}" in
    '') sudo apt-get install -y wmctrl python3-tk python3-pil python3-pil.imagetk ;;
    --skip-packages) : ;;
    *) echo 'Usage: sh install.sh [--skip-packages]'; exit 1 ;;
esac
mkdir -p /home/pi/r1
stamp=$(date +%Y%m%d-%H%M%S)
if test -f /home/pi/r1/key_check_new.py; then
    cp -p /home/pi/r1/key_check_new.py "/home/pi/r1/key_check_new.py.bak-$stamp"
fi
cp key_check_new.py /home/pi/r1/key_check_new.py
cp 123click-logo.png /home/pi/r1/123click-logo.png
if ! systemctl cat keycheck.service >/dev/null 2>&1; then
    sudo install -m 644 keycheck.service /etc/systemd/system/keycheck.service
fi
sudo mkdir -p /etc/systemd/system/keycheck.service.d
if test -f /etc/systemd/system/keycheck.service.d/override.conf; then
    sudo cp -p /etc/systemd/system/keycheck.service.d/override.conf "/etc/systemd/system/keycheck.service.d/override.conf.bak-$stamp"
fi
sudo install -m 644 override.conf /etc/systemd/system/keycheck.service.d/override.conf
/usr/bin/python3 configure_autostart.py
sudo systemctl daemon-reload
sudo systemctl enable keycheck.service
sudo systemctl restart keycheck.service
sudo systemctl --no-pager --full status keycheck.service
