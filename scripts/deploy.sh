#!/usr/bin/env bash
set -euo pipefail
set +x

for name in SSH_HOST SSH_USER SSH_PASSWORD TELEGRAM_BOT_TOKEN TRACKER_OAUTH_TOKEN TRACKER_ORG_ID TRACKER_ORG_TYPE ALLOWED_TELEGRAM_USER_ID; do
  if [[ -z "${!name:-}" ]]; then
    echo "Missing GitHub Actions secret: $name" >&2
    exit 1
  fi
done

python3 scripts/smoke.py

sudo apt-get update -qq
sudo apt-get install -y -qq sshpass >/dev/null

workdir="$(mktemp -d)"
trap 'rm -rf "$workdir"' EXIT
ssh-keyscan -T 10 -t ed25519 "$SSH_HOST" > "$workdir/known_hosts" 2>/dev/null
ssh-keygen -lf "$workdir/known_hosts" | grep -Fq 'SHA256:/JSgUuv8GdiYLSI+IQgn2dj+s6YU+VwqfcqmugqHz0M' || {
  echo 'SSH host fingerprint does not match the course server.' >&2
  exit 1
}

git archive --format=tar HEAD | gzip > "$workdir/release.tar.gz"
python3 - "$workdir/bot.env" <<'PY'
import os
import pathlib
import sys

names = ('TELEGRAM_BOT_TOKEN', 'TRACKER_OAUTH_TOKEN', 'TRACKER_ORG_ID',
         'TRACKER_ORG_TYPE', 'ALLOWED_TELEGRAM_USER_ID')
values = [os.environ[name] for name in names]
if any('\n' in value or '\r' in value for value in values):
    raise SystemExit('A secret contains a newline')
path = pathlib.Path(sys.argv[1])
path.write_text(''.join(f'{name}={value}\n' for name, value in zip(names, values)))
path.chmod(0o600)
PY

export SSHPASS="$SSH_PASSWORD"
ssh_opts=(-o "UserKnownHostsFile=$workdir/known_hosts" -o StrictHostKeyChecking=yes)
destination="${SSH_USER}@${SSH_HOST}"
sshpass -e ssh "${ssh_opts[@]}" "$destination" 'mkdir -p "$HOME/telegram-bot" && chmod 700 "$HOME/telegram-bot"'
sshpass -e scp "${ssh_opts[@]}" "$workdir/release.tar.gz" "$workdir/bot.env" "$destination:~/telegram-bot/"
sshpass -e ssh "${ssh_opts[@]}" "$destination" \
  'set -eu; cd "$HOME/telegram-bot"; tar -xzf release.tar.gz; rm release.tar.gz; mv bot.env .env; chmod 600 .env; docker compose up -d --build --remove-orphans; sleep 8; docker compose ps; docker compose logs --tail=30 bot; test "$(docker compose ps --status running --services)" = bot'
