import os, sys, time
from pathlib import Path
import paramiko

HOST_HOME = "100.69.10.5"
HOST_TAB = "100.107.208.119"
HOME_USER = "carloserver"
TAB_USER = "carlo"
PASSWORD_HOME = os.environ.get("LABSHIELD_SSH_PASSWORD")
PASSWORD_TAB = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
REMOTE_DIR = os.environ.get("LABSHIELD_REMOTE_DIR", "/home/carlo/labshield-server")

FILES = [
    ("Server/server.js","server.js"),("Server/package.json","package.json"),
    ("Server/Dockerfile","Dockerfile"),("Server/docker-compose.yml","docker-compose.yml"),
    ("Server/public/index.html","public/index.html"),
    ("Server/public/register.html","public/register.html"),
    ("Server/public/dashboard.html","public/dashboard.html"),
    ("Server/public/student-dashboard.html","public/student-dashboard.html"),
    ("Server/public/learning-media.html","public/learning-media.html"),
    ("Server/public/style.css","public/style.css"),
    ("Server/public/angket.html","public/angket.html"),
]

def q(v): return "'"+v.replace("'","'\"'\"'")+"'"

def conn(h,u,p,t=20):
    c=paramiko.SSHClient();c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(h,username=u,password=p,timeout=t,auth_timeout=t,banner_timeout=t,look_for_keys=False,allow_agent=False)
    return c

def run(c,cmd,t=900):
    i,o,e=c.exec_command(cmd,timeout=t)
    ch=o.channel
    while not ch.exit_status_ready():
        if ch.recv_ready(): print(ch.recv(4096).decode(errors="replace"),end="")
        if ch.recv_stderr_ready(): print(ch.recv_stderr(4096).decode(errors="replace"),end="",file=sys.stderr)
        time.sleep(0.2)
    out=o.read().decode(errors="replace");err=e.read().decode(errors="replace")
    if out: print(out,end="")
    if err: print(err,end="",file=sys.stderr)
    code=ch.recv_exit_status()
    if code: raise RuntimeError(f"exit code {code}")

def main():
    if not PASSWORD_HOME: raise RuntimeError("Set LABSHIELD_SSH_PASSWORD")
    root=Path(__file__).resolve().parent.parent

    print("=== 1. Get tunnel credentials from home lab ===")
    hl=conn(HOST_HOME,HOME_USER,PASSWORD_HOME)
    try:
        sftp=hl.open_sftp()
        for fn in ["f0d6194e-1425-41b2-a526-d97025e5a040.json"]:
            loc=str(root/"tmp_creds.json")
            try:
                sftp.get(f"/etc/cloudflared/{fn}",loc)
                break
            except: pass
        sftp.close()
    finally: hl.close()
    print("  Credentials saved")

    print("=== 2. Deploy to carlotab ===")
    tab=conn(HOST_TAB,TAB_USER,PASSWORD_TAB)
    try:
        run(tab,f"mkdir -p {REMOTE_DIR}/public ~/.cloudflared",30)
        print("  Uploading files...")
        sftp=tab.open_sftp()
        try:
            for l,r in FILES:
                print(f"    {l}")
                sftp.put(str(root/l),f"{REMOTE_DIR}/{r}")
            sftp.put(str(root/"tmp_creds.json"),"/home/carlo/.cloudflared/creds.json")
        finally: sftp.close()
        Path(root/"tmp_creds.json").unlink(missing_ok=True)

        print("  cloudflared config...")
        cfg=(f"credentials-file: /home/carlo/.cloudflared/creds.json\ntunnel: f0d6194e-1425-41b2-a526-d97025e5a040\n"
             "ingress:\n  - hostname: labshieldprotocol.my.id\n    service: http://localhost:5000\n"
             "  - hostname: www.labshieldprotocol.my.id\n    service: http://localhost:5000\n"
             "  - service: http_status:404\n")
        run(tab,f"cat > ~/.cloudflared/config.yml <<'EOF'\n{cfg}\nEOF",30)

        print("  Docker build & start...")
        run(tab,f"cd {REMOTE_DIR} && touch .env && docker compose up -d --build --remove-orphans",600)
        run(tab,"curl -fsS http://127.0.0.1:5000/ | head -c 100",30)
        print("  Server OK")

        print("  SSH key setup...")
        run(tab,"mkdir -p ~/.ssh && chmod 700 ~/.ssh",30)
        i,o,e=tab.exec_command("cat ~/.ssh/id_ed25519.pub 2>/dev/null || (ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519 -N '' -q && cat ~/.ssh/id_ed25519.pub)",30)
        pub=o.read().decode(errors="replace").strip()
        if "ssh-ed25519" not in pub:
            run(tab,"ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519 -N '' -q",30)
            i,o,e=tab.exec_command("cat ~/.ssh/id_ed25519.pub",30)
            pub=o.read().decode(errors="replace").strip()
        print(f"  Public key: {pub[:50]}...")
        hl2=conn(HOST_HOME,HOME_USER,PASSWORD_HOME)
        try: run(hl2,f"mkdir -p ~/.ssh && echo {q(pub)} >> ~/.ssh/authorized_keys && sort -u -o ~/.ssh/authorized_keys ~/.ssh/authorized_keys",30)
        finally: hl2.close()
        print("  SSH key added to home lab")

        print("  Sync script...")
        sync=(f"#!/bin/sh\nset -e\nHOME={q(HOST_HOME)} U={q(HOME_USER)} D={q(REMOTE_DIR)}\n"
              "if ssh -o StrictHostKeyChecking=no -o ConnectTimeout=5 $U@$HOME \"echo alive\" 2>/dev/null; then\n"
              "  echo HOME LAB ALIVE; rsync -avz --delete -e \"ssh -o StrictHostKeyChecking=no\" $U@$HOME:$D/data/ $D/data/\n"
              "  echo SYNC OK\nelse\n  echo HOME LAB DOWN - carlotab write mode\nfi")
        run(tab,f"cat > /home/carlo/labshield-sync.sh <<'S'\n{sync}\nS && chmod +x /home/carlo/labshield-sync.sh",30)

        print("  User systemd timer...")
        run(tab,"mkdir -p ~/.config/systemd/user",30)
        timer_body=('[Unit]\nDescription=LabShield data sync\n[Service]\nType=oneshot\nExecStart=/home/carlo/labshield-sync.sh\n'
                    '[Install]\nWantedBy=default.target')
        run(tab,f"cat > ~/.config/systemd/user/labshield-sync.service <<'U'\n{timer_body}\nU",30)
        run(tab,f"cat > ~/.config/systemd/user/labshield-sync.timer <<'U'\n[Unit]\nDescription=Sync every 5min\n[Timer]\nOnBootSec=1min\nOnUnitActiveSec=5min\nUnit=labshield-sync.service\n[Install]\nWantedBy=timers.target\nU",30)
        run(tab,"systemctl --user daemon-reload && systemctl --user enable --now labshield-sync.timer",30)
        run(tab,"systemctl --user is-active labshield-sync.timer || true",30)

        print("  Installing cloudflared (no sudo)...")
        install_cf='command -v cloudflared || (curl -fsSL https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64 -o /tmp/cloudflared && chmod +x /tmp/cloudflared && mkdir -p ~/.local/bin && cp /tmp/cloudflared ~/.local/bin/cloudflared && echo "export PATH=\\$PATH:\\$HOME/.local/bin" >> ~/.bashrc && export PATH="$PATH:$HOME/.local/bin") || true'
        run(tab,install_cf,300)
        run(tab,"cloudflared tunnel --config ~/.cloudflared/config.yml run > /tmp/carlotab-tunnel.log 2>&1 &",30)
        print("  Tunnel starting...")
    finally: tab.close()

    print()
    print("=== DEPLOY COMPLETE ===")
    print("LabShield running on carlotab: http://100.107.208.119:5000")
    print("Cloudflare tunnel on carlotab starting up")
    print("Data sync timer active (every 5 min)")
    print()
    print("Next: add carlotab connector in Cloudflare Dashboard")
    print("  Zero Trust > Networks > Tunnels > Portfolio")
    print("  Should show 2 active connectors")

if __name__=="__main__":
    main()
