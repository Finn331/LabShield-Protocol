import os

import paramiko


host = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
user = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
password = os.environ["LABSHIELD_SSH_PASSWORD"]

client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect(
    hostname=host,
    username=user,
    password=password,
    timeout=20,
    auth_timeout=20,
    banner_timeout=20,
    look_for_keys=False,
    allow_agent=False,
)

command = """
hostname
uptime
command -v docker || true
command -v nginx || true
command -v certbot || true
ls -lah /tmp/labshield-paramiko-deploy.tar.gz 2>/dev/null || true
ls -lah /opt/labshield-server 2>/dev/null || true
docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}' 2>/dev/null || true
"""

stdin, stdout, stderr = client.exec_command(command, timeout=60)
print(stdout.read().decode(errors="replace"))
err = stderr.read().decode(errors="replace")
if err:
    print(err)
client.close()
