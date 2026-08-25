import os, tarfile, tempfile, time
from pathlib import Path
import paramiko

TAB_PW = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
SRC = Path(r"C:\Users\carlo\OneDrive\Documents\Web Project\WebTools")
REMOTE = "/home/carlo/toolora"
HOST_PORT = 9008
CONTAINER_PORT = 9003
EXCLUDE = {"node_modules", ".next", "build", "dist", ".git", "__pycache__", "data", ".cache", ".tmp", ".venv", "venv"}

def make_archive():
    archive = Path(tempfile.gettempdir()) / "toolora.tar.gz"
    if archive.exists(): archive.unlink()
    with tarfile.open(archive, "w:gz") as tar:
        count = 0
        for p in SRC.rglob("*"):
            if not p.is_file(): continue
            rel = str(p.relative_to(SRC).as_posix())
            parts = rel.split("/")
            if any(part in EXCLUDE or part.startswith(".") for part in parts):
                continue
            tar.add(p, arcname=rel)
            count += 1
    print(f"Archive: {archive.stat().st_size / 1024:.0f} KB, {count} files")
    return archive

archive = make_archive()
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=TAB_PW, timeout=20, look_for_keys=False, allow_agent=False)

print("Uploading...")
sftp = c.open_sftp()
sftp.put(str(archive), "/tmp/toolora.tar.gz")
sftp.close()
archive.unlink()

def run(cmd, t=120):
    i, o, e = c.exec_command(cmd, timeout=t)
    out = o.read().decode(errors="replace").strip()
    err = e.read().decode(errors="replace").strip()
    if out: print(f"  {out[:200]}")
    if err: print(f"  ERR: {err[:200]}")

run(f"mkdir -p {REMOTE} && tar -xzf /tmp/toolora.tar.gz -C {REMOTE} && rm /tmp/toolora.tar.gz && echo OK", 120)
compose = f"""services:
  toolora:
    build: .
    container_name: toolora
    ports:
      - "{HOST_PORT}:{CONTAINER_PORT}"
    restart: always
"""
run(f"cat > {REMOTE}/docker-compose.yml <<'CMP'\n{compose}\nCMP", 30)
print("Building...")
run(f"cd {REMOTE} && docker compose up -d --build --remove-orphans", 600)
time.sleep(5)
run(f"curl -sS -o /dev/null -w 'HTTP %{{http_code}}' http://127.0.0.1:{HOST_PORT}/ 2>/dev/null", 30)
run("docker ps --filter name=toolora --format '{{.Names}} {{.Status}} {{.Ports}}'", 30)
c.close()
print("DONE")
