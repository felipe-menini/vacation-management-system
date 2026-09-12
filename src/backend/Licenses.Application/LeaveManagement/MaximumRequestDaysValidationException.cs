using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed class MaximumRequestDaysValidationException : InvalidOperationException
{
    private MaximumRequestDaysValidationException(
        decimal maximumRequestDays,
        decimal calculatedDays,
        PolicyDayCountMode dayCountMode) : base("Leave request exceeds the maximum request duration.")
    {
        Code = "MAXIMUM_REQUEST_DAYS_EXCEEDED";
        MaximumRequestDays = maximumRequestDays;
        CalculatedDays = calculatedDays;
        DayCountMode = dayCountMode;
    }

    public string Code { get; }
    public decimal MaximumRequestDays { get; }
    public decimal CalculatedDays { get; }
    public PolicyDayCountMode DayCountMode { get; }

    public static MaximumRequestDaysValidationException Exceeded(
        decimal maximumRequestDays,
        decimal calculatedDays,
        PolicyDayCountMode dayCountMode) =>
        new(maximumRequestDays, calculatedDays, dayCountMode);
}
