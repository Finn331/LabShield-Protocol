import os
import sys
import time

import paramiko


host = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
user = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
password = os.environ["LABSHIELD_SSH_PASSWORD"]
command = os.environ["LABSHIELD_REMOTE_COMMAND"]
timeout = int(os.environ.get("LABSHIELD_REMOTE_TIMEOUT", "900"))

client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect(
    host,
    username=user,
    password=password,
    timeout=20,
    auth_timeout=20,
    banner_timeout=20,
    look_for_keys=False,
    allow_agent=False,
)

try:
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
    code = channel.recv_exit_status()
finally:
    client.close()

sys.exit(code)
