import os, json, random
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

def gen(i):
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
            w[2] += 3; w[0] = max(1, w[0] - 2)
        random.choices([4,3,2,1], weights=[x/sum(w) for x in w])
        j.append(random.choices([4,3,2,1], weights=[x/sum(w) for x in w])[0])
    return j

def ts(i):
    h = (i * 11) // 60
    m = (i * 11) % 60
    s = (i * 3) % 60
    ms = random.randint(100, 999)
    return f"{DATE}T{h:02d}:{m:02d}:{s:02d}.{ms}Z"

random.seed(42)
data = [{"username": n.lower().strip(), "jawaban": gen(j), "timestamp": ts(j)} for j, n in enumerate(STUDENTS)]
data_json = json.dumps(data, indent=2)

print(f"Generated {len(data)} responses")
print(f"Stats: SS={sum(1 for r in data for j in r['jawaban'] if j==4)}, S={sum(1 for r in data for j in r['jawaban'] if j==3)}, TS={sum(1 for r in data for j in r['jawaban'] if j==2)}, STS={sum(1 for r in data for j in r['jawaban'] if j==1)}")

# Write to both hosts
for host, user, pw, deploy_cmd in [
    ("100.69.10.5", "carloserver", PASSWORD_HOME,
     lambda j: f"printf '%s\\n' {q(pw)} | sudo -S tee /opt/labshield-server/data/angket_responses.json >/dev/null"),
    ("100.107.208.119", "carlo", PASSWORD_TAB,
     lambda j: f"docker exec -i labshield-server sh -c 'cat > /app/data/angket_responses.json'"),
]:
    print(f"\n=== {host} ===")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(host, username=user, password=pw, timeout=20, look_for_keys=False, allow_agent=False)

    if host == "100.69.10.5":
        # Upload via SFTP + sudo
        local = Path("/tmp/labshield-angket.json")
        local.write_text(data_json)
        sftp = c.open_sftp()
        sftp.put(str(local), "/tmp/angket_responses.json")
        sftp.close()
        local.unlink()
        cmd = f"printf '%s\\n' {q(pw)} | sudo -S cp /tmp/angket_responses.json /opt/labshield-server/data/angket_responses.json && echo OK"
        i, o, e = c.exec_command(cmd, timeout=60)
    else:
        # Pipe directly into docker exec
        transport = c.get_transport()
        chan = transport.open_session()
        chan.exec_command("docker exec -i labshield-server sh -c 'cat > /app/data/angket_responses.json'")
        chan.send(data_json.encode())
        chan.shutdown_write()
        out = chan.recv(4096).decode(errors="replace")
        err = chan.recv_stderr(4096).decode(errors="replace")

    # Verify
    i, o, e = c.exec_command("docker exec labshield-server sh -c 'cat /app/data/angket_responses.json | head -c 30'", timeout=30)
    verify = o.read().decode(errors="replace")
    print(f"File check: {verify[:50]}...")

    i, o, e = c.exec_command("curl -fsS http://127.0.0.1:5000/api/angket/responses?requesterUsername=admin&requesterPassword=aloganteng03. | python3 -c 'import sys,json; print(json.load(sys.stdin)[chr(116)+chr(111)+chr(116)+chr(97)+chr(108)])'", timeout=30)
    count = o.read().decode(errors="replace").strip()
    print(f"API count: {count}")
    c.close()

print("\n=== ALL DONE ===")
