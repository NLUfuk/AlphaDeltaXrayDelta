# CRM + Kanban

Multi-tenant CRM with a Kanban ticket pipeline. .NET 10 (Clean Architecture) + React 19/Vite + MSSQL + S3-compatible storage.

> **`docs/` is not published.** The spec, the progress/decision log, the requirement checklist,
> the roadmap and the deploy walkthrough are kept in the working tree but deliberately untracked
> (owner's call, 2026-08-07) — this repository carries only what runs the project. The files below
> exist next to a local clone's checkout; they are not resolvable links here.
>
> `docs/MIMARI-RAPOR.md` (what the system actually is today) · `docs/crm-kanban-mimari.md` (spec) ·
> `docs/PROGRESS.md` (progress, decisions, tech debt) · `docs/ONERILER.md` (roadmap) ·
> `docs/CRM_Kanban_Gereksinim_Listesi.md` (requirement checklist) ·
> `docs/DEPLOY-monsterasp.md` (IIS / MonsterASP.NET deploy)

## Quickest start (Windows, Docker)

```powershell
./up.ps1            # generates .env with random secrets, builds, starts, waits for readiness
./up.ps1 -Down      # stop
./up.ps1 -Reset     # stop and wipe volumes (DB + files)
```

First run prints the generated super-admin password. Everything below is the manual equivalent.

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
   permissions, and shares one of the two customer entry points:

   | Link | Who it is for |
   |---|---|
   | `/form/<slug>` | Anonymous public form — no account needed. A submission from an unknown email lands in **moderation** (`/moderation`) and reaches the board only once approved. |
   | `/c/<slug>` | The company's own sign-in page — the customer registers, gets a 6-digit code by email, and their requests go straight to the board. |

   Staff work the board at `/` (kanban), customers see a plain list of their own requests.

### Configuration (all overridable via environment, `Section__Key`)

| Key | Purpose |
|---|---|
| `ConnectionStrings__Default` | SQL Server connection |
| `Jwt__SigningKey` | JWT signing secret (required, keep out of source) |
| `SuperAdmin__Email` / `SuperAdmin__Password` | first super admin (seeded once) |
| **`App__PublicBaseUrl`** | absolute base for links in outgoing mail (invite, verification code). **Wrong value = every emailed link is broken** — set it to the URL users actually reach. |
| `Files__Provider` | `s3` (default), `local` (host disk, `LocalStorage__RootPath`), or `azure` (`AzureBlob__ConnectionString`) |
| `S3__BucketName` / `S3__AccessKey` / `S3__SecretKey` / `S3__Region` | S3 storage. `S3__ServiceUrl` empty for AWS, set for MinIO/R2/B2; `S3__ForcePathStyle` `true` for MinIO, `false` for AWS |
| `Email__Provider` | `log` (console) or `smtp` |
| `Email__Host` / `Email__Port` / `Email__UseSsl` | SMTP relay. Port **587** with STARTTLS — `System.Net.Mail` cannot do implicit SSL on 465 |
| `Email__Username` / `Email__Password` / `Email__From` / `Email__FromName` | SMTP credentials and sender identity. `From` must be on a domain verified at the relay, or mail is rewritten/rejected |
| `Seed__Demo` | `true` seeds two demo companies with tickets (handy for a first look, **`false` in real production**) |
| `Captcha__Enabled` | leave `false` until a provider is wired — `true` without one fails closed |

In Docker these are set from `.env` (see `.env.example`); `docker-compose.yml` maps each `UPPER_SNAKE`
variable to its `Section__Key`. Outside Docker, set `Section__Key` directly in the environment.

## Deploy — IIS / MonsterASP.NET (no Docker)

```powershell
./publish.ps1       # SPA build -> API wwwroot -> self-contained win-x64 publish into ./publish
```

Upload the contents of `./publish` to the site root and set the config keys above in the panel's
environment variables. Full walkthrough, including the storage options: `docs/DEPLOY-monsterasp.md`.

### Production notes

- **TLS** terminates at the reverse proxy (nginx `web` service). Put a real cert / a fronting proxy
  in front of it before exposing publicly; the API speaks plain HTTP inside the compose network.
- **Migrations** run on API startup — fine for single-instance. For multi-instance, move them to a
  one-shot job (see `docs/PROGRESS.md` tech debt #6).
- Secrets come from `.env` / the orchestrator's secret store, never from committed files.
