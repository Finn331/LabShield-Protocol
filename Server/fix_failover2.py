import os, paramiko

TAB_PW = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
HOME_PW = os.environ.get("LABSHIELD_SSH_PASSWORD")

print("=== CARLOTAB TUNNEL IDENTITY ===")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=TAB_PW, timeout=20, look_for_keys=False, allow_agent=False)

for label, cmd in [
    ("CONFIG_YML", "cat /home/carlo/.cloudflared/config.yml"),
    ("CREDS_FILE", "cat /home/carlo/.cloudflared/f0d6194e-1425-41b2-a526-d97025e5a040.json 2>/dev/null | head -c 200"),
    ("CREDS_LINK", "ls -la /home/carlo/.cloudflared/creds.json"),
    ("HOME_DIR_CREDS", "ls -la /home/carlo/.cloudflared/ && echo SEP && head -c 200 /home/carlo/.cloudflared/f0d6194e-1425-41b2-a526-d97025e5a040.json"),
]:
    print(f"\n--- {label} ---")
    i, o, e = c.exec_command(cmd, timeout=30)
    print(o.read().decode(errors="replace").strip()[:500])

c.close()

print("\n\n=== HOME LAB TUNNEL IDENTITY ===")
try:
    h = paramiko.SSHClient()
    h.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    h.connect("100.69.10.5", username="carloserver", password=HOME_PW, timeout=10, look_for_keys=False, allow_agent=False)
    for label, cmd in [
        ("CONFIG_USER", "cat /home/carloserver/.cloudflared/config.yml"),
        ("CREDS_CONTENT", "printf '%s' 'aloganteng03.' | sudo -S head -c 200 /etc/cloudflared/f0d6194e-1425-41b2-a526-d97025e5a040.json 2>/dev/null"),
    ]:
        print(f"\n--- {label} ---")
        i, o, e = h.exec_command(cmd, timeout=30)
        print(o.read().decode(errors="replace").strip()[:500])
    h.close()
except Exception as ex:
    print(f"Home lab DOWN: {ex}")
