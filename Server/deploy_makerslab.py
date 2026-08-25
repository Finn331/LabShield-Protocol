import os, tarfile, tempfile, time
from pathlib import Path
import paramiko

TAB_PW = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
SRC = Path(r"C:\Users\carlo\OneDrive\Documents\Web Project\Makers Lab Web")
REMOTE = "/home/carlo/makers-lab-web"
HOST_PORT = 9014
CONTAINER_PORT = 9007
EXCLUDE = {"node_modules", ".next", "build", "dist", ".git", "__pycache__", "data", ".cache", ".tmp", ".venv"}

def make_archive():
    archive = Path(tempfile.gettempdir()) / "makerslab.tar.gz"
    if archive.exists(): archive.unlink()
    with tarfile.open(archive, "w:gz") as tar:
        for p in SRC.rglob("*"):
            if not p.is_file(): continue
            rel = str(p.relative_to(SRC).as_posix())
            parts = rel.split("/")
            if any(part in EXCLUDE or part.startswith(".") for part in parts):
                continue
            tar.add(p, arcname=rel)
    print(f"Archive: {archive.stat().st_size / 1024:.0f} KB")
    return archive

archive = make_archive()
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=TAB_PW, timeout=20, look_for_keys=False, allow_agent=False)

print("Uploading...")
sftp = c.open_sftp()
sftp.put(str(archive), "/tmp/makerslab.tar.gz")
sftp.close()
archive.unlink()

def run(cmd, t=120):
    i, o, e = c.exec_command(cmd, timeout=t)
    out = o.read().decode(errors="replace").strip()
    err = e.read().decode(errors="replace").strip()
    if out: print(f"  {out[:200]}")
    if err: print(f"  ERR: {err[:200]}")

run(f"mkdir -p {REMOTE} && tar -xzf /tmp/makerslab.tar.gz -C {REMOTE} && rm /tmp/makerslab.tar.gz && echo OK", 60)
compose = f"""services:
  makerslab:
    build: .
    container_name: makerslab
    ports:
      - "{HOST_PORT}:{CONTAINER_PORT}"
    restart: always
"""
run(f"cat > {REMOTE}/docker-compose.yml <<'CMP'\n{compose}\nCMP", 30)
print("Building...")
run(f"cd {REMOTE} && docker compose up -d --build --remove-orphans", 600)
time.sleep(5)
run(f"curl -sS -o /dev/null -w 'HTTP %{{http_code}}' http://127.0.0.1:{HOST_PORT}/ 2>/dev/null", 30)
run("docker ps --filter name=makerslab --format '{{.Names}} {{.Status}} {{.Ports}}'", 30)
c.close()
print("DONE")
