import os, paramiko, time

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
cfg = """credentials-file: /home/carlo/.cloudflared/creds.json
tunnel: f0d6194e-1425-41b2-a526-d97025e5a040
ingress:
  - hostname: makerslab.engineer
    service: http://localhost:9014
  - hostname: www.makerslab.engineer
    service: http://localhost:9014
  - hostname: carlomuzaqi.my.id
    service: http://localhost:9006
  - hostname: www.carlomuzaqi.my.id
    service: http://localhost:9006
  - hostname: toolora.cloud
    service: http://localhost:9008
  - hostname: www.toolora.cloud
    service: http://localhost:9008
  - hostname: labshieldprotocol.my.id
    service: http://localhost:5000
  - hostname: www.labshieldprotocol.my.id
    service: http://localhost:5000
  - hostname: smarthydroponic.my.id
    service: http://localhost:3000
  - hostname: www.smarthydroponic.my.id
    service: http://localhost:3000
  - service: http_status:404
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

# Write config
i, o, e = c.exec_command(f"cat > /home/carlo/.cloudflared/config.yml <<'EOF'\n{cfg}\nEOF", timeout=30)
print("Config write: " + ("OK" if not o.read().decode(errors="replace").strip() else ""))

# Kill old tunnel, start new one
i, o, e = c.exec_command("pkill -f 'cloudflared tunnel' 2>/dev/null; echo KILLED", timeout=30)
print(o.read().decode(errors="replace").strip())
time.sleep(2)

i, o, e = c.exec_command(
    "nohup /home/carlo/.local/bin/cloudflared tunnel "
    "--credentials-file /home/carlo/.cloudflared/creds.json "
    "--no-autoupdate run f0d6194e-1425-41b2-a526-d97025e5a040 "
    "> /tmp/carlotab-tunnel.log 2>&1 & sleep 5; "
    "pgrep -f cloudflared | head -n 1 || echo dead",
    timeout=30,
)
print(f"Tunnel PID: {o.read().decode(errors='replace').strip()}")

# Verify
time.sleep(3)
for domain, port in [
    ("carlomuzaqi.my.id", 9006), ("makerslab.engineer", 9014),
    ("toolora.cloud", 9008), ("smarthydroponic.my.id", 3000),
    ("labshieldprotocol.my.id", 5000),
]:
    i, o, e = c.exec_command(
        f"curl -sS -H 'Host: {domain}' http://127.0.0.1:{port}/ -o /dev/null -w '%{{http_code}}' 2>/dev/null || echo fail",
        timeout=15,
    )
    print(f"  {domain}: port={port}, code={o.read().decode(errors='replace').strip()}")

c.close()
print("\nDone - kunjungi https://carlomuzaqi.my.id/ dll")
