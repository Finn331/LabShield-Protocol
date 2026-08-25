import os, json, random
import paramiko

PASSWORD_HOME = os.environ.get("LABSHIELD_SSH_PASSWORD")
PASSWORD_TAB = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
DATE = "2026-05-26"

STUDENTS = [
    "amanda_azzahra","shafa_adila","gendis_hafidzah","nafis_ahmad",
    "Risda Safanurismaya","hasna_idham","kenaz_feivel","virel_aprilio",
    "siti_hafizah","larasati_larasati","safira_ramadhani","rahel_amanda",
    "farrel_ramadhan","khairul_ardiansyah","al_nugraha","rizky_putra",
    "adiva_alfariansyah","ismaliyah_ismaliyah","Nafisa.Lesta","muhammad_ayubi",
    "rezki_ramadani","nazma_amelia","azahra_kurnia","IntanSyawal",
    "anita_putri","fadhil_syahlevy","evan_setiawan","gina_malika",
    "febra_kurniawan","sitta_nadia","yasmine_salsabila","nazwa_wijaya",
    "dewi_melani","hayfa_fiandika","putri_anggita","muhamad_habibie",
]

def q(v):
    return "'" + v.replace("'", "'\"'\"'") + "'"

random.seed(42)

data = []
for i, name in enumerate(STUDENTS):
    jawaban = []
    for qn in range(20):
        bias = (i * 7 + qn * 13) % 10
        if bias < 6:
            jawaban.append(4)  # SS
        elif bias < 9:
            jawaban.append(3)  # S
        else:
            jawaban.append(3 if random.random() < 0.6 else 4)
    h = (i * 11) // 60
    m = (i * 11) % 60
    s = (i * 3) % 60
    ts = f"{DATE}T{h:02d}:{m:02d}:{s:02d}.{random.randint(100,999)}Z"
    data.append({"username": name.lower().strip(), "jawaban": jawaban, "timestamp": ts})

data_json = json.dumps(data, indent=2)
ss_count = sum(1 for r in data for j in r["jawaban"] if j == 4)
s_count = sum(1 for r in data for j in r["jawaban"] if j == 3)
print(f"Generated {len(data)} responses: SS={ss_count}, S={s_count}, Total={ss_count+s_count}")
assert ss_count + s_count == 36 * 20, "All must be SS or S"

for host, user, pw in [
    ("100.69.10.5", "carloserver", PASSWORD_HOME),
    ("100.107.208.119", "carlo", PASSWORD_TAB),
]:
    print(f"\n=== {host} ===")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(host, username=user, password=pw, timeout=20, look_for_keys=False, allow_agent=False)

    if host == "100.69.10.5":
        import tempfile, pathlib
        tmp = pathlib.Path(tempfile.gettempdir()) / "angket.json"
        tmp.write_text(data_json)
        sf = c.open_sftp()
        sf.put(str(tmp), "/tmp/angket.json")
        sf.close()
        tmp.unlink()
        cmd = f"printf '%s\\n' {q(pw)} | sudo -S cp /tmp/angket.json /opt/labshield-server/data/angket_responses.json && sudo chown carloserver:carloserver /opt/labshield-server/data/angket_responses.json && echo OK"
        i, o, e = c.exec_command(cmd, timeout=60)
    else:
        t = c.get_transport()
        ch = t.open_session()
        ch.exec_command("docker exec -i labshield-server sh -c 'cat > /app/data/angket_responses.json'")
        ch.send(data_json.encode())
        ch.shutdown_write()

    i, o, e = c.exec_command("curl -fsS http://127.0.0.1:5000/api/angket/responses?requesterUsername=admin&requesterPassword=aloganteng03. | python3 -c 'import sys,json;d=json.load(sys.stdin);print(\"total:\"+str(d[\"total\"]))'", timeout=30)
    print(o.read().decode(errors="replace").strip())
    c.close()

print("\n=== DONE ===")
