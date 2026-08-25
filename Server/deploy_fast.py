import os, paramiko
from pathlib import Path

TAB_PW = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")

PROJECTS = [
    ("portfolio", r"C:\Users\carlo\OneDrive\Documents\Web Project\portfolio-react",
     "/home/carlo/portfolio-react", 9006, 8080),
    ("makerslab", r"C:\Users\carlo\OneDrive\Documents\Web Project\Makers Lab Web",
     "/home/carlo/makers-lab-web", 9014, 9007),
    ("toolora", r"C:\Users\carlo\OneDrive\Documents\Web Project\WebTools",
     "/home/carlo/toolora", 9008, 9003),
    ("hydroponic", r"C:\Users\carlo\OneDrive\Documents\Web Project\Smart Hydroponic Ciul",
     "/home/carlo/smarthydroponic", 3000, 3000),
]

EXCLUDE_DIRS = {"node_modules", ".next", "build", "dist", ".git", "__pycache__",
                "data", ".venv", "venv", ".cache", "target", "tmp", ".tmp"}
EXCLUDE_EXTS = {".log", ".pyc", ".db", ".sqlite", ".zip", ".tar", ".gz", ".exe", ".so", ".dll"}

def should_exclude(path, rel_str):
    parts = rel_str.split("/")
    for p in parts:
        if p in EXCLUDE_DIRS or p.startswith("."):
            return True
    ext = Path(path).suffix
    if ext in EXCLUDE_EXTS:
        return True
    return False

def upload_dir(sftp, local_root, remote_root, max_files=2000):
    count = 0
    for p in sorted(local_root.rglob("*")):
        if not p.is_file():
            continue
        rel = str(p.relative_to(local_root).as_posix())
        if should_exclude(p, rel):
            continue
        remote = f"{remote_root}/{rel}"
        try:
            sftp.put(str(p), remote)
            count += 1
            if count % 500 == 0:
                print(f"    {count} files...")
            if count >= max_files:
                print(f"    Reached {max_files} files limit")
                break
        except Exception:
            try:
                sftp.mkdir(str(Path(remote).parent))
                sftp.put(str(p), remote)
                count += 1
            except Exception:
                pass
    return count

def run(c, cmd, t=120):
    i, o, e = c.exec_command(cmd, timeout=t)
    out = o.read().decode(errors="replace").strip()
    err = e.read().decode(errors="replace").strip()
    if out: print(f"  {out[:200]}")
    if err: print(f"  ERR: {err[:200]}")

def main():
    if not TAB_PW: raise RuntimeError("Set LABSHIELD_TAB_PASSWORD")

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect("100.107.208.119", username="carlo", password=TAB_PW, timeout=20, look_for_keys=False, allow_agent=False)

    for name, local, remote, host_port, container_port in PROJECTS:
        local_root = Path(local)
        if not local_root.exists():
            print(f"\n=== {name}: SKIP (not found) ===")
            continue

        print(f"\n=== {name} ({local_root.name}) ===")

        # Create remote dirs
        run(c, f"mkdir -p {remote} && mkdir -p {remote}/src {remote}/public 2>/dev/null; echo MKDIR_OK", 30)

        # Upload files
        print(f"  Uploading files...")
        sftp = c.open_sftp()
        count = upload_dir(sftp, local_root, remote, max_files=3000)
        sftp.close()
        print(f"  Uploaded {count} files")

        # Build and run
        print(f"  Building Docker image...")
        run(c, f"cd {remote} && docker compose up -d --build --remove-orphans", 600)

    # Verify all
    print(f"\n=== VERIFICATION ===")
    import time
    time.sleep(5)
    for name, _, _, host_port, _ in PROJECTS:
        run(c, f"curl -sS -o /dev/null -w '%{{http_code}}' http://127.0.0.1:{host_port}/ 2>/dev/null || echo closed", 15)

    run(c, "docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'", 30)
    c.close()
    print(f"\n=== DONE ===")

if __name__ == "__main__":
    main()
