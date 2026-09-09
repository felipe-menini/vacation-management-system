using Licenses.Domain.LeaveManagement;

namespace Licenses.Application.LeaveManagement;

public sealed record LeaveTypeDto(Guid Id, string Code, string Name, string? Description, bool IsActive, int SortOrder, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record CreateLeaveTypeCommand(string Code, string Name, string? Description, bool? IsActive, int SortOrder);
public sealed record UpdateLeaveTypeCommand(string Name, string? Description, bool IsActive, int SortOrder);

public sealed record BalanceBucketDto(Guid Id, string Code, string Name, string? Description, string Unit, bool IsActive, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record CreateBalanceBucketCommand(string Code, string Name, string? Description, string Unit, bool? IsActive);
public sealed record UpdateBalanceBucketCommand(string Name, string? Description, string Unit, bool IsActive);

public interface ILeaveCatalogRepository
{
    Task<List<LeaveType>> ListLeaveTypesAsync(bool? isActive, CancellationToken cancellationToken);
    Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> LeaveTypeCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken);
    Task AddLeaveTypeAsync(LeaveType leaveType, CancellationToken cancellationToken);

    Task<List<BalanceBucket>> ListBalanceBucketsAsync(bool? isActive, CancellationToken cancellationToken);
    Task<BalanceBucket?> GetBalanceBucketAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> BalanceBucketCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken);
    Task AddBalanceBucketAsync(BalanceBucket balanceBucket, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
