import os, paramiko

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

commands = [
    ("DOCKER", "docker ps --filter name=labshield --format '{{.Names}} {{.Status}} {{.Ports}}'"),
    ("TUNNEL_PROC", "ps aux | grep cloudflared | grep -v grep || echo NO_TUNNEL"),
    ("TUNNEL_LOG", "tail -n 15 /tmp/carlotab-tunnel.log 2>/dev/null || echo no-log"),
    ("SYNC_TIMER", "systemctl --user is-active labshield-sync.timer 2>/dev/null || echo no-timer"),
    ("LOCAL_API", "curl -fsS -I http://127.0.0.1:5000/ 2>/dev/null && echo SERVER_OK || echo SERVER_DOWN"),
    ("SYNC_SCRIPT", "ls -la ~/labshield-sync.sh 2>/dev/null; head -n 15 ~/labshield-sync.sh 2>/dev/null || echo no-script"),
]

for label, cmd in commands:
    print(f"\n=== {label} ===")
    i, o, e = c.exec_command(cmd, timeout=30)
    print(o.read().decode(errors="replace").strip()[:300])
    err = e.read().decode(errors="replace").strip()
    if err: print(f"ERR: {err[:200]}")

c.close()
