import os
import sys
import time

import paramiko


HOST = os.environ.get("LABSHIELD_SSH_HOST", "100.69.10.5")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ.get("LABSHIELD_SSH_PASSWORD")


def shell_quote(value):
    return "'" + value.replace("'", "'\"'\"'") + "'"


def main():
    if not PASSWORD:
        raise RuntimeError("Set LABSHIELD_SSH_PASSWORD")
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(HOST, username=USER, password=PASSWORD, timeout=20, auth_timeout=20, banner_timeout=20, look_for_keys=False, allow_agent=False)
    command = r"""
set -e
cd /opt/labshield-server
echo '=== compose ==='
docker compose ps || true
echo '=== host files ==='
ls -la public | head
echo '=== local headers ==='
curl -sS -I http://127.0.0.1:5000/ || true
curl -sS -I http://127.0.0.1:5000/dashboard.html || true
curl -sS -I http://127.0.0.1:5000/angket.html || true
echo '=== container files ==='
docker exec labshield-server sh -lc 'ls -la /app/public | head; test -f /app/public/dashboard.html && echo dash-ok || echo dash-missing; test -f /app/public/angket.html && echo angket-ok || echo angket-missing' || true
echo '=== api ==='
curl -sS http://127.0.0.1:5000/api/angket/status?username=testing_siswa || true
echo '=== logs ==='
docker logs --tail 30 labshield-server || true
"""
    sudo = f"printf '%s\\n' {shell_quote(PASSWORD)} | sudo -S bash -lc {shell_quote(command)}"
    try:
        _, stdout, stderr = client.exec_command(sudo, timeout=300)
        channel = stdout.channel
        while not channel.exit_status_ready():
            if channel.recv_ready():
                print(channel.recv(4096).decode(errors="replace"), end="")
            if channel.recv_stderr_ready():
                print(channel.recv_stderr(4096).decode(errors="replace"), end="", file=sys.stderr)
            time.sleep(0.2)
        print(stdout.read().decode(errors="replace"), end="")
        print(stderr.read().decode(errors="replace"), end="", file=sys.stderr)
        raise SystemExit(channel.recv_exit_status())
    finally:
        client.close()


if __name__ == "__main__":
    main()
