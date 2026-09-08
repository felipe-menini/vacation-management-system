# Use Cases

This document lists the MVP use cases from the source proposal. It does not define final UI or API contracts.

| ID | Use case | Primary actor | Expected result |
| --- | --- | --- | --- |
| UC-01 | Sign in | User | Sign in through Microsoft Entra ID SSO and provision a local profile when needed. |
| UC-02 | View balances | Employee | See balances by type/bucket, pending, consumed, expirations, and next carry-over. |
| UC-03 | Create request | Employee | Select type, dates/half-days, comment, attachments; calculate days and validate policy before sending. |
| UC-04 | Save draft | Employee | Save incomplete request without reserving balance or starting approval. |
| UC-05 | Submit request | Employee | Run validations, reserve balance when applicable, and create approval workflow. |
| UC-06 | Approve request | Supervisor/Encargado/RRHH | Approve a step, record comments as required by policy, and advance workflow. |
| UC-07 | Reject request | Supervisor/Encargado/RRHH | Reject with reason, release reservation, and notify requester. |
| UC-08 | Request cancellation | Employee | Request cancellation of approved leave while keeping it active until resolved. |
| UC-09 | Resolve cancellation | Superior/RRHH | Approve or reject cancellation and refund balance when policy says so. |
| UC-10 | Revoke leave | Superior/RRHH | Revoke approved leave with mandatory reason and enhanced audit. |
| UC-11 | Manual creation | Superior/RRHH | Create leave for an employee inside scope; may be pre-approved depending on permission. |
| UC-12 | Adjust balance | RRHH / enabled role | Record manual credit/debit with reason and optional evidence without editing prior movements. |
| UC-13 | View team | Supervisor/Encargado | View people, permitted balances, upcoming absences, and conflicts inside scope. |
| UC-14 | Team calendar | Supervisor/Encargado | View approved/pending absences according to privacy rules and detect coverage issues. |
| UC-15 | Manage policies | RRHH | Create a new policy version with future effective date and simulate impact. |
| UC-16 | Manage organization | RRHH/Admin | Maintain units, responsible users, scopes, and delegated exceptions. |
| UC-17 | Manage medical attachment | Employee/RRHH authorized | Upload, download, or view certificates according to permission and audit access. |
| UC-18 | View audit | RRHH/Admin | Search events by user, entity, period, and action; export basic evidence. |
| UC-19 | Migrate SharePoint data | Admin/technical process | Import mapped data, validate counts, and preserve source IDs. |
| UC-20 | Notify changes | System | Send request, decision, cancellation, balance, or expiration notifications through configured channels. |

## Related documents

- [Requirements](requirements.md)
- [Workflows](workflows.md)
- [API](../architecture/api.md)
