import os
import sys

import paramiko


HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ.get("LABSHIELD_SSH_PASSWORD", "")


def main() -> int:
    if not PASSWORD:
        print("LABSHIELD_SSH_PASSWORD is not set", file=sys.stderr)
        return 1

    command = (
        "set -e; "
        "echo '=== docker ==='; "
        "docker --version || true; "
        "docker compose version || true; "
        "echo '=== appdir ==='; "
        "ls -la /opt/labshield-server || true; "
        "echo '=== compose ==='; "
        "cd /opt/labshield-server && docker compose ps || true; "
        "echo '=== cloudflared ==='; "
        "command -v cloudflared || true; "
        "systemctl is-active cloudflared || true; "
        "docker ps --filter name=cloudflared --format '{{.Names}} {{.Status}} {{.Ports}}' || true; "
        "echo '=== ports ==='; "
        "ss -ltnp | grep -E ':(80|443|5000)' || true; "
        "echo '=== api ==='; "
        "curl -fsS http://127.0.0.1:5000/api/scores || true"
    )

    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(
        hostname=HOST,
        username=USER,
        password=PASSWORD,
        timeout=20,
        auth_timeout=20,
        banner_timeout=20,
        look_for_keys=False,
        allow_agent=False,
    )

    try:
        _, stdout, stderr = client.exec_command(command, timeout=240)
        out = stdout.read().decode("utf-8", errors="replace")
        err = stderr.read().decode("utf-8", errors="replace")
        if out:
            print(out, end="")
        if err:
            print(err, end="", file=sys.stderr)
    finally:
        client.close()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
