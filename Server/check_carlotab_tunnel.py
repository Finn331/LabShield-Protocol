import os, time
import paramiko

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)
try:
    for cmd, label in [
        ("cat /home/carlo/.cloudflared/config.yml", "CONFIG"),
        ("head -c 200 /home/carlo/.cloudflared/f0d6194e-1425-41b2-a526-d97025e5a040.json 2>/dev/null || echo absent", "CREDS"),
        ("tail -n 30 /tmp/carlotab-tunnel.log 2>/dev/null || echo no-log", "LOG"),
        ("docker ps --filter name=labshield --format '{{.Names}} {{.Status}}' 2>/dev/null", "DOCKER"),
    ]:
        print(f"=== {label} ===")
        i, o, e = client.exec_command(cmd, timeout=30)
        print(o.read().decode(errors="replace")[:500])
finally:
    client.close()
