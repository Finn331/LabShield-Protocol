param(
    [string]$HostName = "2.27.165.46",
    [string]$User = "root",
    [string]$RemoteDir = "/opt/labshield-server",
    [string]$Domain = "labshieldprotocol.my.id",
    [switch]$SkipSsl
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$archive = Join-Path ([System.IO.Path]::GetTempPath()) "labshield-server-restore.tar.gz"
$remoteArchive = "/tmp/labshield-server-restore.tar.gz"

function Invoke-CheckedCommand {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$StepName
    )

    Write-Host "==> $StepName"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$StepName failed with exit code $LASTEXITCODE"
    }
}

foreach ($requiredFile in @("server.js", "package.json", "docker-compose.yml", "Dockerfile")) {
    $path = Join-Path $scriptDir $requiredFile
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required file: $path"
    }
}

$publicDir = Join-Path $scriptDir "public"
if (-not (Test-Path -LiteralPath $publicDir)) {
    throw "Missing web folder: $publicDir"
}

if (Test-Path -LiteralPath $archive) {
    Remove-Item -LiteralPath $archive -Force
}

$tarArgs = @(
    "-czf", $archive,
    "--exclude", "Server/node_modules",
    "--exclude", "Server/data",
    "-C", $repoRoot,
    "Server"
)
Invoke-CheckedCommand -FilePath "tar" -Arguments $tarArgs -StepName "Packing Server folder"

$remoteSetup = @'
set -e
export DEBIAN_FRONTEND=noninteractive

REMOTE_DIR="__REMOTE_DIR__"
DOMAIN="__DOMAIN__"
SKIP_SSL="__SKIP_SSL__"
ARCHIVE="__REMOTE_ARCHIVE__"

echo "==> Installing base packages"
apt-get update
apt-get install -y ca-certificates curl gnupg lsb-release tar nginx

if ! command -v docker >/dev/null 2>&1; then
    echo "==> Installing Docker"
    curl -fsSL https://get.docker.com | sh
fi

if ! docker compose version >/dev/null 2>&1; then
    echo "==> Installing Docker Compose plugin"
    apt-get install -y docker-compose-plugin
fi

echo "==> Deploying application files"
mkdir -p "$REMOTE_DIR"
tar -xzf "$ARCHIVE" -C "$REMOTE_DIR" --strip-components=1
mkdir -p "$REMOTE_DIR/data"

if [ -f "$REMOTE_DIR/users.json" ]; then
    cp "$REMOTE_DIR/users.json" "$REMOTE_DIR/data/users.json"
fi

if [ -f "$REMOTE_DIR/student_scores.json" ]; then
    cp "$REMOTE_DIR/student_scores.json" "$REMOTE_DIR/data/student_scores.json"
fi

chmod +x "$REMOTE_DIR/start.sh" "$REMOTE_DIR/restart.sh" 2>/dev/null || true

echo "==> Starting LabShield Docker service"
cd "$REMOTE_DIR"
docker compose up -d --build --remove-orphans

echo "==> Configuring Nginx reverse proxy"
cat > /etc/nginx/sites-available/labshield <<EOF
server {
    listen 80;
    server_name $DOMAIN;

    client_max_body_size 50m;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }
}
EOF

ln -sf /etc/nginx/sites-available/labshield /etc/nginx/sites-enabled/labshield
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl enable nginx
systemctl restart nginx

if [ "$SKIP_SSL" != "true" ]; then
    echo "==> Attempting Let's Encrypt SSL setup"
    apt-get install -y certbot python3-certbot-nginx
    if certbot --nginx -d "$DOMAIN" --non-interactive --agree-tos --register-unsafely-without-email --redirect; then
        systemctl reload nginx
    else
        echo "WARNING: SSL setup failed. Check that DNS for $DOMAIN points to this server, then rerun certbot manually."
    fi
fi

echo "==> Health checks"
curl -fsS http://127.0.0.1:5000/register.html >/dev/null
curl -fsS http://127.0.0.1:5000/api/scores >/dev/null
docker compose ps

echo "==> Restore complete"
echo "Backend: http://__HOST_NAME__:5000/api"
echo "Web:     http://$DOMAIN/register.html"
if [ "$SKIP_SSL" != "true" ]; then
    echo "HTTPS:   https://$DOMAIN/register.html"
fi
'@

$skipSslValue = if ($SkipSsl) { "true" } else { "false" }
$remoteSetup = $remoteSetup.Replace("__REMOTE_DIR__", $RemoteDir)
$remoteSetup = $remoteSetup.Replace("__DOMAIN__", $Domain)
$remoteSetup = $remoteSetup.Replace("__SKIP_SSL__", $skipSslValue)
$remoteSetup = $remoteSetup.Replace("__REMOTE_ARCHIVE__", $remoteArchive)
$remoteSetup = $remoteSetup.Replace("__HOST_NAME__", $HostName)

$remoteScript = Join-Path ([System.IO.Path]::GetTempPath()) "labshield-remote-restore.sh"
Set-Content -LiteralPath $remoteScript -Value $remoteSetup -NoNewline -Encoding UTF8

$target = "$User@$HostName"

Invoke-CheckedCommand -FilePath "ssh" -Arguments @("-o", "StrictHostKeyChecking=accept-new", $target, "mkdir -p /tmp") -StepName "Preparing remote server"
Invoke-CheckedCommand -FilePath "scp" -Arguments @($archive, "$target`:$remoteArchive") -StepName "Uploading Server archive"
Invoke-CheckedCommand -FilePath "scp" -Arguments @($remoteScript, "$target`:/tmp/labshield-remote-restore.sh") -StepName "Uploading remote restore script"
Invoke-CheckedCommand -FilePath "ssh" -Arguments @($target, "bash /tmp/labshield-remote-restore.sh") -StepName "Running remote restore"

Write-Host "==> Local verification"
try {
    Invoke-WebRequest -Uri "http://$HostName`:5000/api/scores" -UseBasicParsing -TimeoutSec 10 | Out-Null
    Write-Host "Backend reachable: http://$HostName`:5000/api/scores"
} catch {
    Write-Warning "Backend verification from this PC failed: $($_.Exception.Message)"
}

Write-Host "Done. If SSL failed, make sure DNS A record for $Domain points to $HostName, then rerun without -SkipSsl."
