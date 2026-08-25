import os, paramiko, time, urllib.request

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")

# Check from carlotab
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

i, o, e = c.exec_command("curl -sS -o /dev/null -w '%{http_code}' https://labshieldprotocol.my.id/ 2>/dev/null || echo fail", timeout=30)
code = o.read().decode(errors="replace").strip()
print(f"Public from carlotab: {code}")

i, o, e = c.exec_command("tail -n 3 /tmp/carlotab-tunnel2.log 2>/dev/null", timeout=30)
print(f"Tunnel log last lines: {o.read().decode(errors='replace')[:300]}")

i, o, e = c.exec_command("grep -c 'Registered' /tmp/carlotab-tunnel2.log 2>/dev/null || echo 0", timeout=30)
conns = o.read().decode(errors="replace").strip()
print(f"Registered connections: {conns}")

i, o, e = c.exec_command("grep 'Updated to new configuration' /tmp/carlotab-tunnel2.log 2>/dev/null", timeout=30)
cfg = o.read().decode(errors="replace").strip()
print(f"Remote config: {'YES' if cfg else 'NO - local only'}")

c.close()

# Check public directly
try:
    req = urllib.request.Request("https://labshieldprotocol.my.id/")
    resp = urllib.request.urlopen(req, timeout=15)
    print(f"\nPublic access: {resp.status} OK")
except urllib.request.HTTPError as e:
    print(f"\nPublic access: {e.code} {e.reason}")
except Exception as e:
    print(f"\nPublic access error: {str(e)[:80]}")
