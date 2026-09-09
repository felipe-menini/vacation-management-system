using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed class LeaveCatalogService(ILeaveCatalogRepository repository, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<LeaveTypeDto>> ListLeaveTypesAsync(bool? isActive, CancellationToken cancellationToken)
    {
        var leaveTypes = await repository.ListLeaveTypesAsync(isActive, cancellationToken);
        return leaveTypes.Select(ToDto).ToList();
    }

    public async Task<LeaveTypeDto?> GetLeaveTypeAsync(Guid id, CancellationToken cancellationToken)
    {
        var leaveType = await repository.GetLeaveTypeAsync(id, cancellationToken);
        return leaveType is null ? null : ToDto(leaveType);
    }

    public async Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeCommand command, CancellationToken cancellationToken)
    {
        var normalizedCode = LeaveType.NormalizeCode(command.Code);
        if (await repository.LeaveTypeCodeExistsAsync(normalizedCode, excludingId: null, cancellationToken))
            throw new InvalidOperationException("Leave type code must be unique.");

        var leaveType = LeaveType.Create(normalizedCode, command.Name, command.Description, command.SortOrder, command.IsActive ?? true, UtcNow());
        await repository.AddLeaveTypeAsync(leaveType, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(leaveType);
    }

    public async Task<LeaveTypeDto?> UpdateLeaveTypeAsync(Guid id, UpdateLeaveTypeCommand command, CancellationToken cancellationToken)
    {
        var leaveType = await repository.GetLeaveTypeAsync(id, cancellationToken);
        if (leaveType is null) return null;

        leaveType.UpdateDetails(command.Name, command.Description, command.SortOrder, command.IsActive, UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(leaveType);
    }

    public async Task<IReadOnlyList<BalanceBucketDto>> ListBalanceBucketsAsync(bool? isActive, CancellationToken cancellationToken)
    {
        var buckets = await repository.ListBalanceBucketsAsync(isActive, cancellationToken);
        return buckets.Select(ToDto).ToList();
    }

    public async Task<BalanceBucketDto?> GetBalanceBucketAsync(Guid id, CancellationToken cancellationToken)
    {
        var bucket = await repository.GetBalanceBucketAsync(id, cancellationToken);
        return bucket is null ? null : ToDto(bucket);
    }

    public async Task<BalanceBucketDto> CreateBalanceBucketAsync(CreateBalanceBucketCommand command, CancellationToken cancellationToken)
    {
        var normalizedCode = BalanceBucket.NormalizeCode(command.Code);
        if (await repository.BalanceBucketCodeExistsAsync(normalizedCode, excludingId: null, cancellationToken))
            throw new InvalidOperationException("Balance bucket code must be unique.");

        var bucket = BalanceBucket.Create(normalizedCode, command.Name, command.Description, ParseUnit(command.Unit), command.IsActive ?? true, UtcNow());
        await repository.AddBalanceBucketAsync(bucket, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(bucket);
    }

    public async Task<BalanceBucketDto?> UpdateBalanceBucketAsync(Guid id, UpdateBalanceBucketCommand command, CancellationToken cancellationToken)
    {
        var bucket = await repository.GetBalanceBucketAsync(id, cancellationToken);
        if (bucket is null) return null;

        bucket.UpdateDetails(command.Name, command.Description, ParseUnit(command.Unit), command.IsActive, UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(bucket);
    }

    public static BalanceBucketUnit ParseUnit(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Unit is required.", nameof(value));
        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "DAY" => BalanceBucketUnit.Day,
            _ => throw new ArgumentException("Balance bucket unit is invalid.", nameof(value))
        };
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static LeaveTypeDto ToDto(LeaveType leaveType) => new(leaveType.Id, leaveType.Code, leaveType.Name, leaveType.Description, leaveType.IsActive, leaveType.SortOrder, leaveType.CreatedAtUtc, leaveType.UpdatedAtUtc);
    private static BalanceBucketDto ToDto(BalanceBucket bucket) => new(bucket.Id, bucket.Code, bucket.Name, bucket.Description, ToCode(bucket.Unit), bucket.IsActive, bucket.CreatedAtUtc, bucket.UpdatedAtUtc);
    private static string ToCode(BalanceBucketUnit unit) => unit switch { BalanceBucketUnit.Day => "DAY", _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Balance bucket unit is invalid.") };
}
