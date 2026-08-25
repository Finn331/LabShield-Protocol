# LabShield Server Restore Design

Date: 2026-05-24

## Goal

Restore the LabShield web application and game-to-server integration on the reinstalled server at `2.27.165.46`, using the existing project server implementation and the domain `labshieldprotocol.my.id` with HTTPS.

## Selected Approach

Deploy the existing `Server` folder to `/opt/labshield-server` and run it through Docker Compose behind Nginx. This matches the current repository structure and avoids rebuilding the server manually from scattered commands.

## Architecture

- `Server/server.js` runs as the Express backend inside the `labshield-server` Docker container.
- `Server/public` is served by Express for the register page, teacher dashboard, student dashboard, and learning media pages.
- Docker Compose maps port `5000` and mounts `./data` to `/app/data` so `users.json` and `student_scores.json` survive container rebuilds.
- Nginx listens on ports `80` and `443` for `labshieldprotocol.my.id` and proxies requests to `127.0.0.1:5000`.
- Certbot provisions and renews the Let's Encrypt certificate for `labshieldprotocol.my.id`.
- Unity keeps using the existing API host `http://2.27.165.46:5000/api` for login and score submission.

## Deployment Flow

1. Package the local `Server` folder, excluding `node_modules` and runtime `data`.
2. Upload the package to the server over SSH.
3. Install required system packages, Docker, Docker Compose plugin, Nginx, UFW, and Certbot if missing.
4. Extract the app to `/opt/labshield-server`.
5. Preserve or initialize persisted data under `/opt/labshield-server/data`.
6. Run `docker compose up -d --build --remove-orphans`.
7. Write the Nginx site config for `labshieldprotocol.my.id`.
8. Enable firewall ports `22`, `80`, `443`, and `5000`.
9. Run Certbot for HTTPS and redirect HTTP to HTTPS.
10. Run health checks against `/register.html` and `/api/scores`.

## Validation

- `https://labshieldprotocol.my.id/register.html` loads the register page.
- `https://labshieldprotocol.my.id/dashboard.html` loads the teacher dashboard.
- `http://2.27.165.46:5000/api/scores` returns JSON.
- `docker compose ps` shows the `labshield-server` container running.
- Existing Unity code still points to `2.27.165.46:5000` for API calls.

## Error Handling

- If SSL setup fails, keep the backend and HTTP reverse proxy running and report that DNS or Certbot needs follow-up.
- If Docker is missing, install it before starting the app.
- If old root-level `users.json` or `student_scores.json` files exist after restore, copy them into `data` before starting the app.
- If local HTTP verification from the development machine fails, check server-side health checks and container status before changing code.

## Security Notes

- Do not persist SSH credentials in repository files.
- Because server credentials were shared in chat, rotate the root password after the restore is complete.
- Keep `Server/.env` free of production secrets unless SMTP or OTP email configuration is intentionally added.

## Out of Scope

- Rewriting authentication or score APIs.
- Migrating JSON storage to a database.
- Changing Unity API URLs unless validation shows the restored endpoint is incompatible.
- Adding SMTP credentials for OTP email registration.
