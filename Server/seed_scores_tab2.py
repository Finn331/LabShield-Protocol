import os, json, paramiko

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")

students_scores = [
    {"name":"amanda_azzahra",a:5,b:0,c:17,d:3,at:47.1,qt:313.405,ts:"2026-05-26T04:41:18.857Z"},
    {"name":"shafa_adila",a:5,b:0,c:17,d:3,at:37.3,qt:358.6664,ts:"2026-05-26T05:00:22.286Z"},
    {"name":"gendis_hafidzah",a:5,b:1,c:17,d:3,at:43.7,qt:257.4877,ts:"2026-05-26T04:46:34.286Z"},
]

# Just write 3 records as a test
data = []
for r in students_scores:
    total_quiz = r["c"] + r["d"]
    qt = r["qt"]
    qtimes = [{"questionID":f"Q{i+1}","timeTakenSeconds":qt/total_quiz if total_quiz>0 else 0,"isCorrect":i<r["c"]} for i in range(total_quiz)]
    data.append({
        "studentName": r["name"], "attemptNumber": 1,
        "apdTotalCorrect": r["a"], "apdTotalWrong": r["b"],
        "apdTimeTakenSeconds": r["at"],
        "quizTotalCorrect": r["c"], "quizTotalWrong": r["d"],
        "questionTimes": qtimes, "quizTimeTakenSeconds": qt,
        "finalScore": 88, "finalScoreStandard": 88, "finalScoreK3": 94,
        "timestamp": r["ts"]
    })

data_json = json.dumps(data, indent=2)
print(f"Data: {len(data_json)} bytes")

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

transport = c.get_transport()
channel = transport.open_session()
channel.exec_command("docker exec -i labshield-server sh -c 'cat > /app/data/student_scores.json'")
channel.send(data_json.encode())
channel.shutdown_write()
import time
time.sleep(2)

out = channel.recv(4096).decode(errors="replace")
err = channel.recv_stderr(4096).decode(errors="replace")
if out: print(f"OUT: {out[:100]}")
if err: print(f"ERR: {err[:100]}")

i, o, e = c.exec_command("curl -fsS http://127.0.0.1:5000/api/scores | python3 -c $'import sys,json;d=json.load(sys.stdin);print(\"records=\"+str(len(d)))'", timeout=30)
print(o.read().decode(errors="replace").strip())
c.close()
