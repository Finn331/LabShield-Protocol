import os, paramiko

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

for label, port in [("portfolio", 9006), ("makerslab", 9014), ("toolora", 9008), ("hydroponic", 3000), ("labshield", 5000)]:
    i, o, e = c.exec_command(f"curl -sS -o /dev/null -w '%{{http_code}}' http://127.0.0.1:{port}/ 2>/dev/null || echo fail", timeout=15)
    print(f"{label} (:{port}): {o.read().decode(errors='replace').strip()}")

c.close()
