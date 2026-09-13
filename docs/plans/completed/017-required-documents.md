# 017 - Required Leave Documents

## Status

Completed in EP-17A, EP-17B1, EP-17B2, and EP-17C.

## Outcome

EP-17 makes supporting-document requirements a versioned leave-policy rule and validates the required document at submission without weakening private document handling or backend authorization.

## Policy behavior

- `LeavePolicyVersion.RequiredDocumentKind` is nullable.
- `null` means no supporting document is required.
- The current supported concrete kind is `MEDICAL_CERTIFICATE`.
- The requirement is stored on `LeavePolicyVersion`, so it is versioned with the effective policy.
- Published policy versions remain immutable; only draft versions can change the requirement.
- Required documents are not hardcoded from `LeaveType` codes.

## Submission behavior

Required-document validation happens during submission, using the exact resolved published policy version that would be frozen on a successful submit.

- Draft leave requests may exist without the required document.
- A persisted `LeaveRequestDocument` with the configured kind satisfies the rule.
- Presence validation checks persisted document metadata only.
- Validation does not reopen private storage, read file bytes, inspect filenames as proof, or revalidate file content.
- Missing required document returns `REQUIRED_DOCUMENT_MISSING` with safe metadata: `requiredDocumentKind`.
- The request remains `DRAFT`.
- No balance reservation or ledger mutation is created.
- No successful submit audit is written.
- No submission outbox event is enqueued.
- No successful policy freeze or calculated days persistence occurs.
- HR, Manager, Supervisor, and create-for-other actors have no bypass.

## Self-service behavior

- Employees can create a draft first.
- The frontend exposes supporting-document upload before submit.
- `REQUIRED_DOCUMENT_MISSING` is displayed as safe business feedback, without storage keys, URLs, or file content.

## Manual create-for-other behavior

Manual create-for-other supports a document-capable draft-first workflow:

1. An authorized actor creates a `DRAFT` request for another employee.
2. The actor uploads the supporting document while all of these remain true:
   - they are the original creator
   - the request is still `DRAFT`
   - they still have `leave.requests.create.for_others` scope over the stored request OrgUnit
3. The actor submits through `submit-for-other`.
4. Submission reuses the normal submission pipeline and required-document policy validation.

Important boundaries:

- Upload authority does not imply read/download authority.
- No generic document-read authority was added.
- The existing atomic create-and-submit API remains available for compatibility.
- The atomic API cannot attach a document before submission and therefore does not bypass required-document policies.

## Privacy and security

- Private storage remains unchanged.
- Document metadata responses do not expose `StorageKey`.
- The API does not return public or permanent storage URLs.
- Audit metadata does not contain medical file content or storage keys.
- Technical administrators receive no automatic business or document authority.

## Audit behavior

- Manual create-for-other draft creation uses the existing `leave.request.create_for_other` audit event.
- Upload uses the existing `leave.document.upload` audit event.
- Successful submit uses the existing `leave.request.submit` audit event.
- Failed required-document validation does not create a successful submit audit event.

## Integration checks

- Policy API persists and returns `RequiredDocumentKind`.
- Submission uses the exact resolved published policy version.
- Required-document validation happens before balance reservation, request submission, audit, outbox, and policy-freeze side effects.
- Self-service frontend can upload before submit.
- Manual create-for-other frontend uses the draft-first path.
- Submit-for-other uses normal submission rules.
- `TECH_ADMIN` receives no automatic business or document authority.
- No `StorageKey` or public URL is exposed by metadata responses.

## Explicit deferrals

These are intentionally deferred and are not missing EP-17 implementation:

- document approval/rejection
- OCR
- malware scanning
- document expiration
- document deletion/retention workflow
- complex multiple-document requirement expressions
- bypass permission
- Microsoft Entra integration changes
- SharePoint integration changes
