import os, json, paramiko

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

i, o, e = c.exec_command("docker exec labshield-server wc -c /app/data/student_scores.json 2>/dev/null || echo missing", timeout=30)
print(f"File size: {o.read().decode(errors='replace').strip()}")

i, o, e = c.exec_command("docker exec labshield-server python3 -c 'import json;d=json.load(open(\"/app/data/student_scores.json\"));print(f\"records={len(d)}\")' 2>/dev/null || echo no-data", timeout=30)
print(f"Records: {o.read().decode(errors='replace').strip()}")

# If empty, seed from CSV data embedded in this script
i, o, e = c.exec_command("docker exec labshield-server python3 -c 'import json;d=json.load(open(\"/app/data/student_scores.json\"));print(d[:1] if d else \"empty\")' 2>/dev/null || echo empty", timeout=30)
first = o.read().decode(errors="replace").strip()
print(f"First record: {first[:100] if first != 'empty' else 'none'}")

c.close()
