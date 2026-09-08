# Roles and Permissions

Permissions are determined by Role + Organizational Scope. See [authorization.md](../architecture/authorization.md) for enforcement architecture.

## Roles

| Role | Main responsibility |
| --- | --- |
| Employee | Operates only on own balances, requests, comments, and allowed attachments. |
| Supervisor | Manages requests and visibility for people in assigned units, usually without descendants. |
| Encargado | Manages higher-level units and, when configured, descendant units. |
| RRHH | Organization-wide functional access, configuration, balance adjustments, reports, and exceptions. |
| Technical Administrator | Technical parameters, integrations, technical catalogs, and support. No default HR approval authority or medical-document access. |

## MVP permission matrix

| Action | Employee | Supervisor | Encargado | RRHH | Technical admin |
| --- | --- | --- | --- | --- | --- |
| View own balance | Yes | Yes | Yes | Yes | Yes |
| View third-party balance | No | In scope | In scope + descendants | Entire organization | Only if support access is authorized |
| Create own request | Yes | Yes | Yes | Yes | Yes |
| Create leave for others | No | In scope, if enabled | In scope, if enabled | Yes | No by default |
| Approve/reject | No | In scope | In scope | Yes | No |
| Revoke approved leave | No | In scope, if enabled | In scope, if enabled | Yes | No |
| Request own cancellation | Yes | Yes | Yes | Yes | Yes |
| Approve cancellation | No | In scope | In scope | Yes | No |
| Add comments | Own requests | In scope | In scope | Yes | Technical comments only |
| View medical certificate | Own certificate | Configurable, normally No | Configurable, normally No | Yes, according to function | No by default |
| Adjust balance | No | Configurable | Configurable | Yes | No |
| Configure types/policies | No | No | No | Yes | Technical support without functional decision |
| Configure units/scopes | No | No | No | Yes/delegated | Technical setup |
| View audit | Own limited audit | In scope | In scope | Yes | Technical audit |

Manual creation, revocation, and balance adjustment require mandatory reason and audit.

## Open Decisions

- Who can view medical certificates besides the employee and RRHH?
- Can supervisors and encargados always create manual leave/revoke leave, or does it depend on leave type?
- Which roles may adjust balance outside RRHH, if any?

## Related documents

- [Authorization architecture](../architecture/authorization.md)
- [Security](../architecture/security.md)
