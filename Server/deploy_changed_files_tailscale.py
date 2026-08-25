import os
import sys
import time
from pathlib import Path

import paramiko


HOST = os.environ.get("LABSHIELD_SSH_HOST", "100.69.10.5")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ.get("LABSHIELD_SSH_PASSWORD")
REMOTE_ROOT = "/opt/labshield-server"

FILES = [
    ("Server/server.js", f"{REMOTE_ROOT}/server.js"),
    ("Server/public/dashboard.html", f"{REMOTE_ROOT}/public/dashboard.html"),
    ("Server/public/student-dashboard.html", f"{REMOTE_ROOT}/public/student-dashboard.html"),
    ("Server/public/angket.html", f"{REMOTE_ROOT}/public/angket.html"),
]


def shell_quote(value):
    return "'" + value.replace("'", "'\"'\"'") + "'"


def connect():
    if not PASSWORD:
        raise RuntimeError("Set LABSHIELD_SSH_PASSWORD before running.")
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
    return client


def exec_stream(client, command, timeout=900):
    stdin, stdout, stderr = client.exec_command(command, timeout=timeout)
    channel = stdout.channel
    while not channel.exit_status_ready():
        if channel.recv_ready():
            print(channel.recv(4096).decode(errors="replace"), end="")
        if channel.recv_stderr_ready():
            print(channel.recv_stderr(4096).decode(errors="replace"), end="", file=sys.stderr)
        time.sleep(0.2)
    out = stdout.read().decode(errors="replace")
    err = stderr.read().decode(errors="replace")
    if out:
        print(out, end="")
    if err:
        print(err, end="", file=sys.stderr)
    code = channel.recv_exit_status()
    if code:
        raise RuntimeError(f"Remote command failed with exit code {code}")


def main():
    root = Path(__file__).resolve().parents[1]
    client = connect()
    try:
        sftp = client.open_sftp()
        temp_files = []
        try:
            for local, remote in FILES:
                local_path = root / local
                temp_remote = f"/tmp/labshield-{Path(remote).name}"
                temp_files.append((temp_remote, remote))
                print(f"upload {local} -> {temp_remote}")
                sftp.put(str(local_path), temp_remote)
        finally:
            sftp.close()

        copy_commands = " && ".join(
            f"cp {shell_quote(src)} {shell_quote(dst)}" for src, dst in temp_files
        )
        sudo_copy = f"printf '%s\\n' {shell_quote(PASSWORD)} | sudo -S bash -lc {shell_quote(copy_commands)}"
        exec_stream(client, sudo_copy, timeout=120)
        compose_command = (
            "cd /opt/labshield-server && "
            "docker compose up -d --build --remove-orphans && "
            "sleep 3 && "
            "docker ps --filter name=labshield-server --format '{{.Names}}|{{.Status}}|{{.Ports}}' && "
            "curl -fsS http://127.0.0.1:5000/api/angket/status?username=testing_siswa"
        )
        sudo_compose = f"printf '%s\\n' {shell_quote(PASSWORD)} | sudo -S bash -lc {shell_quote(compose_command)}"
        exec_stream(client, sudo_compose)
    finally:
        client.close()


if __name__ == "__main__":
    main()
