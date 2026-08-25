import os
import sys
import time

import paramiko


HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ["LABSHIELD_SSH_PASSWORD"]
SMTP_USER = os.environ["LABSHIELD_SMTP_USER"]
SMTP_PASS = os.environ["LABSHIELD_SMTP_PASS"]
SMTP_FROM = os.environ.get("LABSHIELD_SMTP_FROM", f"Labshield <{SMTP_USER}>")


def shell_quote(value):
    return "'" + value.replace("'", "'\"'\"'") + "'"


def exec_checked(client, command, timeout=600):
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
        print(out, end="")
    if err:
        print(err, end="", file=sys.stderr)
    status = channel.recv_exit_status()
    if status != 0:
        raise RuntimeError(f"Remote command failed with exit code {status}")


def main():
    compose_body = """version: '3.8'

services:
  labshield-backend:
    build: .
    image: labshield-server:local
    container_name: labshield-server
    env_file:
      - .env
    ports:
      - "${PORT:-5000}:${PORT:-5000}"
    environment:
      - PORT=${PORT:-5000}
      - DATA_DIR=/app/data
      - SMTP_HOST=${SMTP_HOST}
      - SMTP_PORT=${SMTP_PORT}
      - SMTP_SECURE=${SMTP_SECURE}
      - SMTP_USER=${SMTP_USER}
      - SMTP_PASS=${SMTP_PASS}
      - SMTP_FROM=${SMTP_FROM}
      - OTP_SECRET=${OTP_SECRET}
    volumes:
      - ./data:/app/data
    restart: always
"""

    env_body = "\n".join([
        "PORT=5000",
        "SMTP_HOST=smtp.gmail.com",
        "SMTP_PORT=587",
        "SMTP_SECURE=false",
        f"SMTP_USER={SMTP_USER}",
        f"SMTP_PASS={SMTP_PASS}",
        f"SMTP_FROM={SMTP_FROM}",
        "OTP_SECRET=labshield-otp-secret-production-2026",
        "",
    ])

    command = f"""
set -e
cd /opt/labshield-server
cat > docker-compose.yml <<'EOF'
{compose_body}
EOF
cat > .env <<'EOF'
{env_body}
EOF
chmod 600 .env
docker compose up -d --build --remove-orphans
docker exec labshield-server sh -lc 'for key in SMTP_HOST SMTP_PORT SMTP_SECURE SMTP_USER SMTP_PASS SMTP_FROM OTP_SECRET; do value=$(printenv "$key" || true); if [ -n "$value" ]; then if [ "$key" = "SMTP_PASS" ]; then echo "$key=<redacted>"; else echo "$key=$value"; fi; else echo "$key=<unset>"; fi; done'
"""

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
        exec_checked(client, command, timeout=1200)
    finally:
        client.close()


if __name__ == "__main__":
    main()
