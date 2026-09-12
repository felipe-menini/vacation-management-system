namespace Licenses.Application.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class BusinessTimeOptions
{
    public const string SectionName = "BusinessTime";
    public string? TimeZoneId { get; set; }
}

public interface IBusinessDateProvider
{
    DateOnly Today { get; }
}

public sealed class BusinessDateProvider(IClock clock, TimeZoneInfo businessTimeZone) : IBusinessDateProvider
{
    public DateOnly Today
    {
        get
        {
            var businessLocal = TimeZoneInfo.ConvertTime(clock.UtcNow, businessTimeZone);
            return DateOnly.FromDateTime(businessLocal.DateTime);
        }
    }
}
