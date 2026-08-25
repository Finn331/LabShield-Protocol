import os, json, tempfile, pathlib, paramiko

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")

rows = []
for r in [
    ["amanda_azzahra",5,0,17,3,47.1,313.405,"2026-05-26T04:41:18.857Z"],
    ["shafa_adila",5,0,17,3,37.3,358.6664,"2026-05-26T05:00:22.286Z"],
    ["gendis_hafidzah",5,1,17,3,43.7,257.4877,"2026-05-26T04:46:34.286Z"],
    ["nafis_ahmad",5,0,15,5,8.2,362.1252,"2026-05-26T04:53:48.000Z"],
    ["Risda Safanurismaya",5,2,16,4,45.3,267.8738,"2026-05-26T04:58:24.000Z"],
    ["hasna_idham",4,1,17,3,36.5,316.4381,"2026-05-26T04:47:53.143Z"],
    ["kenaz_feivel",3,2,20,0,50.1,307.2268,"2026-05-26T04:50:30.857Z"],
    ["virel_aprilio",3,1,18,2,47.2,307.6157,"2026-05-26T05:02:20.571Z"],
    ["siti_hafizah",5,1,19,1,40.1,325.9324,"2026-05-26T05:01:01.714Z"],
    ["larasati_larasati",5,2,19,1,15.8,293.008,"2026-05-26T04:51:49.714Z"],
    ["safira_ramadhani",3,1,20,0,17,254.7493,"2026-05-26T04:59:42.857Z"],
    ["rahel_amanda",4,0,15,5,28.1,267.1804,"2026-05-26T04:57:05.143Z"],
    ["farrel_ramadhan",3,2,17,3,16.1,290.5462,"2026-05-26T04:45:15.429Z"],
    ["khairul_ardiansyah",4,0,17,3,15.9,294.4556,"2026-05-26T04:51:10.286Z"],
    ["al_nugraha",5,1,19,1,47.7,350.737,"2026-05-26T04:40:39.429Z"],
    ["rizky_putra",5,0,19,1,12.5,324.3351,"2026-05-26T04:59:03.429Z"],
    ["adiva_alfariansyah",4,2,20,0,52.7,334.3429,"2026-05-26T04:40:00.000Z"],
    ["ismaliyah_ismaliyah",3,0,19,1,12.4,323.6528,"2026-05-26T04:49:51.429Z"],
    ["Nafisa.Lesta",3,1,15,5,34.2,322.1035,"2026-05-26T04:54:27.429Z"],
    ["muhammad_ayubi",3,0,18,2,8.1,278.4884,"2026-05-26T04:53:08.571Z"],
    ["rezki_ramadani",5,1,20,0,40.8,298.6329,"2026-05-26T04:57:44.571Z"],
    ["nazma_amelia",5,0,17,3,12.5,304.0361,"2026-05-26T04:55:06.857Z"],
    ["azahra_kurnia",5,2,16,4,49.3,301.45,"2026-05-26T04:42:37.714Z"],
    ["IntanSyawal",4,2,19,0,49.29134,201.899,"2026-05-26T04:49:12.000Z"],
    ["anita_putri",3,0,19,1,44.8,229.8529,"2026-05-26T04:41:58.286Z"],
    ["fadhil_syahlevy",3,0,18,2,21,312.418,"2026-05-26T04:44:36.000Z"],
    ["evan_setiawan",4,2,17,3,40,253.5699,"2026-05-26T04:43:56.571Z"],
    ["gina_malika",5,2,18,2,27.5,351.1707,"2026-05-26T04:47:13.714Z"],
    ["febra_kurniawan",5,2,20,0,49.5,261.7508,"2026-05-26T04:45:54.857Z"],
    ["sitta_nadia",5,1,18,2,24.5,317.5126,"2026-05-26T05:01:41.143Z"],
    ["yasmine_salsabila",5,0,15,5,11.3,252.1195,"2026-05-26T05:03:00.000Z"],
    ["nazwa_wijaya",3,1,16,4,39.5,296.3675,"2026-05-26T04:55:46.286Z"],
    ["dewi_melani",4,2,19,1,20.6,237.273,"2026-05-26T04:43:17.143Z"],
    ["hayfa_fiandika",4,2,19,1,31,302.5821,"2026-05-26T04:48:32.571Z"],
    ["putri_anggita",4,0,17,3,29.5,277.2414,"2026-05-26T04:56:25.714Z"],
    ["muhamad_habibie",4,2,18,2,38.4,329.7143,"2026-05-26T04:52:29.143Z"],
]:
    name, apd_c, apd_w, quiz_c, quiz_w, apd_t, quiz_t, ts = r
    tot = quiz_c + quiz_w
    def std(): t=apd_c+apd_w+quiz_c+quiz_w; return round((apd_c+quiz_c)/t*100) if t>0 else 0
    def k3():
        a1=apd_c+apd_w; q1=quiz_c+quiz_w
        aa=apd_c/a1*100 if a1>0 else 0; qa=quiz_c/q1*100 if q1>0 else 0
        return max(0,min(100,round(aa*.6+qa*.4-min(20,apd_w*5))))
    rows.append({
        "studentName": name.strip(), "attemptNumber": 1,
        "apdTotalCorrect": apd_c, "apdTotalWrong": apd_w,
        "apdTimeTakenSeconds": apd_t,
        "quizTotalCorrect": quiz_c, "quizTotalWrong": quiz_w,
        "questionTimes": [
            {"questionID":f"Q{i+1}","timeTakenSeconds":quiz_t/tot if tot>0 else 0,"isCorrect":i<quiz_c}
            for i in range(tot)
        ],
        "quizTimeTakenSeconds": quiz_t,
        "finalScore": std(), "finalScoreStandard": std(), "finalScoreK3": k3(),
        "timestamp": ts
    })

data_json = json.dumps(rows, indent=2)
print(f"{len(rows)} records, {len(data_json)} bytes")

tmp = pathlib.Path(tempfile.gettempdir()) / "scores.json"
tmp.write_text(data_json)

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

sftp = c.open_sftp()
sftp.put(str(tmp), "/tmp/scores.json")
sftp.close()
tmp.unlink()

i, o, e = c.exec_command("docker cp /tmp/scores.json labshield-server:/app/data/student_scores.json && echo CP_OK", timeout=30)
print(o.read().decode(errors="replace").strip() or "cp done")

i, o, e = c.exec_command("curl -fsS http://127.0.0.1:5000/api/scores | python3 -c $'import sys,json;d=json.load(sys.stdin);print(\"records=\"+str(len(d)))'", timeout=30)
print(o.read().decode(errors="replace").strip())
c.close()
