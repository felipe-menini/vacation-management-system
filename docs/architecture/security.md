# Security, Privacy, and Audit Architecture

Security is centered on Entra ID authentication, backend authorization, least-privilege Microsoft integrations, private attachment storage, and business audit evidence.

## Authentication

- Use Microsoft Entra ID for corporate SSO.
- Do not store Microsoft passwords.
- Prefer backend/BFF OpenID Connect with secure cookies.
- Validate allowed tenant and required claims in the backend.
- MFA and Conditional Access remain corporate Entra policies.
- Use Entra Object ID as the stable local user identifier because email can change.

## Authorization controls

- Enforce authorization in backend policies and domain commands.
- Deny by default.
- Apply organizational scope in queries and commands.
- Separate technical administration from HR/business authority.
- Re-authorize every sensitive document access.

See [authorization.md](authorization.md).

## Medical attachments and sensitive documents

Medical certificates and sensitive attachments must:

- be stored in private object storage
- never expose permanent public URLs
- use encryption in transit and at rest
- validate MIME type, extension, size, and safe file names
- pass malware scanning before download/view access is enabled
- be accessed only through backend authorization or short-lived delegated URLs
- audit view/download attempts
- follow retention and deletion policy defined with RRHH/Legal, not hardcoded

Technical administrators do not receive medical-document access by default.

## Audit events

| Event | Minimum data |
| --- | --- |
| Login / access failure | User, tenant, timestamp, result, correlation ID. |
| Create/submit request | Actor, request, applied policy, calculated days, relevant values. |
| Approve/reject/cancel/revoke | Actor, decision, reason/comment, previous/new state. |
| Balance adjustment | Actor, amount, bucket, reason, derived balance before/after. |
| Policy change | Actor, previous/new version, effective dates, diff. |
| Organization/permission change | Actor, affected user, scope, role, before/after. |
| Sensitive attachment access | Actor, document, action, result. |
| Migration | Process, batch, source, result, errors. |

## Logs vs audit

Technical logs support operations and troubleshooting and may rotate. Business audit is functional evidence and requires a separate retention policy. Application logs must not contain tokens, secrets, or medical content.

## Related documents

- [Authorization](authorization.md)
- [Integrations](integrations.md)
- [Roles and permissions](../product/roles-permissions.md)

## Implemented audit trail foundation

EP-12A provides the backend persistence/writer foundation for audit events. Audit answers who did what to which resource, when, and with what minimal context. It is not the source of business truth for balances, approvals, cancellations, revocations, or outbox processing; those remain in their dedicated history tables.

Audit metadata must be intentionally small and privacy-safe. Do not store passwords, tokens, cookies, authorization headers, medical document bytes, private storage keys, uploaded files, complete EF entities, or unnecessary sensitive personal data. Sensitive document access auditing is planned for EP-12B. Audit viewer/search authorization is planned for EP-12C. Retention remains a future RRHH/Legal/compliance decision.
