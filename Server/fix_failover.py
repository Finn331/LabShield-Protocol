import os, paramiko

TAB_PW = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
HOME_PW = os.environ.get("LABSHIELD_SSH_PASSWORD")

print("=== 1. Check carlotab state ===")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=TAB_PW, timeout=20, look_for_keys=False, allow_agent=False)

for label, cmd in [
    ("DOCKER", "docker ps --filter name=labshield --format '{{.Names}} {{.Status}}'"),
    ("SERVER", "curl -sS -I http://127.0.0.1:5000/ 2>/dev/null | head -n 1 || echo DOWN"),
    ("TUNNEL_PID", "pgrep -f 'cloudflared.*config.yml' | head -n 1 || echo dead"),
    ("CONFIG_UPDATES", "grep -c 'Updated to new configuration' /tmp/carlotab-tunnel.log 2>/dev/null || echo 0"),
    ("REGISTERED", "grep -c 'Registered tunnel connection' /tmp/carlotab-tunnel.log 2>/dev/null || echo 0"),
]:
    i, o, e = c.exec_command(cmd, timeout=30)
    out = o.read().decode(errors="replace").strip()
    print(f"  {label}: {out[:100]}")

# If tunnel is not running, restart it
i, o, e = c.exec_command("pgrep -f 'cloudflared.*config.yml' 2>/dev/null || echo dead", timeout=30)
tunnel_alive = o.read().decode(errors="replace").strip()
if tunnel_alive == "dead":
    print("\n=== 2. Tunnel dead on carlotab! Restarting... ===")
    c.exec_command("nohup /home/carlo/.local/bin/cloudflared tunnel --config /home/carlo/.cloudflared/config.yml run > /tmp/carlotab-tunnel.log 2>&1 &", timeout=30)
    import time; time.sleep(5)
    i, o, e = c.exec_command("pgrep -f 'cloudflared.*config.yml' | head -n 1 || echo still-dead", timeout=30)
    print(f"  After restart: {o.read().decode(errors='replace').strip()}")
else:
    print("\n=== 2. Tunnel is alive ===")

# Check if carlotab can reach the public domain
i, o, e = c.exec_command("curl -sS -o /dev/null -w '%{http_code}' https://labshieldprotocol.my.id/ 2>/dev/null || echo fail", timeout=30)
public_status = o.read().decode(errors="replace").strip()
print(f"\n=== 3. Public access from carlotab: {public_status} ===")

if public_status != "200":
    print("\n=== 4. Checking config mismatch ===")
    i, o, e = c.exec_command("grep 'Updated to new configuration' /tmp/carlotab-tunnel.log 2>/dev/null || echo none", timeout=30)
    config_info = o.read().decode(errors="replace").strip()
    print(f"  Config updates: {config_info[:300]}")

    # The issue is likely that Cloudflare remote config doesn't include labshieldprotocol
    # Let's check the current config
    i, o, e = c.exec_command("grep -A 1 'Updated to new configuration' /tmp/carlotab-tunnel.log 2>/dev/null | tail -n 2", timeout=30)
    remote_cfg = o.read().decode(errors="replace").strip()
    if "labshieldprotocol" not in remote_cfg:
        print("  REMOTE CONFIG IS MISSING labshieldprotocol.my.id!")
        print("  Adding to tunnel public hostname required in Cloudflare Dashboard")
        print("  -> Zero Trust > Networks > Tunnels > Portfolio > Public Hostname")
        print("  -> Add: labshieldprotocol.my.id -> localhost:5000")

print("\n=== 5. Checking home lab (likely down) ===")
try:
    h = paramiko.SSHClient()
    h.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    h.connect("100.69.10.5", username="carloserver", password=HOME_PW, timeout=10, look_for_keys=False, allow_agent=False)
    i, o, e = h.exec_command("echo alive", timeout=10)
    print(f"  Home lab: {o.read().decode(errors='replace').strip()}")
    h.close()
except Exception as e:
    print(f"  Home lab: DOWN ({str(e)[:50]})")

c.close()
print("\n=== DIAGNOSTIC COMPLETE ===")
