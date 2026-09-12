using Licenses.Application.Common;
using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed record MinimumNoticeCalculationRequest(
    DateOnly StartDate,
    PolicyDayCountMode NoticeDayCountMode,
    Guid? WorkingCalendarId);

public sealed record MinimumNoticeCalculationResult(
    DateOnly BusinessToday,
    DateOnly StartDate,
    PolicyDayCountMode NoticeDayCountMode,
    Guid? WorkingCalendarId,
    int NoticeDays);

public interface IMinimumNoticeCalculator
{
    Task<MinimumNoticeCalculationResult> CalculateAsync(MinimumNoticeCalculationRequest request, CancellationToken cancellationToken);
}

public sealed class MinimumNoticeCalculator(IBusinessDateProvider businessDateProvider, IWorkingCalendarRepository workingCalendarRepository) : IMinimumNoticeCalculator
{
    public async Task<MinimumNoticeCalculationResult> CalculateAsync(MinimumNoticeCalculationRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.NoticeDayCountMode)) throw new ArgumentOutOfRangeException(nameof(request), "Notice day count mode is invalid.");

        var businessToday = businessDateProvider.Today;
        if (request.StartDate < businessToday) throw new InvalidOperationException("StartDate is before the business-local current date.");

        var noticeDays = request.NoticeDayCountMode switch
        {
            PolicyDayCountMode.CalendarDays => request.StartDate.DayNumber - businessToday.DayNumber,
            PolicyDayCountMode.BusinessDays => await CalculateBusinessDaysAsync(businessToday, request.StartDate, request.WorkingCalendarId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request), "Notice day count mode is invalid.")
        };

        return new MinimumNoticeCalculationResult(businessToday, request.StartDate, request.NoticeDayCountMode, request.WorkingCalendarId, noticeDays);
    }

    private async Task<int> CalculateBusinessDaysAsync(DateOnly businessToday, DateOnly startDate, Guid? workingCalendarId, CancellationToken cancellationToken)
    {
        if (workingCalendarId is null) throw new InvalidOperationException("WorkingCalendarId is required for BUSINESS_DAYS notice calculation.");

        var calendar = await workingCalendarRepository.GetCalendarAsync(workingCalendarId.Value, cancellationToken)
            ?? throw new InvalidOperationException("Working calendar does not exist.");

        var noticeDays = 0;
        for (var date = businessToday; date < startDate; date = date.AddDays(1))
        {
            if (calendar.IsWorkingDay(date)) noticeDays++;
        }

        return noticeDays;
    }
}
