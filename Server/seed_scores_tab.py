import os, json, paramiko

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")

# Same CSV data that was used to seed home lab earlier
students_scores = [
    ["amanda_azzahra",1,5,0,17,3,47.1,313.405,88,94,"2026-05-26T04:41:18.857Z"],
    ["shafa_adila",1,5,0,17,3,37.3,358.6664,88,94,"2026-05-26T05:00:22.286Z"],
    ["gendis_hafidzah",1,5,1,17,3,43.7,257.4877,85,79,"2026-05-26T04:46:34.286Z"],
    ["nafis_ahmad",1,5,0,15,5,8.2,362.1252,80,90,"2026-05-26T04:53:48.000Z"],
    ["Risda Safanurismaya",1,5,2,16,4,45.3,267.8738,78,65,"2026-05-26T04:58:24.000Z"],
    ["hasna_idham",1,4,1,17,3,36.5,316.4381,84,77,"2026-05-26T04:47:53.143Z"],
    ["kenaz_feivel",1,3,2,20,0,50.1,307.2268,92,66,"2026-05-26T04:50:30.857Z"],
    ["virel_aprilio",1,3,1,18,2,47.2,307.6157,88,76,"2026-05-26T05:02:20.571Z"],
    ["siti_hafizah",1,5,1,19,1,40.1,325.9324,92,83,"2026-05-26T05:01:01.714Z"],
    ["larasati_larasati",1,5,2,19,1,15.8,293.008,89,71,"2026-05-26T04:51:49.714Z"],
    ["safira_ramadhani",1,3,1,20,0,17,254.7493,96,80,"2026-05-26T04:59:42.857Z"],
    ["rahel_amanda",1,4,0,15,5,28.1,267.1804,79,90,"2026-05-26T04:57:05.143Z"],
    ["farrel_ramadhan",1,3,2,17,3,16.1,290.5462,80,60,"2026-05-26T04:45:15.429Z"],
    ["khairul_ardiansyah",1,4,0,17,3,15.9,294.4556,88,94,"2026-05-26T04:51:10.286Z"],
    ["al_nugraha",1,5,1,19,1,47.7,350.737,92,83,"2026-05-26T04:40:39.429Z"],
    ["rizky_putra",1,5,0,19,1,12.5,324.3351,96,98,"2026-05-26T04:59:03.429Z"],
    ["adiva_alfariansyah",1,4,2,20,0,52.7,334.3429,92,70,"2026-05-26T04:40:00.000Z"],
    ["ismaliyah_ismaliyah",1,3,0,19,1,12.4,323.6528,96,98,"2026-05-26T04:49:51.429Z"],
    ["Nafisa.Lesta",1,3,1,15,5,34.2,322.1035,75,70,"2026-05-26T04:54:27.429Z"],
    ["muhammad_ayubi",1,3,0,18,2,8.1,278.4884,91,96,"2026-05-26T04:53:08.571Z"],
    ["rezki_ramadani",1,5,1,20,0,40.8,298.6329,96,85,"2026-05-26T04:57:44.571Z"],
    ["nazma_amelia",1,5,0,17,3,12.5,304.0361,88,94,"2026-05-26T04:55:06.857Z"],
    ["azahra_kurnia",1,5,2,16,4,49.3,301.45,78,65,"2026-05-26T04:42:37.714Z"],
    ["IntanSyawal",1,4,2,19,0,49.29134,201.899,92,70,"2026-05-26T04:49:12.000Z"],
    ["anita_putri",1,3,0,19,1,44.8,229.8529,96,98,"2026-05-26T04:41:58.286Z"],
    ["fadhil_syahlevy",1,3,0,18,2,21,312.418,91,96,"2026-05-26T04:44:36.000Z"],
    ["evan_setiawan",1,4,2,17,3,40,253.5699,81,64,"2026-05-26T04:43:56.571Z"],
    ["gina_malika",1,5,2,18,2,27.5,351.1707,85,69,"2026-05-26T04:47:13.714Z"],
    ["febra_kurniawan",1,5,2,20,0,49.5,261.7508,93,73,"2026-05-26T04:45:54.857Z"],
    ["sitta_nadia",1,5,1,18,2,24.5,317.5126,88,81,"2026-05-26T05:01:41.143Z"],
    ["yasmine_salsabila",1,5,0,15,5,11.3,252.1195,80,90,"2026-05-26T05:03:00.000Z"],
    ["nazwa_wijaya",1,3,1,16,4,39.5,296.3675,79,72,"2026-05-26T04:55:46.286Z"],
    ["dewi_melani",1,4,2,19,1,20.6,237.273,88,68,"2026-05-26T04:43:17.143Z"],
    ["hayfa_fiandika",1,4,2,19,1,31,302.5821,88,68,"2026-05-26T04:48:32.571Z"],
    ["putri_anggita",1,4,0,17,3,29.5,277.2414,88,94,"2026-05-26T04:56:25.714Z"],
    ["muhamad_habibie",1,4,2,18,2,38.4,329.7143,85,66,"2026-05-26T04:52:29.143Z"],
]

def compute_standard(apd_c, apd_w, quiz_c, quiz_w):
    t = apd_c + quiz_c + apd_w + quiz_w
    return int(round((apd_c + quiz_c) / t * 100)) if t > 0 else 0

def compute_k3(apd_c, apd_w, quiz_c, quiz_w):
    apd = apd_c + apd_w
    quiz = quiz_c + quiz_w
    apd_acc = (apd_c / apd * 100) if apd > 0 else 0
    quiz_acc = (quiz_c / quiz * 100) if quiz > 0 else 0
    score = (apd_acc * 0.6) + (quiz_acc * 0.4) - min(20, apd_w * 5)
    return max(0, min(100, int(round(score))))

rows = []
for r in students_scores:
    name = r[0]; attempt = r[1]; apd_c = r[2]; apd_w = r[3]; quiz_c = r[4]; quiz_w = r[5]
    apd_time = r[6]; quiz_time = r[7]; ts = r[10]
    total_quiz = quiz_c + quiz_w
    question_times = []
    for i in range(total_quiz):
        question_times.append({
            "questionID": f"Q{i+1}",
            "timeTakenSeconds": quiz_time / total_quiz if total_quiz > 0 else 0,
            "isCorrect": i < quiz_c
        })
    fs = compute_standard(apd_c, apd_w, quiz_c, quiz_w)
    k3 = compute_k3(apd_c, apd_w, quiz_c, quiz_w)
    rows.append({
        "studentName": name.strip(),
        "attemptNumber": attempt,
        "apdTotalCorrect": apd_c, "apdTotalWrong": apd_w,
        "apdTimeTakenSeconds": apd_time,
        "quizTotalCorrect": quiz_c, "quizTotalWrong": quiz_w,
        "questionTimes": question_times,
        "quizTimeTakenSeconds": quiz_time,
        "finalScore": fs, "finalScoreStandard": fs, "finalScoreK3": k3,
        "timestamp": ts
    })

data_json = json.dumps(rows, indent=2)
print(f"Generated {len(rows)} score records")

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

# Write via docker exec node
import base64
b64 = base64.b64encode(data_json.encode()).decode()
node_script = f"const fs=require('fs'); fs.writeFileSync('/app/data/student_scores.json',Buffer.from('{b64}','base64').toString()); console.log('written');"
cmd = f"docker exec labshield-server node -e '{node_script}'"
i, o, e = c.exec_command(cmd, timeout=30)
out = o.read().decode(errors="replace").strip()
err = e.read().decode(errors="replace").strip()
print(f"Write: {out[:100]}")
if err: print(f"ERR: {err[:200]}")

# Verify
i, o, e = c.exec_command("docker exec labshield-server python3 -c 'import json;s=json.load(open(\"/app/data/student_scores.json\"));print(len(s))'", timeout=30)
count = o.read().decode(errors="replace").strip()
print(f"Verified: {count} records in student_scores.json")
c.close()
