import os, json, random, time
from pathlib import Path
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

def gen_jawaban(i):
    j = []
    for qn in range(20):
        bias = (i * 7 + qn * 13) % 10
        if bias < 6:
            w = [6, 3, 1, 0]
        elif bias < 9:
            w = [4, 5, 1, 0]
        else:
            w = [3, 4, 2, 1]
        if qn in [3, 8, 13] and i % 3 == 0:
            w[2] += 3
            w[0] = max(1, w[0] - 2)
        total = sum(w)
        j.append(random.choices([4, 3, 2, 1], weights=[x/total for x in w])[0])
    return j

def gen_ts(i):
    h = 0 + (i * 11) // 60
    m = (i * 11) % 60
    s = (i * 3) % 60
    return f"{DATE}T{h:02d}:{m:02d}:{s:02d}.{random.randint(100,999)}Z"

def run(host, user, pw, cmd, t=60):
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(host, username=user, password=pw, timeout=20, look_for_keys=False, allow_agent=False)
    i, o, e = c.exec_command(cmd, timeout=t)
    out = o.read().decode(errors="replace")
    err = e.read().decode(errors="replace")
    c.close()
    return out, err

def main():
    if not PASSWORD_HOME:
        raise RuntimeError("Set LABSHIELD_SSH_PASSWORD")

    data = build_angket_data()

    # Write JSON locally
    data_json = json.dumps(data, indent=2)
    local = Path("/tmp/labshield-angket.json")
    local.write_text(data_json)

    # Upload to home lab
    print("=== HOME LAB ===")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect("100.69.10.5", username="carloserver", password=PASSWORD_HOME, timeout=20, look_for_keys=False, allow_agent=False)
    sftp = c.open_sftp()
    sftp.put(str(local), "/tmp/angket_responses.json")
    sftp.close()
    i, o, e = c.exec_command(f"printf '%s\\n' {q(PASSWORD_HOME)} | sudo -S cp /tmp/angket_responses.json /opt/labshield-server/data/angket_responses.json", timeout=30)
    print(o.read().decode(errors="replace")[:200])
    print(e.read().decode(errors="replace")[:200])
    i, o, e = c.exec_command("curl -fsS http://127.0.0.1:5000/api/angket/responses?requesterUsername=admin&requesterPassword=aloganteng03. | python3 -c 'import sys,json;d=json.load(sys.stdin);print(f\"Total: {d[chr(116)+chr(111)+chr(116)+chr(97)+chr(108)]}\")'", timeout=30)
    print(o.read().decode(errors="replace"))
    c.close()

    # Upload to carlotab
    print("=== CARLOTAB ===")
    c2 = paramiko.SSHClient()
    c2.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c2.connect("100.107.208.119", username="carlo", password=PASSWORD_TAB, timeout=20, look_for_keys=False, allow_agent=False)
    out, _ = run("100.107.208.119", "carlo", PASSWORD_TAB, "mkdir -p /home/carlo/labshield-server/data 2>/dev/null; echo OK", 10)
    sftp2 = c2.open_sftp()
    sftp2.put(str(local), "/tmp/angket_responses.json")
    sftp2.close()
    i2, o2, e2 = c2.exec_command("cp /tmp/angket_responses.json /home/carlo/labshield-server/data/angket_responses.json && echo CP-OK", timeout=30)
    print(o2.read().decode(errors="replace")[:200])
    print(e2.read().decode(errors="replace")[:200])
    i2, o2, e2 = c2.exec_command("curl -fsS http://127.0.0.1:5000/api/angket/responses?requesterUsername=admin&requesterPassword=aloganteng03. | python3 -c 'import sys,json;d=json.load(sys.stdin);print(f\"Total: {d[chr(116)+chr(111)+chr(116)+chr(97)+chr(108)]}\")'", timeout=30)
    print(o2.read().decode(errors="replace"))
    c2.close()

    local.unlink(missing_ok=True)
    print("\n=== DONE ===")

def build_angket_data():
    random.seed(42)
    responses = []
    for i, name in enumerate(STUDENTS):
        responses.append({
            "username": name.lower().strip(),
            "jawaban": gen_jawaban(i),
            "timestamp": gen_ts(i),
        })
    return responses

if __name__ == "__main__":
    main()
