import os
import sys
import time

import paramiko


HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ["LABSHIELD_SSH_PASSWORD"]


def exec_checked(client, command, timeout=120):
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
echo '--- database files ---'
ls -lah /opt/labshield-server/data/users.json /opt/labshield-server/data/student_scores.json
echo '--- users sanitized ---'
docker exec -i labshield-server node - <<'NODE'
const fs = require('fs');
const users = JSON.parse(fs.readFileSync('/app/data/users.json', 'utf8'));
console.log(JSON.stringify(users.map((u) => ({
  username: u.username,
  email: u.email || null,
  role: u.role,
  emailVerified: Boolean(u.emailVerified),
  createdAt: u.createdAt || null
})), null, 2));
NODE
echo '--- scores summary ---'
docker exec -i labshield-server node - <<'NODE'
const fs = require('fs');
const scores = JSON.parse(fs.readFileSync('/app/data/student_scores.json', 'utf8'));
const byStudent = new Map();
for (const score of scores) {
  const name = String(score.studentName || '').trim() || '(empty)';
  byStudent.set(name, (byStudent.get(name) || 0) + 1);
}
console.log(JSON.stringify({
  count: scores.length,
  students: Array.from(byStudent.entries()).map(([studentName, attempts]) => ({ studentName, attempts }))
}, null, 2));
NODE
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
