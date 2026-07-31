# CRM + Kanban

Multi-tenant CRM with a Kanban ticket pipeline. .NET 10 (Clean Architecture) + React 19/Vite + MSSQL + S3-compatible storage.

- Architecture and spec: `crm-kanban-mimari.md`
- Progress, decisions, tech debt: `PROGRESS.md`

## Local development

Backend (needs a local SQL Server; connection in `appsettings.json`):

```bash
dotnet run --project src/CrmKanban.Api        # https://localhost:7084
```

Secrets in dev (user-secrets on the API project):

```bash
dotnet user-secrets set "Jwt:SigningKey" "<long-random-key>"
dotnet user-secrets set "SuperAdmin:Email" "admin@example.com"
dotnet user-secrets set "SuperAdmin:Password" "<strong-password>"
```

Frontend:

```bash
cd frontend && npm install && npm run dev     # http://localhost:5173 (proxies /api -> 7084)
```

Tests:

```bash
dotnet test
```

## Deploy — Docker Compose (API + MSSQL + MinIO + nginx)

One command brings up the whole stack: the API, SQL Server, MinIO (S3), and an nginx container
serving the SPA and reverse-proxying `/api` (same origin, no CORS).

1. Copy the env template and set real secrets:

   ```bash
   cp .env.example .env
   # edit .env — set MSSQL_SA_PASSWORD, JWT_SIGNING_KEY (openssl rand -base64 48),
   # S3_SECRET_KEY, SUPERADMIN_EMAIL/PASSWORD
   ```

2. Build and start:

   ```bash
   docker compose up --build -d
   ```

   On startup the API applies EF migrations and runs the idempotent seed (permissions, role matrix,
   default statuses, first super admin). The `createbucket` service creates the private S3 bucket.

3. Open the app: `http://localhost:${WEB_PORT}` (default `8080`). MinIO console: `http://localhost:9001`.

4. Log in with `SUPERADMIN_EMAIL` / `SUPERADMIN_PASSWORD` (you are forced to change the password on
   first login). From there: create an admin, the admin opens a company, invites staff, assigns
   permissions, and shares the public form link `/form/<company-slug>`.

### Configuration (all overridable via environment, `Section__Key`)

| Key | Purpose |
|---|---|
| `ConnectionStrings__Default` | SQL Server connection |
| `Jwt__SigningKey` | JWT signing secret (required, keep out of source) |
| `S3__ServiceUrl` / `S3__BucketName` / `S3__AccessKey` / `S3__SecretKey` | S3-compatible storage |
| `SuperAdmin__Email` / `SuperAdmin__Password` | first super admin (seeded once) |
| `Email__Provider` | `log` (console) or `smtp` (needs `Email__Host/Port/User/Password`) |
| `Captcha__Enabled` | leave `false` until a provider is wired — `true` without one fails closed |

### Production notes

- **TLS** terminates at the reverse proxy (nginx `web` service). Put a real cert / a fronting proxy
  in front of it before exposing publicly; the API speaks plain HTTP inside the compose network.
- **Migrations** run on API startup — fine for single-instance. For multi-instance, move them to a
  one-shot job (see `PROGRESS.md` tech debt #6).
- Secrets come from `.env` / the orchestrator's secret store, never from committed files.
