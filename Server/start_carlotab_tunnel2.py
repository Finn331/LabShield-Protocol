import paramiko, os, time

pw = os.environ.get("LABSHIELD_TAB_PASSWORD", "aloganteng")
client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("100.107.208.119", username="carlo", password=pw, timeout=20, look_for_keys=False, allow_agent=False)
try:
    checks = [
        "echo CONF_FILE; cat /home/carlo/.cloudflared/config.yml",
        "echo CREDS_LINK; ls -la /home/carlo/.cloudflared/creds.json",
        "echo CREDS_CONTENT; head -c 100 /home/carlo/.cloudflared/creds.json 2>/dev/null || echo BAD",
    ]
    for cmd in checks:
        i, o, e = client.exec_command(cmd, timeout=30)
        print(o.read().decode(errors="replace").strip()[:200])
        print()

    # Start fresh
    start = (
        "rm -f /tmp/carlotab-tunnel.log && "
        "nohup /home/carlo/.local/bin/cloudflared tunnel "
        "--config /home/carlo/.cloudflared/config.yml run "
        "> /tmp/carlotab-tunnel.log 2>&1 &"
    )
    i, o, e = client.exec_command(start, timeout=30)
    print("Start output:", o.read().decode(errors="replace").strip())
    time.sleep(5)

    i, o, e = client.exec_command(
        "echo STATUS; ps aux | grep cloudflared | grep -v grep | head -n 2; "
        "echo LOG; cat /tmp/carlotab-tunnel.log",
        timeout=30,
    )
    output = o.read().decode(errors="replace")
    print(output[:1000])
finally:
    client.close()
