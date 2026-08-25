# Home Lab Cloudflare Tunnel Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move LabShield from the VPS default target to the home lab server at `192.168.100.142` and expose it publicly through Cloudflare Tunnel at `https://labshieldprotocol.my.id`.

**Architecture:** Keep the existing Node/Express Docker service and persistent JSON data model. Update local Unity endpoints to the public HTTPS domain, update Paramiko helper defaults to the home lab account, and add a focused home-lab deploy/tunnel script that uses sudo for package and service setup. Cloudflare Tunnel will proxy the public hostname directly to `http://127.0.0.1:5000` on the home lab server.

**Tech Stack:** Unity 6 C#, Unity MCP, Node.js/Express, Docker Compose, Python Paramiko, Cloudflare `cloudflared`, systemd.

---

## File Structure

- Modify: `Assets/Scripts/Networking/AuthManager.cs` to use `https://labshieldprotocol.my.id/api` and keep registration on HTTPS.
- Modify: `Assets/Scripts/Networking/NetworkManager.cs` to submit scores to `https://labshieldprotocol.my.id/api/submit-score`.
- Modify: `Server/restore_with_paramiko.py` so default SSH target is home lab and remote privileged commands run with sudo.
- Modify: `Server/quick_restore_paramiko.py`, `Server/configure_smtp_paramiko.py`, `Server/configure_labshield_nginx_paramiko.py`, `Server/run_vps_command_paramiko.py`, `Server/remote_status.py`, `Server/check_vps_paramiko.py`, `Server/diagnose_otp_paramiko.py`, `Server/verify_otp_origin_paramiko.py`, `Server/inspect_live_database_paramiko.py`, and `Server/seed_students_paramiko.py` so their default host/user target the home lab.
- Create: `Server/configure_cloudflare_tunnel_paramiko.py` to install/configure `cloudflared` and create a systemd service using a tunnel token supplied at runtime.
- Create: `Server/test_home_lab_config.py` to pin expected home-lab defaults and Unity HTTPS endpoints.

---

### Task 1: Add Home Lab Configuration Regression Test

**Files:**
- Create: `Server/test_home_lab_config.py`
- Read: `Assets/Scripts/Networking/AuthManager.cs`
- Read: `Assets/Scripts/Networking/NetworkManager.cs`
- Read: `Server/restore_with_paramiko.py`

- [ ] **Step 1: Create the failing test**

Create `Server/test_home_lab_config.py` with this content:

```python
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "Server"
ASSETS = ROOT / "Assets" / "Scripts" / "Networking"


def read(path):
    return path.read_text(encoding="utf-8")


def test_unity_uses_public_https_domain():
    auth_manager = read(ASSETS / "AuthManager.cs")
    network_manager = read(ASSETS / "NetworkManager.cs")

    assert 'private string baseUrl = "https://labshieldprotocol.my.id/api";' in auth_manager
    assert 'private string registerUrl = "https://labshieldprotocol.my.id/register.html";' in auth_manager
    assert '[SerializeField] private string serverUrl = "https://labshieldprotocol.my.id/api/submit-score";' in network_manager
    assert "http://2.27.165.46:5000" not in auth_manager
    assert "http://2.27.165.46:5000" not in network_manager


def test_paramiko_defaults_target_home_lab():
    restore_script = read(SERVER / "restore_with_paramiko.py")

    assert 'LABSHIELD_SSH_HOST", "192.168.100.142"' in restore_script
    assert 'LABSHIELD_SSH_USER", "carloserver"' in restore_script
    assert 'sudo -S' in restore_script
    assert "cloudflared" in restore_script


def test_helper_scripts_no_longer_default_to_vps_root():
    helper_names = [
        "quick_restore_paramiko.py",
        "configure_smtp_paramiko.py",
        "configure_labshield_nginx_paramiko.py",
        "run_vps_command_paramiko.py",
        "remote_status.py",
        "check_vps_paramiko.py",
        "diagnose_otp_paramiko.py",
        "verify_otp_origin_paramiko.py",
        "inspect_live_database_paramiko.py",
        "seed_students_paramiko.py",
    ]

    for helper_name in helper_names:
        text = read(SERVER / helper_name)
        assert '2.27.165.46' not in text, helper_name
        assert 'LABSHIELD_SSH_HOST", "192.168.100.142"' in text or 'HOST = "192.168.100.142"' in text, helper_name
        assert 'LABSHIELD_SSH_USER", "carloserver"' in text or 'USER = "carloserver"' in text or 'username = os.environ.get("LABSHIELD_SSH_USER", "carloserver")' in text, helper_name
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `python -m pytest Server/test_home_lab_config.py -q`

Expected: FAIL because Unity still has the old HTTP VPS endpoint, `restore_with_paramiko.py` still defaults to the VPS/root flow, and helper scripts still mention `2.27.165.46`.

---

### Task 2: Update Unity Networking Endpoints

**Files:**
- Modify: `Assets/Scripts/Networking/AuthManager.cs:8-9`
- Modify: `Assets/Scripts/Networking/NetworkManager.cs:9`
- Test: `Server/test_home_lab_config.py`

- [ ] **Step 1: Update `AuthManager.cs`**

Replace the URL fields with:

```csharp
private string baseUrl = "https://labshieldprotocol.my.id/api";
private string registerUrl = "https://labshieldprotocol.my.id/register.html";
```

- [ ] **Step 2: Update `NetworkManager.cs`**

Replace the score URL field with:

```csharp
[SerializeField] private string serverUrl = "https://labshieldprotocol.my.id/api/submit-score";
```

- [ ] **Step 3: Verify the endpoint test partially passes**

Run: `python -m pytest Server/test_home_lab_config.py::test_unity_uses_public_https_domain -q`

Expected: PASS.

- [ ] **Step 4: Use Unity MCP to check editor and console state**

Run Unity MCP calls:

```text
manage_scene(action="get_active")
read_console(action="get", types=["error", "warning"], count="10", format="detailed", include_stacktrace=false)
```

Expected: Unity MCP responds. Existing unrelated Unity AI Toolkit or shadow warnings may remain, but no new C# compile errors should appear from `AuthManager.cs` or `NetworkManager.cs`.

---

### Task 3: Add Cloudflare Tunnel Configuration Script

**Files:**
- Create: `Server/configure_cloudflare_tunnel_paramiko.py`
- Test: `Server/test_home_lab_config.py`

- [ ] **Step 1: Create `configure_cloudflare_tunnel_paramiko.py`**

Create this file:

```python
import os
import sys
import time

import paramiko


HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ.get("LABSHIELD_SSH_PASSWORD")
TUNNEL_TOKEN = os.environ.get("LABSHIELD_CLOUDFLARE_TUNNEL_TOKEN")


def connect():
    if not PASSWORD:
        raise RuntimeError("Set LABSHIELD_SSH_PASSWORD before running this script.")

    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(
        hostname=HOST,
        username=USER,
        password=PASSWORD,
        timeout=20,
        auth_timeout=20,
        banner_timeout=20,
        look_for_keys=False,
        allow_agent=False,
    )
    return client


def exec_checked(client, command, timeout=None):
    print(f"==> {command.splitlines()[0][:100]}")
    stdin, stdout, stderr = client.exec_command(command, timeout=timeout)
    channel = stdout.channel

    while not channel.exit_status_ready():
        while channel.recv_ready():
            sys.stdout.write(channel.recv(4096).decode(errors="replace"))
            sys.stdout.flush()
        while channel.recv_stderr_ready():
            sys.stderr.write(channel.recv_stderr(4096).decode(errors="replace"))
            sys.stderr.flush()
        time.sleep(0.2)

    out = stdout.read().decode(errors="replace")
    err = stderr.read().decode(errors="replace")
    if out:
        print(out, end="")
    if err:
        print(err, end="", file=sys.stderr)

    status = channel.recv_exit_status()
    if status != 0:
        raise RuntimeError(f"Remote command failed with exit code {status}: {command}")
    return out


def sudo_script(script):
    escaped_password = PASSWORD.replace("'", "'\\''")
    escaped_script = script.replace("'", "'\\''")
    return f"printf '%s\\n' '{escaped_password}' | sudo -S bash -lc '{escaped_script}'"


def main():
    if not TUNNEL_TOKEN:
        raise RuntimeError("Set LABSHIELD_CLOUDFLARE_TUNNEL_TOKEN with the Cloudflare Tunnel token.")

    escaped_token = TUNNEL_TOKEN.replace("'", "'\\''")
    script = f"""
set -e
export DEBIAN_FRONTEND=noninteractive

if ! command -v cloudflared >/dev/null 2>&1; then
    mkdir -p /usr/share/keyrings
    curl -fsSL https://pkg.cloudflare.com/cloudflare-main.gpg -o /usr/share/keyrings/cloudflare-main.gpg
    echo 'deb [signed-by=/usr/share/keyrings/cloudflare-main.gpg] https://pkg.cloudflare.com/cloudflared any main' > /etc/apt/sources.list.d/cloudflared.list
    apt-get update
    apt-get install -y cloudflared
fi

cloudflared service uninstall >/dev/null 2>&1 || true
cloudflared service install '{escaped_token}'
systemctl enable cloudflared
systemctl restart cloudflared
systemctl --no-pager --full status cloudflared || true
"""

    client = connect()
    try:
        exec_checked(client, sudo_script(script), timeout=600)
        exec_checked(client, "curl -fsS http://127.0.0.1:5000/api/scores >/dev/null", timeout=30)
    finally:
        client.close()

    print("Cloudflare Tunnel configuration complete.")


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: Run syntax check**

Run: `python -m py_compile Server/configure_cloudflare_tunnel_paramiko.py`

Expected: PASS with no output.

---

### Task 4: Update Home Lab Deploy Defaults and Sudo Flow

**Files:**
- Modify: `Server/restore_with_paramiko.py`
- Test: `Server/test_home_lab_config.py`

- [ ] **Step 1: Change default target constants**

In `Server/restore_with_paramiko.py`, set:

```python
HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
PASSWORD = os.environ.get("LABSHIELD_SSH_PASSWORD")
REMOTE_DIR = os.environ.get("LABSHIELD_REMOTE_DIR", "/opt/labshield-server")
DOMAIN = os.environ.get("LABSHIELD_DOMAIN", "labshieldprotocol.my.id")
SKIP_SSL = os.environ.get("LABSHIELD_SKIP_SSL", "true").lower() in {"1", "true", "yes"}
EXCLUDE_VIDEOS = os.environ.get("LABSHIELD_EXCLUDE_VIDEOS", "false").lower() in {"1", "true", "yes"}
```

- [ ] **Step 2: Add sudo command wrapper**

Add this helper after `exec_checked`:

```python
def sudo_command(command):
    escaped_password = PASSWORD.replace("'", "'\\''")
    escaped_command = command.replace("'", "'\\''")
    return f"printf '%s\\n' '{escaped_password}' | sudo -S bash -lc '{escaped_command}'"
```

- [ ] **Step 3: Remove Nginx/Certbot from remote restore script**

In `remote_restore_script()`, keep package install, Docker install, file extraction, compose startup, local health checks, and add `cloudflared` package verification. The body should include these commands instead of Nginx/Certbot setup:

```sh
echo "==> Installing base packages"
apt-get update
apt-get install -y ca-certificates curl gnupg lsb-release tar

if ! command -v docker >/dev/null 2>&1; then
    echo "==> Installing Docker"
    curl -fsSL https://get.docker.com | sh
fi

if ! docker compose version >/dev/null 2>&1; then
    echo "==> Installing Docker Compose plugin"
    apt-get install -y docker-compose-plugin
fi

if ! command -v cloudflared >/dev/null 2>&1; then
    echo "==> cloudflared is not installed yet. Run configure_cloudflare_tunnel_paramiko.py with LABSHIELD_CLOUDFLARE_TUNNEL_TOKEN after deploy."
fi
```

Keep the Docker startup and health check:

```sh
echo "==> Starting LabShield Docker service"
cd "$REMOTE_DIR"
docker compose up -d --build --remove-orphans

echo "==> Health checks"
curl -fsS http://127.0.0.1:5000/register.html >/dev/null
curl -fsS http://127.0.0.1:5000/api/scores >/dev/null
docker compose ps
```

- [ ] **Step 4: Execute remote restore script with sudo**

Replace the remote execution line:

```python
exec_checked(client, f"bash {remote_script}", timeout=1800)
```

with:

```python
exec_checked(client, sudo_command(f"bash {remote_script}"), timeout=1800)
```

- [ ] **Step 5: Run syntax check**

Run: `python -m py_compile Server/restore_with_paramiko.py`

Expected: PASS with no output.

---

### Task 5: Update Remaining Paramiko Helper Defaults

**Files:**
- Modify: `Server/quick_restore_paramiko.py`
- Modify: `Server/configure_smtp_paramiko.py`
- Modify: `Server/configure_labshield_nginx_paramiko.py`
- Modify: `Server/run_vps_command_paramiko.py`
- Modify: `Server/remote_status.py`
- Modify: `Server/check_vps_paramiko.py`
- Modify: `Server/diagnose_otp_paramiko.py`
- Modify: `Server/verify_otp_origin_paramiko.py`
- Modify: `Server/inspect_live_database_paramiko.py`
- Modify: `Server/seed_students_paramiko.py`
- Test: `Server/test_home_lab_config.py`

- [ ] **Step 1: Replace hardcoded/default host**

In every listed helper, replace the default or hardcoded host with:

```python
"192.168.100.142"
```

For scripts using environment defaults, the line should be:

```python
HOST = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
```

or for lowercase variable style:

```python
host = os.environ.get("LABSHIELD_SSH_HOST", "192.168.100.142")
```

- [ ] **Step 2: Replace root default user**

For scripts that define an SSH user, set:

```python
USER = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
```

or for lowercase variable style:

```python
username = os.environ.get("LABSHIELD_SSH_USER", "carloserver")
```

If a script has `USER = "root"`, replace it with:

```python
USER = "carloserver"
```

- [ ] **Step 3: Run the helper default test**

Run: `python -m pytest Server/test_home_lab_config.py::test_helper_scripts_no_longer_default_to_vps_root -q`

Expected: PASS.

---

### Task 6: Run Local Verification Before Remote Deploy

**Files:**
- Test: `Server/test_home_lab_config.py`
- Test: `Server/test_learning_media.py`
- Test: `Server/test_compose_config.py`
- Test: `Server/test_csv_export.py`
- Read: `Server/server.js`

- [ ] **Step 1: Run Node syntax check**

Run: `node --check Server/server.js`

Expected: PASS with no output.

- [ ] **Step 2: Run Python regression tests**

Run: `python -m pytest Server/test_home_lab_config.py Server/test_learning_media.py Server/test_compose_config.py Server/test_csv_export.py -q`

Expected: PASS for all tests.

- [ ] **Step 3: Search for old VPS endpoint in runtime code**

Run: `rg "2\.27\.165\.46|http://2\.27\.165\.46:5000" Assets Server --glob "!node_modules/**"`

Expected: No runtime endpoint references remain. Spec or historical plan references are acceptable only under `docs/`.

---

### Task 7: Deploy to Home Lab and Configure Tunnel

**Files:**
- Execute: `Server/restore_with_paramiko.py`
- Execute: `Server/configure_cloudflare_tunnel_paramiko.py`
- Read: `Server/docker-compose.yml`

- [ ] **Step 1: Set local environment variables for SSH**

In PowerShell, set the password only in the current process:

```powershell
$env:LABSHIELD_SSH_HOST = "192.168.100.142"
$env:LABSHIELD_SSH_USER = "carloserver"
$env:LABSHIELD_SSH_PASSWORD = "<provided password>"
```

Expected: variables exist only for the current shell session.

- [ ] **Step 2: Deploy the server to home lab**

Run: `python Server/restore_with_paramiko.py`

Expected: archive upload completes, Docker Compose builds/starts `labshield-server`, and local remote health checks for `/register.html` and `/api/scores` pass.

- [ ] **Step 3: Configure Cloudflare Tunnel**

Set the Cloudflare tunnel token in the current PowerShell process:

```powershell
$env:LABSHIELD_CLOUDFLARE_TUNNEL_TOKEN = "<cloudflare tunnel token>"
```

Run: `python Server/configure_cloudflare_tunnel_paramiko.py`

Expected: `cloudflared` installs if needed, systemd service is installed/restarted, and local `/api/scores` health check passes.

- [ ] **Step 4: Verify remote service status**

Run: `python Server/remote_status.py`

Expected: Docker reports `labshield-server` running and `curl http://127.0.0.1:5000/api/scores` returns JSON.

---

### Task 8: Verify Public Domain and Unity MCP

**Files:**
- Read: `Assets/Scripts/Networking/AuthManager.cs`
- Read: `Assets/Scripts/Networking/NetworkManager.cs`

- [ ] **Step 1: Verify public HTTP endpoints**

Run:

```powershell
curl.exe -fsS -I "https://labshieldprotocol.my.id/"
curl.exe -fsS -I "https://labshieldprotocol.my.id/register.html"
curl.exe -fsS "https://labshieldprotocol.my.id/api/scores"
```

Expected: homepage and registration page return HTTP 200 headers, and `/api/scores` returns JSON.

- [ ] **Step 2: Check Unity MCP active scene**

Run Unity MCP call:

```text
manage_scene(action="get_active")
```

Expected: Unity returns the active scene, such as `MainMenu`.

- [ ] **Step 3: Check Unity console after script changes**

Run Unity MCP call:

```text
read_console(action="get", types=["error", "warning"], count="10", format="detailed", include_stacktrace=false)
```

Expected: no C# compile errors related to `AuthManager.cs` or `NetworkManager.cs`. Existing unrelated warnings can be reported separately.

---

### Task 9: Final Review

**Files:**
- Read: `git diff -- Assets/Scripts/Networking/AuthManager.cs Assets/Scripts/Networking/NetworkManager.cs Server docs/superpowers`

- [ ] **Step 1: Inspect git status**

Run: `git status --short`

Expected: only intended files are modified/added.

- [ ] **Step 2: Inspect diff**

Run: `git diff -- Assets/Scripts/Networking/AuthManager.cs Assets/Scripts/Networking/NetworkManager.cs Server docs/superpowers`

Expected: changes match this plan, no secrets are committed, and password/token values do not appear in tracked files.

- [ ] **Step 3: Summarize outcome**

Report:

```text
- Unity endpoints now use https://labshieldprotocol.my.id.
- Server deploy defaults now target 192.168.100.142 as carloserver.
- Docker service status on home lab.
- Cloudflare Tunnel service status.
- Public endpoint verification results.
- Unity MCP console verification results.
```

---

## Self-Review

- Spec coverage: The plan covers home lab SSH defaults, Docker deployment, Cloudflare Tunnel, Unity endpoint updates, secret handling, public verification, and Unity MCP verification.
- Placeholder scan: The only angle-bracket values are runtime secrets intentionally supplied through environment variables, not committed content.
- Type consistency: Python variable names and Unity C# field declarations match existing files and tests.
