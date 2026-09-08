# Leave Management System

Corporate leave management system walking skeleton.

## EP-00 developer quick path

### Prerequisites

- .NET SDK 10
- Node.js 24 with npm
- Docker and Docker Compose

### Run locally without Docker

Start PostgreSQL first, then run the API and frontend:

```bash
dotnet run --project src/backend/Licenses.Api
cd src/frontend/licenses-web
npm ci
npm run dev
```

The Vite dev server proxies `/api/*` to `VITE_API_PROXY_TARGET`, which defaults to
`http://localhost:5080`.

### Run with Docker Compose

```bash
cp .env.example .env
docker compose up --build
```

### Local URLs

| Service | URL |
| --- | --- |
| Frontend | http://localhost:5173 |
| API | http://localhost:5080 |
| API health | http://localhost:5080/health |
| PostgreSQL readiness | http://localhost:5080/health/ready |

### Run tests and checks

```bash
dotnet restore src/backend/Licenses.slnx
dotnet build src/backend/Licenses.slnx
dotnet test src/backend/Licenses.slnx

cd src/frontend/licenses-web
npm ci
npm run lint
npm run typecheck
npm run build

docker compose config
```

## Scope guard

EP-00 intentionally creates only a production-oriented walking skeleton. It does
not implement leave requests, users, roles, authorization rules, policy entities,
balance entities, Microsoft Entra ID, Microsoft Graph, or SharePoint integration.
