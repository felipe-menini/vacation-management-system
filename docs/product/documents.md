# Leave Request Documents

Medical certificates are sensitive documents. PostgreSQL stores metadata only; bytes live behind `IPrivateDocumentStorage`. Development uses private filesystem storage under a non-public directory or Docker named volume. Future Azure Blob Storage can replace the local implementation behind the same abstraction.

The API never exposes public URLs, permanent storage URLs, local paths, or storage keys. All content access goes through authenticated backend endpoints with explicit document authorization.

EP-10 supports PDF, JPEG, and PNG up to the configured upload limit. Malware scanning, retention/deletion rules, OCR, previews, and policy-required-document enforcement are future production hardening items.
