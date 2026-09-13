# Leave Request Documents

Medical certificates are sensitive documents. PostgreSQL stores metadata only; bytes live behind `IPrivateDocumentStorage`. Development uses private filesystem storage under a non-public directory or Docker named volume. Future Azure Blob Storage can replace the local implementation behind the same abstraction.

The API never exposes public URLs, permanent storage URLs, local paths, or storage keys. All content access goes through authenticated backend endpoints with explicit document authorization.

EP-10 supports PDF, JPEG, and PNG up to the configured upload limit. EP-17 adds policy-required-document enforcement for `MEDICAL_CERTIFICATE` at leave-request submission. Malware scanning, retention/deletion rules, OCR, previews, document approval/rejection, expiration, and complex multiple-document requirement expressions remain deferred.

## Implemented EP-17 required-document enforcement

`LeavePolicyVersion.RequiredDocumentKind` is nullable. `null` means no supporting document is required; `MEDICAL_CERTIFICATE` is the current supported concrete requirement. Submission checks persisted `LeaveRequestDocument` metadata for the required kind before submission side effects. It does not reopen storage or inspect file contents merely to prove presence.

Missing required documents keep the request in `DRAFT` and do not freeze policy, calculate days, reserve balance, enqueue submission outbox, or write a successful submit audit. Manual create-for-other can upload while the original creator still has scoped `leave.requests.create.for_others` authority and the request remains `DRAFT`; this upload authority does not grant read/download authority. Metadata responses continue to omit `StorageKey` and public URLs.
