import os
import sys
import time

import paramiko


HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ.get("LABSHIELD_SSH_PASSWORD")
TUNNEL_TOKEN = os.environ.get("LABSHIELD_CLOUDFLARE_TUNNEL_TOKEN")


def connect():
    if not PASSWORD:
        raise RuntimeError("Set LABSHIELD_SSH_PASSWORD before running this script.")

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
    return client


def exec_checked(client, command, timeout=None, label=None):
    print(f"==> {label or command.splitlines()[0][:100]}")
    stdin, stdout, stderr = client.exec_command(command, timeout=timeout)
    channel = stdout.channel

    while not channel.exit_status_ready():
        while channel.recv_ready():
            sys.stdout.write(channel.recv(4096).decode(errors="replace"))
            sys.stdout.flush()
        while channel.recv_stderr_ready():
            sys.stderr.write(channel.recv_stderr(4096).decode(errors="replace"))
            sys.stderr.flush()
        time.sleep(0.2)

    out = stdout.read().decode(errors="replace")
    err = stderr.read().decode(errors="replace")
    if out:
        print(out, end="")
    if err:
        print(err, end="", file=sys.stderr)

    status = channel.recv_exit_status()
    if status != 0:
        raise RuntimeError(f"Remote command failed with exit code {status}: {label or command}")
    return out


def sudo_script(script):
    escaped_password = PASSWORD.replace("'", "'\\''")
    escaped_script = script.replace("'", "'\\''")
    return f"printf '%s\\n' '{escaped_password}' | sudo -S bash -lc '{escaped_script}'"


def main():
    if not TUNNEL_TOKEN:
        raise RuntimeError("Set LABSHIELD_CLOUDFLARE_TUNNEL_TOKEN with the Cloudflare Tunnel token.")

    escaped_token = TUNNEL_TOKEN.replace("'", "'\\''")
    script = f"""
set -e
export DEBIAN_FRONTEND=noninteractive

if ! command -v cloudflared >/dev/null 2>&1; then
    mkdir -p /usr/share/keyrings
    curl -fsSL https://pkg.cloudflare.com/cloudflare-main.gpg -o /usr/share/keyrings/cloudflare-main.gpg
    echo 'deb [signed-by=/usr/share/keyrings/cloudflare-main.gpg] https://pkg.cloudflare.com/cloudflared any main' > /etc/apt/sources.list.d/cloudflared.list
    apt-get update
    apt-get install -y cloudflared
fi

cloudflared service uninstall >/dev/null 2>&1 || true
cloudflared service install '{escaped_token}'
systemctl enable cloudflared
systemctl restart cloudflared
systemctl --no-pager --full status cloudflared || true
"""

    client = connect()
    try:
        exec_checked(client, sudo_script(script), timeout=600, label="install and start cloudflared tunnel")
        exec_checked(client, "curl -fsS http://127.0.0.1:5000/api/scores >/dev/null", timeout=30)
    finally:
        client.close()

    print("Cloudflare Tunnel configuration complete.")


if __name__ == "__main__":
    main()
