import os
import posixpath
import sys
import tarfile
import tempfile
import time
from pathlib import Path

import paramiko


HOST = "192.168.100.142"
USER = "carloserver"
PASSWORD = os.environ["LABSHIELD_SSH_PASSWORD"]
REMOTE_DIR = "/opt/labshield-server"
REMOTE_ARCHIVE = "/tmp/labshield-core.tar.gz"
DOMAIN = "labshieldprotocol.my.id"


def make_core_archive():
    server_dir = Path(__file__).resolve().parent
    archive_path = Path(tempfile.gettempdir()) / "labshield-core.tar.gz"
    if archive_path.exists():
        archive_path.unlink()

    with tarfile.open(archive_path, "w:gz") as archive:
        for path in server_dir.rglob("*"):
            rel = path.relative_to(server_dir.parent).as_posix()
            if rel.startswith("Server/node_modules/") or rel == "Server/node_modules":
                continue
            if rel.startswith("Server/data/") or rel == "Server/data":
                continue
            if rel == "Server/.env":
                continue
            if rel.startswith("Server/public/assets/videos/"):
                continue
            archive.add(path, arcname=rel)
    print(f"archive={archive_path} size={archive_path.stat().st_size}", flush=True)
    return archive_path


def exec_checked(client, command, timeout=600):
    print(f"$ {command.splitlines()[0][:120]}", flush=True)
    stdin, stdout, stderr = client.exec_command(command, timeout=timeout)
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
        print(out, end="", flush=True)
    if err:
        print(err, end="", file=sys.stderr, flush=True)
    code = channel.recv_exit_status()
    if code != 0:
        raise RuntimeError(f"remote command failed ({code}): {command}")


def main():
    archive = make_core_archive()

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
        exec_checked(client, "mkdir -p /tmp")
        print(f"uploading {archive} -> {REMOTE_ARCHIVE}", flush=True)
        sftp = client.open_sftp()
        try:
            total = archive.stat().st_size
            last = -1

            def progress(sent, size):
                nonlocal last
                pct = int(sent * 100 / (size or total or 1))
                if pct != last and (pct % 10 == 0 or pct == 100):
                    print(f"upload {pct}%", flush=True)
                    last = pct

            sftp.put(str(archive), REMOTE_ARCHIVE, callback=progress)
        finally:
            sftp.close()

        remote = f'''
set -e
export DEBIAN_FRONTEND=noninteractive
mkdir -p "{REMOTE_DIR}"
tar -xzf "{REMOTE_ARCHIVE}" -C "{REMOTE_DIR}" --strip-components=1
mkdir -p "{REMOTE_DIR}/data"
if [ -f "{REMOTE_DIR}/users.json" ] && [ ! -f "{REMOTE_DIR}/data/users.json" ]; then cp "{REMOTE_DIR}/users.json" "{REMOTE_DIR}/data/users.json"; fi
if [ -f "{REMOTE_DIR}/student_scores.json" ] && [ ! -f "{REMOTE_DIR}/data/student_scores.json" ]; then cp "{REMOTE_DIR}/student_scores.json" "{REMOTE_DIR}/data/student_scores.json"; fi
cd "{REMOTE_DIR}"
docker compose up -d --build --remove-orphans
cat > /etc/nginx/sites-available/labshield <<'EOF'
server {{
    listen 80;
    server_name {DOMAIN};
    client_max_body_size 50m;
    location / {{
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }}
}}
EOF
ln -sf /etc/nginx/sites-available/labshield /etc/nginx/sites-enabled/labshield
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl restart nginx
curl -fsS http://127.0.0.1:5000/api/scores >/dev/null
curl -fsS http://127.0.0.1:5000/register.html >/dev/null
docker ps --format 'table {{{{.Names}}}}\t{{{{.Status}}}}\t{{{{.Ports}}}}'
'''
        exec_checked(client, remote, timeout=1200)
    finally:
        client.close()


if __name__ == "__main__":
    main()
