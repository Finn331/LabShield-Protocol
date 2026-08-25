import os
import sys
import time

import paramiko


HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ["LABSHIELD_SSH_PASSWORD"]


def exec_checked(client, command, timeout=180):
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
    command = r'''
set -e
echo '--- container SMTP env presence ---'
docker exec labshield-server sh -lc 'for key in SMTP_HOST SMTP_PORT SMTP_SECURE SMTP_USER SMTP_PASS SMTP_FROM OTP_SECRET; do value=$(printenv "$key" || true); if [ -n "$value" ]; then if [ "$key" = "SMTP_PASS" ]; then echo "$key=<redacted>"; else echo "$key=$value"; fi; else echo "$key=<unset>"; fi; done'
echo '--- origin OTP request ---'
docker exec -i labshield-server node - <<'NODE'
const http = require('http');
const payload = JSON.stringify({ email: `debug-otp-${Date.now()}@example.com` });
const request = http.request({
  hostname: '127.0.0.1',
  port: 5000,
  path: '/api/register/request-otp',
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Content-Length': Buffer.byteLength(payload)
  }
}, (response) => {
  let body = '';
  response.setEncoding('utf8');
  response.on('data', (chunk) => body += chunk);
  response.on('end', () => {
    console.log(`HTTP ${response.statusCode}`);
    console.log(body);
    process.exit(response.statusCode >= 200 && response.statusCode < 500 ? 0 : 1);
  });
});
request.on('error', (error) => {
  console.error(error.message);
  process.exit(1);
});
request.write(payload);
request.end();
NODE
echo '--- recent OTP logs ---'
docker logs --tail 40 labshield-server
'''

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
