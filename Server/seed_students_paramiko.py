import math
import os
import random
import sys
import time

import paramiko

random.seed(2026)

HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ["LABSHIELD_SSH_PASSWORD"]

STUDENTS = [
    "ADIVA ZALIKA ALFARIANSYAH",
    "AL DIANSYAH NUGRAHA",
    "AMANDA FIORENZA AZZAHRA",
    "ANITA SOFIANA PUTRI",
    "AZAHRA DWI KURNIA",
    "DEWI MELANI",
    "EVAN SETIAWAN",
    "FADHIL SYAHLEVY",
    "FARREL CAESAR FITRANDA RAMADHAN",
    "FEBRA PHILIPS KURNIAWAN",
    "GENDIS RAHMA HAFIDZAH",
    "GINA NASWA MALIKA",
    "HASNA QONITHA IDHAM",
    "HAYFA KHALFANI FIANDIKA",
    # INTAN SYAWAL AZZAHRA already registered as IntanSyawal
    "ISMALIYAH",
    "KENAZ SAHIRA FEIVEL",
    "KHAIRUL AZZAM ARDIANSYAH",
    "LARASATI",
    "MUHAMAD FAJRI AL HABIBIE",
    "MUHAMMAD MAS'UD AYUBI",
    # NAFIS AHMAD already registered as nafis_ahmad
    # NAFISA LESTA FAJARINA already registered as Nafisa.Lesta
    "NAZMA AMELIA",
    "NAZWA CAHYANI WIJAYA",
    "PUTRI ANGGITA",
    "RAHEL ASIH AMANDA",
    "REZKI ALI RAMADANI",
    # RISDA SAFANURISMAYA already registered as Risda Safanurismaya
    "RIZKY RADISYA PUTRA",
    "SAFIRA RAMADHANI",
    "SHAFA NUR ADILA",
    "SITI HAFIZAH",
    "SITTA ASMA NADIA",
    "VIREL ADHNA APRILIO",
    "YASMINE KAMIL SALSABILA",
]


def username_from(name):
    parts = name.strip().split()
    first = parts[0].lower()
    last = parts[-1].lower()
    return f"{first}_{last}"


def random_score():
    return random.randint(75, 100)


def random_apd():
    correct = random.randint(3, 5)
    wrong = random.randint(0, 2)
    time_sec = round(random.uniform(8, 55), 1)
    return correct, wrong, time_sec


def random_quiz():
    correct = random.randint(15, 20)
    wrong = max(0, 20 - correct)
    question_times = []
    for i in range(1, 20):
        qid = f"Soal_{i}"
        time_sec = round(random.uniform(1.5, 30), 4)
        question_times.append({
            "questionID": qid,
            "timeTakenSeconds": time_sec,
            "isCorrect": random.choice([True, False])
        })
    total_time = round(sum(q["timeTakenSeconds"] for q in question_times), 2)
    return correct, wrong, question_times, total_time


def exec_checked(client, command, timeout=300):
    stdin, stdout, stderr = client.exec_command(command, timeout=timeout)
    channel = stdout.channel
    while not channel.exit_status_ready():
        if channel.recv_ready():
            sys.stdout.write(channel.recv(4096).decode(errors="replace"))
            sys.stdout.flush()
        if channel.recv_stderr_ready():
            sys.stderr.write(channel.recv_stderr(4096).decode(errors="replace"))
            sys.stderr.flush()
        time.sleep(0.2)
    out = stdout.read().decode(errors="replace")
    err = stderr.read().decode(errors="replace")
    if out:
        sys.stdout.write(out)
    if err:
        sys.stderr.write(err)
    return channel.recv_exit_status()


def main():
    # Build JSON payloads
    import json

    new_users = []
    new_scores = []

    for name in STUDENTS:
        username = username_from(name)
        password = f"labshield{random.randint(100, 999)}"

        new_users.append({
            "username": username,
            "password": password,
            "role": "student",
            "email": None,
        })

        apd_correct, apd_wrong, apd_time = random_apd()
        quiz_correct, quiz_wrong, question_times, quiz_time = random_quiz()
        standard = random_score()
        k3 = max(75, standard - random.randint(0, 15))

        new_scores.append({
            "studentName": username,
            "attemptNumber": 1,
            "apdTotalCorrect": apd_correct,
            "apdTotalWrong": apd_wrong,
            "apdTimeTakenSeconds": apd_time,
            "quizTotalCorrect": quiz_correct,
            "quizTotalWrong": quiz_wrong,
            "questionTimes": question_times,
            "finalScore": standard,
            "finalScoreStandard": standard,
            "finalScoreK3": k3,
            "timestamp": "2026-05-27T08:00:00.000Z",
        })

    # Also add scores for already registered students without scores
    for existing_name, existing_user in [
        ("NAFIS AHMAD", "nafis_ahmad"),
        ("NAFISA LESTA FAJARINA", "Nafisa.Lesta"),
        ("RISDA SAFANURISMAYA", "Risda Safanurismaya"),
    ]:
        apd_correct, apd_wrong, apd_time = random_apd()
        quiz_correct, quiz_wrong, question_times, quiz_time = random_quiz()
        standard = random_score()
        k3 = max(75, standard - random.randint(0, 15))
        new_scores.append({
            "studentName": existing_user,
            "attemptNumber": 1,
            "apdTotalCorrect": apd_correct,
            "apdTotalWrong": apd_wrong,
            "apdTimeTakenSeconds": apd_time,
            "quizTotalCorrect": quiz_correct,
            "quizTotalWrong": quiz_wrong,
            "questionTimes": question_times,
            "finalScore": standard,
            "finalScoreStandard": standard,
            "finalScoreK3": k3,
            "timestamp": "2026-05-27T08:00:00.000Z",
        })

    users_json = json.dumps(new_users, indent=2, ensure_ascii=False)
    scores_json = json.dumps(new_scores, indent=2, ensure_ascii=False)

    import base64

    users_b64 = base64.b64encode(users_json.encode()).decode()
    scores_b64 = base64.b64encode(scores_json.encode()).decode()

    command = f'''
set -e
echo '--- registering new students ---'
python3 -c "
import base64, json
path = '/opt/labshield-server/data/users.json'
with open(path, 'r') as f:
    content = f.read().strip()
data = json.loads(content) if content else []
new_users = json.loads(base64.b64decode('{users_b64}'))
existing = {{u['username'] for u in data}}
added = 0
for user in new_users:
    if user['username'] not in existing:
        data.append(user)
        added += 1
        existing.add(user['username'])
with open(path, 'w') as f:
    json.dump(data, f, indent=2)
print(f'Added {{added}} new students, total: {{len(data)}}')
"

echo '--- writing scores ---'
python3 -c "
import base64, json, os
path = '/opt/labshield-server/data/student_scores.json'
with open(path, 'r') as f:
    content = f.read().strip()
existing = json.loads(content) if content else []
new_scores = json.loads(base64.b64decode('{scores_b64}'))
existing.extend(new_scores)
with open(path, 'w') as f:
    json.dump(existing, f, indent=2)
print(f'Added {{len(new_scores)}} scores, total: {{len(existing)}}')
"

echo '--- verify ---'
cd /opt/labshield-server
docker compose restart labshield-server
sleep 3
curl -fsS http://127.0.0.1:5000/api/scores | python3 -c "
import json, sys
data = json.load(sys.stdin)
students = set(row.get('studentName','') for row in data)
scores = list(data)
print(f'Total score entries: {{len(scores)}}')
print(f'Unique students: {{len(students)}}')
min_score = min((row.get('finalScoreStandard', row.get('finalScore', 0)) for row in scores), default=0)
max_score = max((row.get('finalScoreStandard', row.get('finalScore', 0)) for row in scores), default=0)
print(f'Score range: {{min_score}} - {{max_score}}')
below = [row.get('studentName','?') for row in scores if row.get('finalScoreStandard', row.get('finalScore', 0)) < 75]
print(f'Below 75: {{below}}')
for row in scores:
    name = row.get('studentName', '')
    score = row.get('finalScoreStandard', row.get('finalScore', 0))
    print(f'  {{name}}: {{score}}')
"
'''

    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(
        HOST,
        username=USER,
        password=PASSWORD,
        timeout=20,
        auth_timeout=20,
        banner_timeout=20,
        look_for_keys=False,
        allow_agent=False,
    )
    try:
        code = exec_checked(client, command, timeout=600)
        if code != 0:
            raise RuntimeError(f"Remote script failed with exit code {code}")
    finally:
        client.close()


if __name__ == "__main__":
    main()
