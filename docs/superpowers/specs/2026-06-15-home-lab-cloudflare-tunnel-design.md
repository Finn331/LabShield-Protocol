# LabShield Home Lab Cloudflare Tunnel Migration Design

## Goal

Move LabShield hosting from the VPS at `2.27.165.46` to the home lab server at `192.168.100.142`, while keeping the public site reachable from anywhere through Cloudflare Tunnel.

## Current State

- The Node/Express server lives in `Server` and runs with Docker Compose on port `5000`.
- Existing deployment helpers use Paramiko and default to the VPS host `2.27.165.46` with root-style server operations.
- Unity currently sends authentication and score requests to `http://2.27.165.46:5000`.
- The public web dashboard and registration flow have been served through `https://labshieldprotocol.my.id`.
- Unity MCP is available and should be used to verify Unity editor state and console status when Unity files are changed.

## Target Architecture

- Home lab host: `192.168.100.142`.
- SSH user: `carloserver`.
- Remote app directory: `/opt/labshield-server`.
- Runtime data directory: `/opt/labshield-server/data` mounted into the container as `/app/data`.
- Backend service: Docker Compose service `labshield-backend`, container name `labshield-server`, listening on port `5000`.
- Public ingress: Cloudflare Tunnel routes `https://labshieldprotocol.my.id` to `http://127.0.0.1:5000` on the home lab server.
- Local reverse proxy: not required for the target setup. Cloudflare Tunnel can proxy directly to the Node container port.

## Deployment Flow

1. Package the local `Server` folder, excluding `node_modules` and local runtime data.
2. SSH to `192.168.100.142` as `carloserver` using Paramiko.
3. Use sudo for privileged remote operations, because `carloserver` is not root.
4. Install or verify required packages: Docker, Docker Compose plugin, and `cloudflared`.
5. Extract the server files into `/opt/labshield-server`.
6. Preserve existing runtime JSON data in `/opt/labshield-server/data` across redeploys.
7. Start or rebuild the Docker Compose service.
8. Configure `cloudflared` as a persistent system service for `labshieldprotocol.my.id`.
9. Verify local service health from the home lab host with `http://127.0.0.1:5000/api/scores`.
10. Verify public service health through `https://labshieldprotocol.my.id`.

## Cloudflare Tunnel Strategy

Use a named Cloudflare Tunnel on the home lab server and route the hostname `labshieldprotocol.my.id` to the local backend.

Preferred operating mode:

- Install `cloudflared` on the home lab server.
- Authenticate or configure the tunnel using Cloudflare credentials or a tunnel token supplied during implementation.
- Run `cloudflared` as a systemd service so the tunnel survives reboot.
- Keep the app private on the LAN except for Cloudflare Tunnel ingress.

The implementation must not hardcode Cloudflare secrets into the repository. Tunnel credentials should remain on the home lab server.

## Unity Endpoint Changes

Change Unity networking code to use the public HTTPS domain rather than the old VPS IP:

- `AuthManager` base API URL: `https://labshieldprotocol.my.id/api`.
- `AuthManager` register URL: `https://labshieldprotocol.my.id/register.html`.
- `NetworkManager` score submit URL: `https://labshieldprotocol.my.id/api/submit-score`.

This makes Unity builds work from outside the home network without exposing the home lab IP directly.

## Helper Script Changes

Deployment and diagnostic helpers should default to the home lab target:

- Default SSH host: `192.168.100.142`.
- Default SSH user: `carloserver`.
- Password should still be supplied through environment variables or interactive input, not committed into scripts.
- Commands that require root privileges should run through sudo.
- Legacy VPS-specific Nginx and Certbot setup should be bypassed or replaced for the Cloudflare Tunnel path.

## Error Handling

- If SSH fails, report whether the host is unreachable, authentication failed, or sudo failed.
- If Docker fails, show container status and recent `labshield-server` logs.
- If local health checks pass but public checks fail, inspect `cloudflared` service status and tunnel logs.
- If Unity compilation or console checks report unrelated existing warnings, report them separately from migration errors.

## Verification

- Run server syntax/regression checks where applicable:
  - `node --check Server/server.js`
  - Existing Python regression tests for learning media, compose config, and CSV export.
- Use Paramiko to verify the home lab host:
  - Docker service is running.
  - `/api/scores` works locally on `127.0.0.1:5000`.
  - `cloudflared` service is active.
- Verify public endpoints:
  - `https://labshieldprotocol.my.id/`
  - `https://labshieldprotocol.my.id/register.html`
  - `https://labshieldprotocol.my.id/api/scores`
- Use Unity MCP after C# edits:
  - Check active scene/editor state.
  - Check console errors and warnings.
  - Validate that the edited networking scripts compile cleanly.

## Out Of Scope

- Changing LabShield application behavior unrelated to hosting.
- Exposing the home lab server through router port forwarding.
- Committing SSH, sudo, SMTP, or Cloudflare secrets.
- Removing historical VPS helper files unless the implementation requires replacing their defaults.
