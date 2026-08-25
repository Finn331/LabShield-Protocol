import os, paramiko, time

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

# Kill existing tunnel
c.exec_command("pkill -f 'cloudflared tunnel' 2>/dev/null; sleep 2", timeout=30)

# Try to get token from credentials JSON
i, o, e = c.exec_command(
    "python3 -c \"import json; creds=json.load(open('/home/carlo/.cloudflared/creds.json')); print(creds.get('TunnelSecret','no-secret'))\" 2>/dev/null || echo no-python",
    timeout=30
)
secret = o.read().decode(errors="replace").strip()
print(f"Secret read: {'yes' if 'no' not in secret and len(secret) > 5 else 'no'}")

# Start tunnel with explicit credentials
cmd = (
    "nohup /home/carlo/.local/bin/cloudflared tunnel --credentials-file /home/carlo/.cloudflared/creds.json "
    "--no-autoupdate run f0d6194e-1425-41b2-a526-d97025e5a040 "
    "> /tmp/carlotab-tunnel2.log 2>&1 &"
)
i, o, e = c.exec_command(cmd, timeout=30)
time.sleep(8)

# Check status
for label, cmd in [
    ("PROCESS", "ps aux | grep cloudflared | grep -v grep || echo dead"),
    ("NEW_LOG", "head -n 15 /tmp/carlotab-tunnel2.log 2>/dev/null || echo no-log"),
    ("OLD_LOG", "tail -n 5 /tmp/carlotab-tunnel.log 2>/dev/null || echo no-log"),
]:
    i, o, e = c.exec_command(f"echo '=== {label} ===' && {cmd}", timeout=30)
    print(f"\n{label}:")
    print(o.read().decode(errors="replace")[:400])

# Check if it got config updates
i, o, e = c.exec_command("grep -c 'Updated to new configuration' /tmp/carlotab-tunnel2.log 2>/dev/null || echo 0", timeout=30)
updates = o.read().decode(errors="replace").strip()
print(f"\nConfig updates in new tunnel: {updates}")

c.close()
