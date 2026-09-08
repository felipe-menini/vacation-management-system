# AGENTS.md

# Project

Corporate Leave Management System.

The application manages employee leave requests, approvals,
balances, organizational scopes and leave policies.

Before implementing or modifying features, read the relevant
documents under:

- docs/architecture/
- docs/product/
- docs/plans/

# Architecture

Backend:
- ASP.NET Core
- .NET 10
- Modular monolith
- PostgreSQL
- Entity Framework Core

Frontend:
- React
- TypeScript
- Responsive web application / PWA

Authentication:
- Microsoft Entra ID
- OpenID Connect
- Backend-for-Frontend authentication
- Secure HttpOnly cookies

External integrations:
- Microsoft Graph
- SharePoint migration
- Private object storage
- Email / Microsoft 365 notifications

# Backend modules

The backend should be organized logically around:

- Identity
- Organization
- Authorization
- LeaveManagement
- Policies
- Balances
- Approvals
- Documents
- Notifications
- Integrations
- Audit

Do not create microservices.

Use a modular monolith.

# Mandatory business rules

Authorization is always enforced by the backend.

Frontend visibility is never considered a security control.

Effective authorization is:

Role + Organizational Scope

A supervisor may only operate on users inside their assigned scope.

An encargado may optionally include descendant organizational units.

Balance transactions are immutable.

Never directly modify a calculated leave balance.

Balance changes must happen through ledger transactions such as:

- GRANT
- RESERVE
- RELEASE
- CONSUME
- REFUND
- ADJUSTMENT
- EXPIRE

Leave policies are versioned.

Historical policy versions must never be modified after they
have been applied to requests.

Business rules such as:

- business days vs calendar days
- weekends
- holidays
- half days
- overlapping requests
- negative balances
- minimum notice
- maximum duration
- required attachments
- approval levels
- team capacity
- carry-over

must be configurable and must not be hardcoded.

# Privacy and security

Medical attachments are sensitive data.

They must:

- use private storage
- never expose permanent public URLs
- require backend authorization
- have access audited

Technical administrators do not automatically have permission
to view medical documents.

Never store Microsoft passwords.

Never commit secrets, access tokens, connection strings or
credentials.

# Integration boundaries

The domain must not depend directly on:

- Microsoft Graph
- SharePoint
- Azure
- PostgreSQL
- HTTP
- React

SharePoint column names and structures must remain inside the
integration/migration layer.

PostgreSQL will be the future source of truth.

SharePoint is only a migration/historical source.

# Data

Use UTC for technical timestamps.

Use database transactions for operations involving:

- leave requests
- approvals
- balances

Critical commands must be idempotent where appropriate.

# Testing

Business logic must have unit tests.

Authorization must have dedicated tests.

Integration tests must use PostgreSQL.

Important workflows require integration/E2E coverage.

Before declaring a task complete:

1. Build the affected projects.
2. Run relevant tests.
3. Run lint/typecheck where applicable.
4. Report any failing tests.
5. Summarize modified files.

# Development workflow

For non-trivial work:

1. Read AGENTS.md.
2. Read relevant architecture/product documentation.
3. Create or update an implementation plan under docs/plans/active.
4. Implement a small coherent vertical slice.
5. Add tests.
6. Run tests.
7. Review the diff.
8. Update documentation if architecture or behavior changed.

Do not implement unrelated functionality.

Do not introduce new frameworks or architectural patterns
without documenting why.

If a business requirement is ambiguous, do not invent a rule.
Document it as an unresolved requirement instead.