using Licenses.Application.LeaveManagement;
using Microsoft.Extensions.Options;

namespace Licenses.Worker;

public sealed class LeaveCompletionOptions
{
    public const string SectionName = "LeaveCompletion";

    public bool Enabled { get; set; } = true;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);
    public int BatchSize { get; set; } = LeaveRequestCompletionService.DefaultBatchSize;
}

public sealed class LeaveCompletionOptionsValidator : IValidateOptions<LeaveCompletionOptions>
{
    public ValidateOptionsResult Validate(string? name, LeaveCompletionOptions options)
    {
        if (options.PollInterval <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("LeaveCompletion:PollInterval must be positive.");
        }

        if (options.PollInterval < TimeSpan.FromMinutes(1))
        {
            return ValidateOptionsResult.Fail("LeaveCompletion:PollInterval must be at least one minute.");
        }

        if (options.BatchSize <= 0 || options.BatchSize > LeaveRequestCompletionService.MaxBatchSize)
        {
            return ValidateOptionsResult.Fail($"LeaveCompletion:BatchSize must be between 1 and {LeaveRequestCompletionService.MaxBatchSize}.");
        }

        return ValidateOptionsResult.Success;
    }
}
