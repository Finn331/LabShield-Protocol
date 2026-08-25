import os
import posixpath
import socket
import subprocess
import sys
import tarfile
import tempfile
import time
from pathlib import Path

import paramiko


HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ.get("LABSHIELD_SSH_PASSWORD")
REMOTE_DIR = os.environ.get("LABSHIELD_REMOTE_DIR", "/opt/labshield-server")
DOMAIN = os.environ.get("LABSHIELD_DOMAIN", "labshieldprotocol.my.id")
SKIP_SSL = os.environ.get("LABSHIELD_SKIP_SSL", "true").lower() in {"1", "true", "yes"}
EXCLUDE_VIDEOS = os.environ.get("LABSHIELD_EXCLUDE_VIDEOS", "false").lower() in {"1", "true", "yes"}


def run_local(command):
    result = subprocess.run(command, shell=True, text=True, capture_output=True)
    if result.returncode != 0:
        raise RuntimeError(f"Local command failed: {command}\n{result.stderr.strip()}")
    return result.stdout


def make_archive(server_dir):
    archive_path = Path(tempfile.gettempdir()) / "labshield-paramiko-deploy.tar.gz"
    if archive_path.exists():
        archive_path.unlink()

    with tarfile.open(archive_path, "w:gz") as archive:
        for path in server_dir.rglob("*"):
            if not path.is_file():
                continue

            rel = path.relative_to(server_dir.parent)
            rel_text = rel.as_posix()
            if rel_text.startswith("Server/node_modules/") or rel_text == "Server/node_modules":
                continue
            if rel_text.startswith("Server/data/") or rel_text == "Server/data":
                continue
            if EXCLUDE_VIDEOS and rel_text.startswith("Server/public/assets/videos/"):
                continue
            archive.add(path, arcname=rel_text)

    return archive_path


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


def sudo_command(command):
    escaped_password = PASSWORD.replace("'", "'\\''")
    escaped_command = command.replace("'", "'\\''")
    return f"printf '%s\\n' '{escaped_password}' | sudo -S bash -lc '{escaped_command}'"


def upload(client, local_path, remote_path):
    print(f"==> Uploading {local_path} to {remote_path}")
    sftp = client.open_sftp()
    try:
        total = Path(local_path).stat().st_size
        last_percent = -1

        def progress(sent, size):
            nonlocal last_percent
            denominator = size or total or 1
            percent = int((sent / denominator) * 100)
            if percent != last_percent and (percent % 5 == 0 or percent == 100):
                print(f"   upload progress: {percent}% ({sent}/{denominator} bytes)")
                last_percent = percent

        sftp.put(str(local_path), remote_path, callback=progress)
    finally:
        sftp.close()


def remote_restore_script():
    return f'''set -e
export DEBIAN_FRONTEND=noninteractive

REMOTE_DIR="{REMOTE_DIR}"
DOMAIN="{DOMAIN}"
ARCHIVE="/tmp/labshield-paramiko-deploy.tar.gz"

echo "==> Installing base packages"
apt-get update
apt-get install -y ca-certificates curl gnupg lsb-release tar

if ! command -v docker >/dev/null 2>&1; then
    echo "==> Installing Docker"
    curl -fsSL https://get.docker.com | sh
fi

if ! docker compose version >/dev/null 2>&1; then
    echo "==> Installing Docker Compose plugin"
    apt-get install -y docker-compose-plugin
fi

if ! command -v cloudflared >/dev/null 2>&1; then
    echo "==> cloudflared is not installed yet. Run configure_cloudflare_tunnel_paramiko.py with LABSHIELD_CLOUDFLARE_TUNNEL_TOKEN after deploy."
fi

echo "==> Deploying application files"
mkdir -p "$REMOTE_DIR"
tar -xzf "$ARCHIVE" -C "$REMOTE_DIR" --strip-components=1
mkdir -p "$REMOTE_DIR/data"

if [ -f "$REMOTE_DIR/users.json" ] && [ ! -f "$REMOTE_DIR/data/users.json" ]; then
    cp "$REMOTE_DIR/users.json" "$REMOTE_DIR/data/users.json"
fi

if [ -f "$REMOTE_DIR/student_scores.json" ] && [ ! -f "$REMOTE_DIR/data/student_scores.json" ]; then
    cp "$REMOTE_DIR/student_scores.json" "$REMOTE_DIR/data/student_scores.json"
fi

chmod +x "$REMOTE_DIR/start.sh" "$REMOTE_DIR/restart.sh" 2>/dev/null || true

echo "==> Starting LabShield Docker service"
cd "$REMOTE_DIR"
docker compose up -d --build --remove-orphans

echo "==> Health checks"
curl -fsS http://127.0.0.1:5000/register.html >/dev/null
curl -fsS http://127.0.0.1:5000/api/scores >/dev/null
docker compose ps

echo "==> Restore complete"
'''


def main():
    server_dir = Path(__file__).resolve().parent
    for name in ["server.js", "package.json", "docker-compose.yml", "Dockerfile", "public"]:
        if not (server_dir / name).exists():
            raise RuntimeError(f"Missing required server asset: {server_dir / name}")

    archive_path = make_archive(server_dir)
    print(f"Archive created: {archive_path}")
    if EXCLUDE_VIDEOS:
        print("Video assets excluded for faster core service restore.")

    try:
        socket.create_connection((HOST, 22), timeout=15).close()
    except OSError as exc:
        raise RuntimeError(f"Cannot reach SSH port on {HOST}: {exc}") from exc

    client = connect()
    try:
        exec_checked(client, "mkdir -p /tmp")
        upload(client, archive_path, "/tmp/labshield-paramiko-deploy.tar.gz")

        script = remote_restore_script()
        remote_script = "/tmp/labshield-paramiko-restore.sh"
        sftp = client.open_sftp()
        try:
            with sftp.file(remote_script, "w") as file_obj:
                file_obj.write(script)
        finally:
            sftp.close()

        exec_checked(client, sudo_command(f"bash {remote_script}"), timeout=1800, label="run remote restore with sudo")
    finally:
        client.close()

    print("==> Local HTTP verification")
    try:
        run_local(f"curl -fsS --connect-timeout 10 http://{HOST}:5000/api/scores")
        print(f"Backend reachable: http://{HOST}:5000/api/scores")
    except Exception as exc:
        print(f"WARNING: local backend verification failed: {exc}")


if __name__ == "__main__":
    main()
