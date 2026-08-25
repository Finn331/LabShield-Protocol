import os, paramiko

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

for label, cmd in [
    ("NEW_TUNNEL_CONNS", "grep -c 'Registered tunnel connection' /tmp/carlotab-tunnel2.log 2>/dev/null || echo 0"),
    ("NEW_TUNNEL_CONFIG", "grep 'Updated to new configuration' /tmp/carlotab-tunnel2.log 2>/dev/null | head -n 1 || echo no-remote-config"),
    ("NEW_TUNNEL_LINES", "cat /tmp/carlotab-tunnel2.log 2>/dev/null | wc -l"),
    ("OLD_TUNNEL_PID", "ps aux | grep cloudflared | grep config.yml | grep -v grep | awk '{print $2}' || echo dead"),
]:
    i, o, e = c.exec_command(cmd, timeout=30)
    out = o.read().decode(errors="replace").strip()
    print(f"{label}: {out[:200]}")

# Finally check if public domain works
import urllib.request
try:
    resp = urllib.request.urlopen("https://labshieldprotocol.my.id/", timeout=15)
    print(f"\nPUBLIC DOMAIN: {resp.status} OK")
except Exception as ex:
    print(f"\nPUBLIC DOMAIN: FAIL ({str(ex)[:80]})")

c.close()
