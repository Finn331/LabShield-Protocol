import os, json, tarfile, tempfile, time
from pathlib import Path
import paramiko

TAB_PW = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")

# Project definitions: (name, local_path, remote_dir, compose_file, port)
# Ports from home lab docker status
PROJECTS = [
    ("portfolio-react",
     r"C:\Users\carlo\OneDrive\Documents\Web Project\portfolio-react",
     "/home/carlo/portfolio-react", None, 9006, 8080),
    ("makers-lab-web",
     r"C:\Users\carlo\OneDrive\Documents\Web Project\Makers Lab Web",
     "/home/carlo/makers-lab-web", None, 9014, 9007),
    ("toolora",
     r"C:\Users\carlo\OneDrive\Documents\Web Project\WebTools",
     "/home/carlo/toolora", None, 9008, 9003),
    ("smarthydroponic",
     r"C:\Users\carlo\OneDrive\Documents\Web Project\Smart Hydroponic Ciul",
     "/home/carlo/smarthydroponic", None, 3000, 3000),
]

def make_archive(name, local_path, exclude_dirs=None):
    if exclude_dirs is None:
        exclude_dirs = {"node_modules", ".next", "build", "dist", "__pycache__", ".git", "data"}
    local = Path(local_path)
    if not local.exists():
        print(f"  SKIP {name}: path not found")
        return None
    archive = Path(tempfile.gettempdir()) / f"deploy-{name}.tar.gz"
    if archive.exists():
        archive.unlink()
    with tarfile.open(archive, "w:gz") as tar:
        for p in local.rglob("*"):
            rel = p.relative_to(local)
            rel_str = str(rel.as_posix())
            if any(rel_str.startswith(e + "/") or rel_str == e for e in exclude_dirs):
                continue
            if p.is_file():
                tar.add(p, arcname=rel_str)
    print(f"  Archive {name}: {archive.stat().st_size / 1024 / 1024:.1f} MB")
    return archive

def run(c, cmd, t=60):
    i, o, e = c.exec_command(cmd, timeout=t)
    out = o.read().decode(errors="replace")
    err = e.read().decode(errors="replace")
    if out and len(out) < 500: print(out.strip())
    if err: print(f"  ERR: {err[:200]}")
    return out

def main():
    if not TAB_PW:
        raise RuntimeError("Set LABSHIELD_TAB_PASSWORD")

    print("=== Creating archives ===")
    archives = {}
    for name, local, *_ in PROJECTS:
        print(f"\n{name}:")
        a = make_archive(name, local)
        if a:
            archives[name] = a

    if not archives:
        print("No projects to deploy!")
        return

    print(f"\n=== Connecting to carlotab ===")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect("100.107.208.119", username="carlo", password=TAB_PW, timeout=20, look_for_keys=False, allow_agent=False)

    print(f"\n=== Uploading & deploying {len(archives)} projects ===")
    for name, local, remote_dir, compose, port, dockerfile_port in PROJECTS:
        if name not in archives:
            continue
        archive = archives[name]
        print(f"\n--- {name} -> {remote_dir} (port {port}) ---")

        # Upload
        sftp = c.open_sftp()
        remote_archive = f"/tmp/deploy-{name}.tar.gz"
        sftp.put(str(archive), remote_archive)
        sftp.close()
        archive.unlink()

        # Extract
        run(c, f"mkdir -p {remote_dir} && tar -xzf {remote_archive} -C {remote_dir} && rm -f {remote_archive}", 120)

        # Check for compose file
        local_compose = list(Path(local).glob("*docker-compose*"))
        has_compose = len(local_compose) > 0

        # If no compose file, create one
        if not has_compose:
            compose = f"""version: '3.8'
services:
  {name}:
    build: .
    container_name: {name}
    ports:
      - "{port}:{dockerfile_port}"
    restart: always
"""
            run(c, f"cat > {remote_dir}/docker-compose.yml <<'COMPOSE'\n{compose}\nCOMPOSE", 30)
            print(f"  Created docker-compose.yml for {name}")

        # Build & start
        run(c, f"cd {remote_dir} && docker compose up -d --build --remove-orphans", 600)
        time.sleep(3)

        # Verify
        run(c, f"curl -sS -o /dev/null -w '%{{http_code}}' http://127.0.0.1:{port}/ 2>/dev/null || echo fail")

    print(f"\n=== All containers ===")
    run(c, "docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'", 30)

    print(f"\n=== Verify all ports ===")
    for _, _, _, _, port, _ in PROJECTS:
        run(c, f"curl -sS -o /dev/null -w '%{{http_code}}' http://127.0.0.1:{port}/ 2>/dev/null || echo closed", 15)

    c.close()
    print("\n=== DONE ===")

if __name__ == "__main__":
    main()
