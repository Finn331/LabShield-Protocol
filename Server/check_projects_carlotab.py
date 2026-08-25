import os, paramiko

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

print("=== DOCKER ===")
i, o, e = c.exec_command("docker ps --format '{{.Names}}|{{.Status}}|{{.Ports}}'", timeout=30)
print(o.read().decode(errors="replace")[:500])

print("\n=== HOME DIRS ===")
i, o, e = c.exec_command("ls -la /home/carlo/ | grep -v '^total' | head -n 30", timeout=30)
print(o.read().decode(errors="replace")[:500])

print("\n=== PORTS ===")
for port in [3000, 9006, 9014, 9008]:
    i, o, e = c.exec_command(f"curl -sS -o /dev/null -w '%{{http_code}}' http://127.0.0.1:{port}/ 2>/dev/null || echo closed", timeout=15)
    print(f"  Port {port}: {o.read().decode(errors='replace').strip()}")

print("\n=== PROCESS PORTS ===")
i, o, e = c.exec_command("ss -tlnp | grep -E '3000|9006|9014|9008|5000'", timeout=30)
print(o.read().decode(errors="replace")[:500])

c.close()
