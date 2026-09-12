using Licenses.Application.LeaveManagement;
using Microsoft.Extensions.Options;

namespace Licenses.Worker;

public interface ILeaveRequestCompletionProcessor
{
    Task<LeaveRequestCompletionProcessingResult> ProcessBacklogAsync(CancellationToken cancellationToken);
}

public sealed class LeaveRequestCompletionProcessor(
    LeaveRequestCompletionService service,
    IOptions<LeaveCompletionOptions> options,
    ILogger<LeaveRequestCompletionProcessor> logger) : ILeaveRequestCompletionProcessor
{
    public async Task<LeaveRequestCompletionProcessingResult> ProcessBacklogAsync(CancellationToken cancellationToken)
    {
        var totalScanned = 0;
        var totalCompleted = 0;
        var totalSkipped = 0;
        var passes = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await service.CompleteEligibleAsync(options.Value.BatchSize, cancellationToken);
            passes++;
            totalScanned += result.ScannedCount;
            totalCompleted += result.CompletedCount;
            totalSkipped += result.SkippedCount;

            logger.LogInformation(
                "Leave completion pass finished. BusinessToday={BusinessToday} Scanned={ScannedCount} Completed={CompletedCount} Skipped={SkippedCount}",
                result.BusinessToday,
                result.ScannedCount,
                result.CompletedCount,
                result.SkippedCount);

            if (result.ScannedCount == 0 || result.ScannedCount < result.RequestedBatchSize)
            {
                break;
            }

            if (result.CompletedCount == 0)
            {
                logger.LogWarning("Leave completion stopped early after a full batch produced no completions to avoid a tight loop.");
                break;
            }
        }

        return new LeaveRequestCompletionProcessingResult(passes, totalScanned, totalCompleted, totalSkipped);
    }
}

public sealed record LeaveRequestCompletionProcessingResult(int Passes, int ScannedCount, int CompletedCount, int SkippedCount);
