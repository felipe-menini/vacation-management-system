# Bootstrap Implementation Plan

This plan tracks EP-00 bootstrap implementation. It establishes the production-oriented
walking skeleton only; leave-management business functionality remains out of scope.

## Source documents

- [Architecture overview](../../architecture/ARCHITECTURE.md)
- [Domain model](../../architecture/domain-model.md)
- [Authorization](../../architecture/authorization.md)
- [Database](../../architecture/database.md)
- [API](../../architecture/api.md)
- [Security](../../architecture/security.md)
- [Integrations](../../architecture/integrations.md)
- [Product requirements](../../product/requirements.md)
- [Use cases](../../product/use-cases.md)
- [Roles and permissions](../../product/roles-permissions.md)
- [Leave policies](../../product/leave-policies.md)
- [Workflows](../../product/workflows.md)

## Bootstrap goals

- [x] Establish the modular monolith backend skeleton.
- [x] Establish the React + TypeScript frontend skeleton.
- [ ] Configure Entra ID authentication through a BFF session model.
- [x] Prepare PostgreSQL persistence and migration strategy.
- [ ] Prepare private attachment storage boundaries.
- [x] Add initial backend unit test projects.
- [x] Add an API integration/smoke test for application startup and `/health`.
- [x] Add CI checks for backend restore/build/test and frontend lint/typecheck/build.

## Implemented EP-00 slices

1. Created `src/backend/Licenses.slnx` with API, Application, Domain, Infrastructure, and Worker projects.
2. Wired project references so Domain stays independent from Infrastructure, ASP.NET Core, EF Core, PostgreSQL, Microsoft Graph, Azure, HTTP, and React.
3. Added `ApplicationDbContext` in Infrastructure with PostgreSQL provider registration and no business/domain tables.
4. Added ASP.NET Core health endpoints:
   - `GET /health` for API liveness.
   - `GET /health/ready` for PostgreSQL readiness.
5. Created a .NET Worker service that starts and logs that it is running.
6. Created `src/frontend/licenses-web` with React, TypeScript, Vite, lint, typecheck, and production build scripts.
7. Implemented a responsive frontend page that calls backend health endpoints through `/api`.
8. Added Dockerfiles for API, Worker, and Frontend plus root `docker-compose.yml`.
9. Added root README developer instructions and GitHub Actions CI.

## Guardrails

- Do not create microservices.
- Do not make SharePoint an operational source of truth.
- Do not hardcode leave policy rules.
- Do not store local Microsoft passwords.
- Do not store medical attachments in public storage.
- Do not directly edit calculated balances.
- Do not grant technical administrators medical-document access by default.
- Do not introduce frameworks or patterns without documenting the reason.

## EP-00 non-goals confirmed

- No leave requests were implemented.
- No users, roles, organizational scopes, or authorization policies were implemented.
- No policy or balance entities were implemented.
- No Microsoft Entra ID, Microsoft Graph, or SharePoint integration was implemented.
- No real secrets were committed.

## Open Decisions before implementation

- Hosting target for the first environment.
- RPO/RTO targets.
- Exact Entra tenant/app registration strategy per environment.
- Source of organizational truth: app-managed, Entra Groups, RRHH system, or SharePoint during transition.
- Initial leave types, buckets, and policy rule values.
- Medical attachment visibility rules.
- Whether temporary approver delegation belongs in MVP.
- SharePoint cutover strategy: write freeze or short controlled delta sync.
