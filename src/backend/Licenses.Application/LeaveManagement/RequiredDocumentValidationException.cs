using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed class RequiredDocumentValidationException : InvalidOperationException
{
    private RequiredDocumentValidationException(LeaveRequestDocumentKind requiredDocumentKind)
        : base("Leave request is missing a required supporting document.")
    {
        Code = "REQUIRED_DOCUMENT_MISSING";
        RequiredDocumentKind = requiredDocumentKind;
    }

    public string Code { get; }
    public LeaveRequestDocumentKind RequiredDocumentKind { get; }

    public static RequiredDocumentValidationException Missing(LeaveRequestDocumentKind requiredDocumentKind) =>
        new(requiredDocumentKind);
}
