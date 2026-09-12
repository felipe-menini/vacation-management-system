using Licenses.Application.Audit;
using Licenses.Application.Common;
using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed class LeaveRequestCompletionService(
    ILeaveRequestRepository repository,
    IBusinessDateProvider businessDateProvider,
    IClock clock,
    IAuditWriter auditWriter)
{
    public const int DefaultBatchSize = 100;
    public const int MaxBatchSize = 500;

    public async Task<CompleteEligibleLeaveRequestsResultDto> CompleteEligibleAsync(int batchSize = DefaultBatchSize, CancellationToken cancellationToken = default)
    {
        var effectiveBatchSize = NormalizeBatchSize(batchSize);
        var businessToday = businessDateProvider.Today;
        var candidates = await repository.ListEligibleApprovedForCompletionAsync(businessToday, effectiveBatchSize, cancellationToken);
        var completedIds = new List<Guid>();
        var skippedCount = 0;

        using var _ = await repository.BeginTransactionAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            var request = await repository.GetForUpdateAsync(candidate.Id, cancellationToken);
            if (request is null)
            {
                skippedCount++;
                continue;
            }

            var completedAtUtc = clock.UtcNow.UtcDateTime;
            if (request.Complete(businessToday, completedAtUtc))
            {
                completedIds.Add(request.Id);
                await auditWriter.WriteAsync(new AuditEventData(
                    null,
                    "leave.request.complete",
                    "LeaveRequest",
                    request.Id,
                    request.UserId,
                    request.OrgUnitId,
                    null,
                    completedAtUtc,
                    AuditMetadataJson.Serialize(new
                    {
                        previousStatus = "APPROVED",
                        resultingStatus = "COMPLETED",
                        request.EndDate,
                        completedAtUtc
                    })),
                    cancellationToken);
            }
            else
            {
                skippedCount++;
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
        await repository.CommitTransactionAsync(cancellationToken);

        return new(
            businessToday,
            effectiveBatchSize,
            candidates.Count,
            completedIds.Count,
            skippedCount,
            completedIds);
    }

    private static int NormalizeBatchSize(int batchSize)
    {
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");
        return Math.Min(batchSize, MaxBatchSize);
    }
}
