import os
import sys
import time

import paramiko


HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ["LABSHIELD_SSH_PASSWORD"]
DOMAIN = os.environ.get("LABSHIELD_DOMAIN", "labshieldprotocol.my.id")


def exec_checked(client, command, timeout=300):
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
    nginx_conf = f"""
server {{
    listen 80;
    server_name {DOMAIN};
    return 301 https://$host$request_uri;
}}

server {{
    listen 443 ssl;
    server_name {DOMAIN};

    ssl_certificate /etc/letsencrypt/live/{DOMAIN}/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/{DOMAIN}/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

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
""".strip()

    command = f"""
set -e
cat > /etc/nginx/sites-available/labshield <<'EOF'
{nginx_conf}
EOF
ln -sf /etc/nginx/sites-available/labshield /etc/nginx/sites-enabled/labshield
nginx -t
systemctl reload nginx
curl -fsS -I http://127.0.0.1:5000/learning-media.html
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
        exec_checked(client, command)
    finally:
        client.close()


if __name__ == "__main__":
    main()
