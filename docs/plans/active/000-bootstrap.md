# Bootstrap Implementation Plan

This plan describes future bootstrap work only. It intentionally does not create React, .NET, Docker, database, or application code yet.

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

## Future bootstrap goals

- Establish the modular monolith backend skeleton.
- Establish the React + TypeScript frontend skeleton.
- Configure Entra ID authentication through a BFF session model.
- Prepare PostgreSQL persistence and migration strategy.
- Prepare private attachment storage boundaries.
- Add test infrastructure for business rules, authorization, and PostgreSQL integration tests.
- Add CI checks for linting, tests, security scanning, and container builds.

## Planned implementation slices

1. Repository structure and solution skeleton.
2. Backend module boundaries and shared-kernel conventions.
3. Authentication/BFF foundation with Entra ID configuration placeholders.
4. Authorization foundation for Role + Organizational Scope.
5. PostgreSQL persistence setup and migration conventions.
6. Policy and balance domain test harness.
7. Attachment storage abstraction with private-access contract.
8. Microsoft Graph integration abstraction for SharePoint migration.
9. Transactional outbox and background worker foundation.
10. Minimal responsive frontend shell and authenticated user context.
11. CI/CD pipeline and local developer workflow documentation.

## Guardrails

- Do not create microservices.
- Do not make SharePoint an operational source of truth.
- Do not hardcode leave policy rules.
- Do not store local Microsoft passwords.
- Do not store medical attachments in public storage.
- Do not directly edit calculated balances.
- Do not grant technical administrators medical-document access by default.
- Do not introduce frameworks or patterns without documenting the reason.

## Open Decisions before implementation

- Hosting target for the first environment.
- RPO/RTO targets.
- Exact Entra tenant/app registration strategy per environment.
- Source of organizational truth: app-managed, Entra Groups, RRHH system, or SharePoint during transition.
- Initial leave types, buckets, and policy rule values.
- Medical attachment visibility rules.
- Whether temporary approver delegation belongs in MVP.
- SharePoint cutover strategy: write freeze or short controlled delta sync.
