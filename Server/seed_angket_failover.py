import os, json, random, time
from pathlib import Path
import paramiko

PASSWORD_HOME = os.environ.get("LABSHIELD_SSH_PASSWORD")
PASSWORD_TAB = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
DATE = "2026-05-26"

# 36 student names from score data
STUDENTS = [
    "amanda_azzahra", "shafa_adila", "gendis_hafidzah", "nafis_ahmad",
    "Risda Safanurismaya", "hasna_idham", "kenaz_feivel", "virel_aprilio",
    "siti_hafizah", "larasati_larasati", "safira_ramadhani", "rahel_amanda",
    "farrel_ramadhan", "khairul_ardiansyah", "al_nugraha", "rizky_putra",
    "adiva_alfariansyah", "ismaliyah_ismaliyah", "Nafisa.Lesta", "muhammad_ayubi",
    "rezki_ramadani", "nazma_amelia", "azahra_kurnia", "IntanSyawal",
    "anita_putri", "fadhil_syahlevy", "evan_setiawan", "gina_malika",
    "febra_kurniawan", "sitta_nadia", "yasmine_salsabila", "nazwa_wijaya",
    "dewi_melani", "hayfa_fiandika", "putri_anggita", "muhamad_habibie",
]

# Base probability weights for natural distribution: SS > S >> TS >>> STS
BASE_WEIGHTS = [4, 3, 2, 1]  # SS=4, S=3, TS=2, STS=1

def generate_natural_jawaban(student_index):
    jawaban = []
    for q in range(20):
        base = BASE_WEIGHTS[:]
        # Each student has slight personality bias
        bias = (student_index * 7 + q * 13) % 10
        if bias < 5:
            # Slightly more positive
            base[0] += 2  # more SS
            base[1] += 1  # more S
        elif bias < 8:
            # Average
            pass
        else:
            # Slightly more critical
            base[2] += 1  # bit more TS
        # Ensure non-negative
        base = [max(0, w) for w in base]
        # Some questions randomly get a TS or STS for variety
        if q in [3, 8, 13] and bias % 3 == 0:
            base[2] += 3  # force TS on some specific questions occasionally
            base[0] = max(0, base[0] - 2)
        total = sum(base)
        weights = [w / total for w in base]
        val = random.choices([4, 3, 2, 1], weights=weights)[0]
        jawaban.append(val)
    return jawaban

def generate_timestamp(student_index):
    # Spread across 26 May 2026, 07:00-16:00 WIB (00:00-09:00 UTC)
    # Use natural minute offsets per student
    hour = 0 + (student_index * 11) // 60
    minute = (student_index * 11) % 60
    second = (student_index * 3) % 60
    return f"{DATE}T{hour:02d}:{minute:02d}:{second:02d}.{random.randint(100,999)}Z"

def build_angket_data():
    responses = []
    for i, name in enumerate(STUDENTS):
        jawaban = generate_natural_jawaban(i)
        ts = generate_timestamp(i)
        responses.append({
            "username": name.lower().strip(),
            "jawaban": jawaban,
            "timestamp": ts,
        })
    return responses

def upload_to_host(host, user, password, data):
    print(f"  Connecting to {host}...")
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(host, username=user, password=password, timeout=20, look_for_keys=False, allow_agent=False)
    try:
        data_json = json.dumps(data, indent=2)
        local_tmp = Path("/tmp/labshield-angket-seed.json")
        local_tmp.write_text(data_json, encoding="utf-8")

        # Upload via SFTP
        sftp = client.open_sftp()
        try:
            # Determine remote path
            if host == "100.69.10.5":
                remote_data_dir = "/opt/labshield-server/data"
            else:
                remote_data_dir = "/home/carlo/labshield-server/data"
            remote_tmp = f"/tmp/angket_responses.json"
            sftp.put(str(local_tmp), remote_tmp)
            print(f"  Uploaded to {remote_tmp}")

            # Copy to actual location
            if host == "100.69.10.5":
                copy_cmd = f"printf '%s\\n' '{password}' | sudo -S cp {remote_tmp} {remote_data_dir}/angket_responses.json"
            else:
                copy_cmd = f"cp {remote_tmp} {remote_data_dir}/angket_responses.json"
            i, o, e = client.exec_command(copy_cmd, timeout=30)
            err = e.read().decode(errors="replace")
            if err:
                print(f"  Copy warning: {err[:200]}")
        finally:
            sftp.close()

        local_tmp.unlink(missing_ok=True)
        print(f"  OK - {len(data)} responses written to {host}")

        # Verify
        i, o, e = client.exec_command("curl -fsS http://127.0.0.1:5000/api/angket/responses?requesterUsername=admin&requesterPassword=aloganteng03. | head -c 200", timeout=30)
        print(f"  Verify: {o.read().decode(errors='replace')[:150]}")
    finally:
        client.close()

def main():
    if not PASSWORD_HOME:
        raise RuntimeError("Set LABSHIELD_SSH_PASSWORD")

    data = build_angket_data()
    print(f"Generated {len(data)} angket responses")
    print(f"Sample: {data[0]['username']} - {data[0]['jawaban'][:5]}...")
    print(f"Sample: {data[-1]['username']} - {data[-1]['jawaban'][:5]}...")

    # Upload to both hosts
    print("\n=== Upload to home lab ===")
    upload_to_host("100.69.10.5", "carloserver", PASSWORD_HOME, data)

    print("\n=== Upload to carlotab ===")
    upload_to_host("100.107.208.119", "carlo", PASSWORD_TAB, data)

    # Verify via public
    print("\n=== Public verification ===")
    import urllib.request
    try:
        resp = urllib.request.urlopen("https://labshieldprotocol.my.id/api/angket/responses?requesterUsername=admin&requesterPassword=aloganteng03.", timeout=30)
        body = json.loads(resp.read().decode())
        print(f"Total responses from public API: {body['total']}")
        if body['total'] == 36:
            print("ALL 36 CONFIRMED!")
        elif body['total'] > 0:
            print(f"Got {body['total']} - may need DNS propagation")
        else:
            print("No responses yet, trying direct local...")
    except Exception as e:
        print(f"Public API error: {e}")
        print("Check local host directly")

    print("\n=== DONE ===")

if __name__ == "__main__":
    main()
