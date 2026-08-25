import paramiko, os, time

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)

def run(cmd, t=60):
    i, o, e = c.exec_command(cmd, timeout=t)
    return o.read().decode(errors="replace")

# 1. Fix creds
print(run("rm -f /home/carlo/.cloudflared/creds.json && ln -s /home/carlo/.cloudflared/f0d6194e-1425-41b2-a526-d97025e5a040.json /home/carlo/.cloudflared/creds.json; echo DONE"))

# 2. Test read
print(run("ls -la /home/carlo/.cloudflared/creds.json && head -c 80 /home/carlo/.cloudflared/creds.json"))

# 3. Kill old tunnel, remove log, start
print(run("pkill -f 'cloudflared tunnel' 2>/dev/null; sleep 1; rm -f /tmp/carlotab-tunnel.log; echo KILLED"))

print(run("nohup /home/carlo/.local/bin/cloudflared tunnel --config /home/carlo/.cloudflared/config.yml run > /tmp/carlotab-tunnel.log 2>&1 & echo LAUNCHED"))

time.sleep(8)

# 4. Check
print(run("pgrep -af cloudflared | grep -v bash | grep -v login || echo NO_TUNNEL"))
print(run("head -n 30 /tmp/carlotab-tunnel.log"))

c.close()
