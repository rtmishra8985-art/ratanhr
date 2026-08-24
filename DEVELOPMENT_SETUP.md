# RatanHR Development Setup

This guide is for local development only. It deliberately keeps database
passwords, JWT keys, encryption keys, and Swagger credentials outside the
repository.

## Required tools

- .NET SDK 8.x
- MySQL 8.4, or Docker with Docker Compose
- Node.js 20+ and Bun (the React source currently ships with `bun.lock`)
- OpenSSL for generating local encryption and JWT values

Confirm the backend toolchain before starting:

```bash
dotnet --version
dotnet restore HRMS.sln
dotnet build HRMS.sln --no-restore
dotnet test HRMS.sln --no-build
```

## Configure local secrets

The API project is configured for ASP.NET Core User Secrets. From the
repository root:

```bash
cd HRMS.API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost;Port=3306;Database=hrms_db;User ID=hrms;Password=<local-password>;AllowPublicKeyRetrieval=True;SslMode=Required"
dotnet user-secrets set "Security:EncryptionKey" \
  "$(openssl rand -base64 32)"
dotnet user-secrets set "Swagger:Username" "local-admin"
dotnet user-secrets set "Swagger:Password" "<local-swagger-password>"
cd ..
```

For local JWT signing, generate a separate development RSA key pair and
provide the values through User Secrets or environment variables. Do not
commit the private key.

```bash
openssl genrsa -out /tmp/ratanhr-dev-private.pem 2048
openssl rsa -in /tmp/ratanhr-dev-private.pem -pubout \
  -out /tmp/ratanhr-dev-public.pem
```

The checked-in `appsettings.Development.json` intentionally contains empty
secret values. This prevents placeholder credentials from being mistaken for
working credentials and prevents accidental secret commits.

## Start the API

Start MySQL first, then run:

```bash
cd HRMS.API
dotnet run
```

Development migrations are applied according to the API's development
configuration. For a controlled migration run, use the EF command documented
in `README.md`.

## Start the React frontend

```bash
cd HRMS.SPA.Source
bun install --frozen-lockfile
PORT=3001 BASE_PATH=/ NODE_ENV=development bun run dev:local
```

The local Vite configuration points API calls at the local API server.

## Biometric scope

The current release supports ZKTeco attendance-log import and device status.
ZKTeco employee roster/user synchronization is intentionally unsupported and
must not be treated as a successful zero-user sync. Enable that workflow only
after the vendor enrollment/template protocol is implemented and tested.

## Before sharing or deploying

Run a repository secret scan and verify that:

- no `.env` files are tracked;
- no private keys or certificates are tracked;
- no local database credentials are in configuration;
- production secrets are injected by the deployment secret manager;
- the current source tree, not the generated `HRMS.SPA` output, is used for
  frontend changes.