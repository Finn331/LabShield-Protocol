import urllib.request

domains = {
    "LabShield": "https://labshieldprotocol.my.id/",
    "Portfolio": "https://carlomuzaqi.my.id/",
    "MakersLab": "https://makerslab.engineer/",
    "Toolora": "https://toolora.cloud/",
    "Hydroponic": "https://smarthydroponic.my.id/",
}
all_ok = True
for name, url in domains.items():
    try:
        req = urllib.request.Request(url, method="GET")
        resp = urllib.request.urlopen(req, timeout=15)
        print(f"  {name:15s}: {resp.status} OK")
    except urllib.request.HTTPError as e:
        print(f"  {name:15s}: {e.code} {e.reason}")
        all_ok = False
    except Exception as e:
        print(f"  {name:15s}: ERROR {str(e)[:40]}")
        all_ok = False

print(f"\n{'ALL OK!' if all_ok else 'Some domains still failing'}")

# Also verify tunnel has both connectors by checking from carlotab
import paramiko, os
try:
    pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)
    i, o, e = c.exec_command("curl -sS -o /dev/null -w '%{http_code}' https://carlomuzaqi.my.id/ 2>/dev/null || echo fail", timeout=15)
    portf = o.read().decode(errors="replace").strip()
    i, o, e = c.exec_command("docker ps --format '{{.Names}}' | wc -l", timeout=15)
    cnt = o.read().decode(errors="replace").strip()
    print(f"\nFrom carlotab: portfolio={portf}")
    print(f"Containers running: {cnt}")
    c.close()
except Exception as e:
    print(f"\nCarlotab check: {e}")
