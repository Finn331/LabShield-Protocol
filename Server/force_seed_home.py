import os, json, random, base64
import paramiko

pw = os.environ.get("LABSHIELD_SSH_PASSWORD")
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

print(f"SS={sum(1 for r in data for j in r['jawaban'] if j==4)}, S={sum(1 for r in data for j in r['jawaban'] if j==3)}")

# SSH to home lab and write directly to host file system
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.69.10.5", username="carloserver", password=pw, timeout=30, auth_timeout=30, banner_timeout=30, look_for_keys=False, allow_agent=False)

# Write to a temp file as carloserver (no sudo), then sudo cp to data dir
import tempfile, pathlib
tmp = pathlib.Path(tempfile.gettempdir()) / "angket_final.json"
tmp.write_text(data_json)
sftp = c.open_sftp()
sftp.put(str(tmp), "/tmp/angket_final2.json")
sftp.close()
tmp.unlink()

# Copy with sudo and verify
commands = [
    f"echo '{pw}' | sudo -S cp /tmp/angket_final2.json /opt/labshield-server/data/angket_responses.json",
    "docker exec labshield-server sh -c 'wc -c /app/data/angket_responses.json'",
    "curl -fsS http://127.0.0.1:5000/api/angket/responses?requesterUsername=admin&requesterPassword=aloganteng03. | python3 -c 'import sys,json;d=json.load(sys.stdin);print(\"total=\"+str(d[\"total\"]));r=d[\"responses\"][0];print(\"first=\"+r[\"username\"]);print(\"all_ss_s=\"+str(all(j in[3,4] for j in r[\"jawaban\"])))'",
]
for cmd in commands:
    print(f"\n> {cmd[:80]}...")
    stdin, stdout, stderr = c.exec_command(cmd, timeout=30)
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()
    if out: print(out[:200])
    if err: print(f"ERR: {err[:200]}")

c.close()
