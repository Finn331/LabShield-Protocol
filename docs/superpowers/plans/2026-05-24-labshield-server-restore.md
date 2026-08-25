# LabShield Server Restore Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the LabShield backend, web dashboard, domain proxy, HTTPS, and Unity API integration on server `2.27.165.46`.

**Architecture:** Use the existing `Server/restore_with_paramiko.py` deployment script to package the local `Server` folder, upload it to `/opt/labshield-server`, run Docker Compose, configure Nginx for `labshieldprotocol.my.id`, and request a Let's Encrypt certificate. Keep runtime data under `/opt/labshield-server/data` so user and score JSON files survive redeploys.

**Tech Stack:** Windows PowerShell 5.1, Python 3, Paramiko, SSH, Docker, Docker Compose plugin, Nginx, Certbot, Node.js Express.

---

## Files And Responsibilities

- `Server/restore_with_paramiko.py`: primary restore/deploy automation over SSH using environment variables for host, user, password, domain, remote directory, and SSL behavior.
- `Server/server.js`: Express API and static web server that handles auth, registration, score submission, dashboards, and leaderboard endpoints.
- `Server/public/*`: web pages served by the backend.
- `Server/docker-compose.yml`: container runtime config, port mapping, and persistent `./data:/app/data` mount.
- `Server/Dockerfile`: builds the Node.js backend image.
- `Assets/Scripts/Networking/AuthManager.cs`: Unity login/register API base URL, already points to `http://2.27.165.46:5000/api`.
- `Assets/Scripts/Networking/NetworkManager.cs`: Unity score submission URL, already points to `http://2.27.165.46:5000/api/submit-score`.

## Pre-Execution Rules

- Do not write SSH credentials to repository files.
- Use process environment variables only for secrets.
- Do not change Unity URLs unless validation proves the restored endpoint is incompatible.
- After restore, tell the owner to rotate the root password because it was shared in chat.

### Task 1: Verify Local Restore Assets

**Files:**
- Read: `Server/restore_with_paramiko.py`
- Read: `Server/server.js`
- Read: `Server/package.json`
- Read: `Server/docker-compose.yml`
- Read: `Server/Dockerfile`
- Read: `Server/public/index.html`
- Read: `Server/public/register.html`
- Read: `Server/public/dashboard.html`

- [ ] **Step 1: Confirm required server files exist**

Run from repo root:

```powershell
Test-Path -LiteralPath "Server\restore_with_paramiko.py"
Test-Path -LiteralPath "Server\server.js"
Test-Path -LiteralPath "Server\package.json"
Test-Path -LiteralPath "Server\docker-compose.yml"
Test-Path -LiteralPath "Server\Dockerfile"
Test-Path -LiteralPath "Server\public\index.html"
Test-Path -LiteralPath "Server\public\register.html"
Test-Path -LiteralPath "Server\public\dashboard.html"
```

Expected: each command prints `True`.

- [ ] **Step 2: Confirm deployment dependency is available**

Run from repo root:

```powershell
python -c "import paramiko; print(paramiko.__version__)"
```

Expected: prints a Paramiko version number. If it fails with `ModuleNotFoundError`, run:

```powershell
python -m pip install paramiko
```

Expected: pip installs or reports `Requirement already satisfied`.

- [ ] **Step 3: Confirm Unity API URLs already match the target server**

Run from repo root:

```powershell
rg "2\.27\.165\.46:5000" "Assets\Scripts\Networking"
```

Expected: matches in `AuthManager.cs` and `NetworkManager.cs`.

### Task 2: Run Automated Server Restore

**Files:**
- Execute: `Server/restore_with_paramiko.py`
- Remote create/update: `/opt/labshield-server`
- Remote create/update: `/etc/nginx/sites-available/labshield`

- [ ] **Step 1: Set non-secret deployment environment variables**

Run from repo root in the same PowerShell session that will execute the deploy:

```powershell
$env:LABSHIELD_SSH_HOST = "2.27.165.46"
$env:LABSHIELD_SSH_USER = "root"
$env:LABSHIELD_REMOTE_DIR = "/opt/labshield-server"
$env:LABSHIELD_DOMAIN = "labshieldprotocol.my.id"
$env:LABSHIELD_SKIP_SSL = "false"
```

Expected: no output.

- [ ] **Step 2: Set the SSH password in process memory only**

Run in the same PowerShell session:

```powershell
$env:LABSHIELD_SSH_PASSWORD = Read-Host "LabShield SSH password"
```

Expected: PowerShell prompts for the password and does not write it to disk.

- [ ] **Step 3: Execute the restore script**

Run from repo root:

```powershell
python "Server\restore_with_paramiko.py"
```

Expected output includes these lines or equivalent progress:

```text
Archive created:
==> Installing base packages
==> Deploying application files
==> Starting LabShield Docker service
==> Configuring Nginx reverse proxy
==> Attempting Let's Encrypt SSL setup
==> Health checks
==> Restore complete
```

- [ ] **Step 4: If Certbot fails but health checks pass, keep the app running**

Expected fallback behavior: the script prints a warning similar to:

```text
WARNING: SSL setup failed. Check DNS for labshieldprotocol.my.id, then rerun certbot manually.
```

Do not roll back the deploy. Continue to Task 3 to verify HTTP and direct backend access, then report the SSL-specific failure.

### Task 3: Verify Remote Runtime State

**Files:**
- Remote inspect: `/opt/labshield-server/docker-compose.yml`
- Remote inspect: `/opt/labshield-server/data/users.json`
- Remote inspect: `/opt/labshield-server/data/student_scores.json`

- [ ] **Step 1: Check the backend API from the local machine**

Run from repo root:

```powershell
Invoke-WebRequest -Uri "http://2.27.165.46:5000/api/scores" -UseBasicParsing -TimeoutSec 15
```

Expected: HTTP status `200` and a JSON body, usually `[]` on a clean restore.

- [ ] **Step 2: Check the register page over HTTPS**

Run from repo root:

```powershell
Invoke-WebRequest -Uri "https://labshieldprotocol.my.id/register.html" -UseBasicParsing -TimeoutSec 20
```

Expected: HTTP status `200` and HTML content for the registration page.

- [ ] **Step 3: Check the teacher dashboard over HTTPS**

Run from repo root:

```powershell
Invoke-WebRequest -Uri "https://labshieldprotocol.my.id/dashboard.html" -UseBasicParsing -TimeoutSec 20
```

Expected: HTTP status `200` and HTML content for the dashboard page.

- [ ] **Step 4: Check remote Docker container status**

Run from repo root:

```powershell
ssh -o StrictHostKeyChecking=accept-new root@2.27.165.46 "cd /opt/labshield-server && docker compose ps"
```

Expected: output includes `labshield-server` with a running status.

- [ ] **Step 5: Check remote data files exist**

Run from repo root:

```powershell
ssh -o StrictHostKeyChecking=accept-new root@2.27.165.46 "test -f /opt/labshield-server/data/users.json && test -f /opt/labshield-server/data/student_scores.json && echo data-ok"
```

Expected:

```text
data-ok
```

### Task 4: Verify Game API Integration With A Safe Test Request

**Files:**
- Read: `Assets/Scripts/Networking/AuthManager.cs`
- Read: `Assets/Scripts/Networking/NetworkManager.cs`
- Remote writes: `/opt/labshield-server/data/student_scores.json` via public API test submission

- [ ] **Step 1: Submit one test score through the same endpoint Unity uses**

Run from repo root:

```powershell
$body = @{
  studentName = "HealthCheck"
  apdTotalCorrect = 1
  apdTotalWrong = 0
  apdTimeTakenSeconds = 1
  quizTotalCorrect = 1
  quizTotalWrong = 0
  questionTimes = @(@{ questionID = "health-check"; timeTaken = 1; isCorrect = $true })
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Uri "http://2.27.165.46:5000/api/submit-score" -Method Post -ContentType "application/json" -Body $body
```

Expected JSON response includes:

```json
{
  "message": "Score saved",
  "finalScore": 100,
  "finalScoreStandard": 100,
  "finalScoreK3": 100
}
```

- [ ] **Step 2: Confirm the test score is readable**

Run from repo root:

```powershell
Invoke-RestMethod -Uri "http://2.27.165.46:5000/api/student-scores/HealthCheck"
```

Expected: JSON array with at least one object where `studentName` is `HealthCheck`.

- [ ] **Step 3: Remove the health-check score from persisted data**

Run from repo root:

```powershell
ssh -o StrictHostKeyChecking=accept-new root@2.27.165.46 "node -e \"const fs=require('fs'); const p='/opt/labshield-server/data/student_scores.json'; const rows=JSON.parse(fs.readFileSync(p,'utf8')); fs.writeFileSync(p, JSON.stringify(rows.filter(r => String(r.studentName || '').toLowerCase() !== 'healthcheck'), null, 2)); console.log('health-check-removed');\""
```

Expected:

```text
health-check-removed
```

- [ ] **Step 4: Confirm the health-check score was removed**

Run from repo root:

```powershell
Invoke-RestMethod -Uri "http://2.27.165.46:5000/api/student-scores/HealthCheck"
```

Expected: empty JSON array `[]`.

### Task 5: Final Security And Operational Notes

**Files:**
- No code changes.

- [ ] **Step 1: Clear local process secret**

Run in the deployment PowerShell session:

```powershell
Remove-Item Env:\LABSHIELD_SSH_PASSWORD
```

Expected: no output.

- [ ] **Step 2: Report final URLs**

Include these URLs in the final status message:

```text
Web register: https://labshieldprotocol.my.id/register.html
Teacher dashboard: https://labshieldprotocol.my.id/dashboard.html
Learning media: https://labshieldprotocol.my.id/learning-media.html
API scores: http://2.27.165.46:5000/api/scores
Unity login API base: http://2.27.165.46:5000/api
Unity score API: http://2.27.165.46:5000/api/submit-score
```

- [ ] **Step 3: Report password rotation requirement**

Final status must include:

```text
Rotate the server root password after this restore because it was shared in chat.
```

## Plan Self-Review

- Spec coverage: deployment, Docker, Nginx, SSL, data persistence, Unity URL validation, health checks, and credential handling are covered by Tasks 1-5.
- Placeholder scan: no `TBD`, `TODO`, or undefined implementation steps remain.
- Type consistency: URLs, file paths, environment variables, and endpoint names match the current repository files and approved design.
