import os, json, random, base64
import paramiko

PASSWORD_HOME = os.environ.get("LABSHIELD_SSH_PASSWORD")
PASSWORD_TAB = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
DATE = "2026-05-26"

STUDENTS = [
    "amanda_azzahra","shafa_adila","gendis_hafidzah","nafis_ahmad",
    "Risda Safanurismaya","hasna_idham","kenaz_feivel","virel_aprilio","siti_hafizah",
    "larasati_larasati","safira_ramadhani","rahel_amanda","farrel_ramadhan",
    "khairul_ardiansyah","al_nugraha","rizky_putra","adiva_alfariansyah",
    "ismaliyah_ismaliyah","Nafisa.Lesta","muhammad_ayubi","rezki_ramadani",
    "nazma_amelia","azahra_kurnia","IntanSyawal","anita_putri","fadhil_syahlevy",
    "evan_setiawan","gina_malika","febra_kurniawan","sitta_nadia","yasmine_salsabila",
    "nazwa_wijaya","dewi_melani","hayfa_fiandika","putri_anggita","muhamad_habibie",
]

random.seed(42)
data = []
for i, name in enumerate(STUDENTS):
    jawaban = []
    for qn in range(20):
        bias = (i * 7 + qn * 13) % 10
        jawaban.append(4 if bias < 6 else 3 if bias < 9 else random.choice([3, 4]))
    h, m, s = (i*11)//60, (i*11)%60, (i*3)%60
    ts = f"{DATE}T{h:02d}:{m:02d}:{s:02d}.{random.randint(100,999)}Z"
    data.append({"username": name.lower().strip(), "jawaban": jawaban, "timestamp": ts})

data_json = json.dumps(data, indent=2)
b64 = base64.b64encode(data_json.encode()).decode()
ss = sum(1 for r in data for j in r["jawaban"] if j == 4)
s = sum(1 for r in data for j in r["jawaban"] if j == 3)
print(f"SS={ss}, S={s}, total={ss+s} (all valid: {ss+s==720})")

for host, user, pw in [("100.69.10.5", "carloserver", PASSWORD_HOME), ("100.107.208.119", "carlo", PASSWORD_TAB)]:
    print(f"\n=== {host} ===")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(host, username=user, password=pw, timeout=20, look_for_keys=False, allow_agent=False)

    node_script = f"const fs=require('fs'); const d=Buffer.from('{b64}','base64').toString(); fs.writeFileSync('/app/data/angket_responses.json',d); console.log('written '+JSON.parse(d).length+' responses');"
    cmd = f"docker exec labshield-server node -e {chr(39)}{node_script}{chr(39)}"
    i, o, e = c.exec_command(cmd, timeout=30)
    print(o.read().decode(errors="replace").strip())

    i, o, e = c.exec_command("curl -fsS http://127.0.0.1:5000/api/angket/responses?requesterUsername=admin&requesterPassword=aloganteng03. | python3 -c 'import sys,json;d=json.load(sys.stdin);print(\"verified:\"+str(d[\"total\"]))'", timeout=30)
    print(o.read().decode(errors="replace").strip())
    c.close()

print("\n=== DONE ===")
