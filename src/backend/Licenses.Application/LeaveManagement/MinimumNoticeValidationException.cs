using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed class MinimumNoticeValidationException : InvalidOperationException
{
    private MinimumNoticeValidationException(
        string code,
        string message,
        int requiredMinimumNoticeDays,
        int? calculatedNoticeDays,
        PolicyDayCountMode noticeDayCountMode,
        DateOnly? businessToday,
        DateOnly? startDate,
        Exception? innerException = null) : base(message, innerException)
    {
        Code = code;
        RequiredMinimumNoticeDays = requiredMinimumNoticeDays;
        CalculatedNoticeDays = calculatedNoticeDays;
        NoticeDayCountMode = noticeDayCountMode;
        BusinessToday = businessToday;
        StartDate = startDate;
    }

    public string Code { get; }
    public int RequiredMinimumNoticeDays { get; }
    public int? CalculatedNoticeDays { get; }
    public PolicyDayCountMode NoticeDayCountMode { get; }
    public DateOnly? BusinessToday { get; }
    public DateOnly? StartDate { get; }

    public static MinimumNoticeValidationException Insufficient(
        int requiredMinimumNoticeDays,
        int calculatedNoticeDays,
        PolicyDayCountMode noticeDayCountMode,
        DateOnly businessToday) =>
        new(
            "INSUFFICIENT_MINIMUM_NOTICE",
            "Leave request does not satisfy the minimum notice requirement.",
            requiredMinimumNoticeDays,
            calculatedNoticeDays,
            noticeDayCountMode,
            businessToday,
            null);

    public static MinimumNoticeValidationException CalculationFailed(
        int requiredMinimumNoticeDays,
        PolicyDayCountMode noticeDayCountMode,
        DateOnly startDate,
        Exception innerException) =>
        new(
            "MINIMUM_NOTICE_CALCULATION_FAILED",
            "Minimum notice could not be validated for the leave request.",
            requiredMinimumNoticeDays,
            null,
            noticeDayCountMode,
            null,
            startDate,
            innerException);
}
