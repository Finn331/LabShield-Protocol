import paramiko, os, time

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)
try:
    stdin, stdout, stderr = client.exec_command(
        "pkill -f 'cloudflared tunnel' 2>/dev/null; "
        "nohup /home/carlo/.local/bin/cloudflared tunnel "
        "--config /home/carlo/.cloudflared/config.yml run "
        ">> /tmp/carlotab-tunnel.log 2>&1 & sleep 5; "
        "echo PID=$(pgrep -f 'cloudflared tunnel' | grep -v bash | grep -v login)",
        timeout=60,
    )
    print(stdout.read().decode(errors="replace"))
    print(stderr.read().decode(errors="replace"))

    time.sleep(3)
    stdin, stdout, stderr = client.exec_command("tail -n 15 /tmp/carlotab-tunnel.log", timeout=30)
    print("---LOG---")
    print(stdout.read().decode(errors="replace"))
finally:
    client.close()
