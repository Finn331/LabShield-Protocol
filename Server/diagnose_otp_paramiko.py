import json
import os
import sys
import time
import urllib.error
import urllib.request

import paramiko


HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ["LABSHIELD_SSH_PASSWORD"]


def request_otp():
    payload = json.dumps({"email": "debug-otp@example.com"}).encode("utf-8")
    req = urllib.request.Request(
        "https://labshieldprotocol.my.id/api/register/request-otp",
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as res:
            print(f"OTP HTTP {res.status}")
            print(res.read().decode("utf-8", errors="replace"))
    except urllib.error.HTTPError as exc:
        print(f"OTP HTTP {exc.code}")
        print(exc.read().decode("utf-8", errors="replace"))


def exec_remote(client, command):
    stdin, stdout, stderr = client.exec_command(command, timeout=120)
    channel = stdout.channel
    while not channel.exit_status_ready():
        if channel.recv_ready():
            print(channel.recv(4096).decode(errors="replace"), end="", flush=True)
        if channel.recv_stderr_ready():
            print(channel.recv_stderr(4096).decode(errors="replace"), end="", file=sys.stderr, flush=True)
        time.sleep(0.2)
    out = stdout.read().decode(errors="replace")
    err = stderr.read().decode(errors="replace")
    if out:
        print(out, end="")
    if err:
        print(err, end="", file=sys.stderr)
    print(f"REMOTE_EXIT={channel.recv_exit_status()}")


def main():
    request_otp()

    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(
        HOST,
        username=USER,
        password=PASSWORD,
        timeout=20,
        auth_timeout=20,
        banner_timeout=20,
        look_for_keys=False,
        allow_agent=False,
    )
    try:
        command = r'''
set -e
cd /opt/labshield-server
echo '--- /opt/labshield-server/.env SMTP lines ---'
grep -n 'SMTP\|OTP' .env 2>/dev/null || true
echo '--- docker-compose SMTP lines ---'
grep -n 'SMTP\|OTP\|env_file' docker-compose.yml 2>/dev/null || true
echo '--- container SMTP env presence ---'
docker exec labshield-server sh -lc 'for key in SMTP_HOST SMTP_PORT SMTP_SECURE SMTP_USER SMTP_PASS SMTP_FROM OTP_SECRET; do value=$(printenv "$key" || true); if [ -n "$value" ]; then if [ "$key" = "SMTP_PASS" ]; then echo "$key=<redacted>"; else echo "$key=$value"; fi; else echo "$key=<unset>"; fi; done'
echo '--- recent logs ---'
docker logs --tail 60 labshield-server
'''
        exec_remote(client, command)
    finally:
        client.close()


if __name__ == "__main__":
    main()
