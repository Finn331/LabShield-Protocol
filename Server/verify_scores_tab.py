import os, paramiko

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

i, o, e = c.exec_command("docker exec labshield-server node -e 'const s=require(\"fs\").statSync(\"/app/data/student_scores.json\"); console.log(\"size=\"+s.size)'", timeout=30)
print(o.read().decode(errors="replace").strip())

i, o, e = c.exec_command("curl -fsS http://127.0.0.1:5000/api/scores | python3 -c $'import sys,json;d=json.load(sys.stdin);print(\"records=\"+str(len(d)))'", timeout=30)
print(o.read().decode(errors="replace").strip())
c.close()
