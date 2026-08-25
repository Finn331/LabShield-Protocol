import paramiko, os

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

def run(cmd, t=30):
    i, o, e = c.exec_command(cmd, timeout=t)
    return o.read().decode(errors="replace")

print("=== DOCKER ===")
print(run("docker ps --filter name=labshield --format '{{.Names}} {{.Status}} {{.Ports}}'"))

print("=== API ===")
print(run("curl -fsS http://127.0.0.1:5000/api/angket/status?username=testing_siswa"))

print("=== TUNNEL ===")
print(run("ps aux | grep cloudflared | grep -v grep | head -n 2"))

print("=== SYNC TIMER ===")
print(run("systemctl --user is-active labshield-sync.timer"))

print("=== SSH KEY ===")
print(run("ssh -o StrictHostKeyChecking=no -o ConnectTimeout=5 carloserver@100.69.10.5 'echo connected' 2>/dev/null || echo FAIL"))

c.close()
