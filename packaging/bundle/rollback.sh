#!/bin/sh
set -eu
echo 'Starting rollback to original Chromium and keycheck...'
cd -- "$(dirname -- "$0")"
if [ "$#" -gt 1 ]; then
    echo 'Usage: sh rollback.sh [backup.tar.gz]' >&2
    exit 1
fi
if [ "$#" = 1 ]; then
    backup=$1
else
    if [ ! -f .rollback-target ]; then
        exec python3 -u rollback.py legacy
    fi
    backup=$(cat .rollback-target)
fi
python3 -u rollback.py legacy "$backup"
echo 'CurtantControl stopped; original Chromium system and keycheck started.'
