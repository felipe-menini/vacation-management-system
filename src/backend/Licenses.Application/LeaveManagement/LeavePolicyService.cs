using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;

namespace Licenses.Application.LeaveManagement;

public sealed class LeavePolicyService(ILeavePolicyRepository repository, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<LeavePolicyDto>> ListPoliciesAsync(CancellationToken cancellationToken)
    {
        var policies = await repository.ListPoliciesAsync(cancellationToken);
        return await ToPolicyDtosAsync(policies, cancellationToken);
    }

    public async Task<LeavePolicyDto?> GetPolicyAsync(Guid id, CancellationToken cancellationToken)
    {
        var policy = await repository.GetPolicyAsync(id, cancellationToken);
        if (policy is null) return null;
        return (await ToPolicyDtosAsync([policy], cancellationToken)).Single();
    }

    public async Task<LeavePolicyDto> CreatePolicyAsync(CreateLeavePolicyCommand command, CancellationToken cancellationToken)
    {
        var leaveType = await repository.GetLeaveTypeAsync(command.LeaveTypeId, cancellationToken) ?? throw new InvalidOperationException("Leave type does not exist.");
        if (command.OrgUnitId is not null && await repository.GetOrgUnitAsync(command.OrgUnitId.Value, cancellationToken) is null) throw new InvalidOperationException("Organizational unit does not exist.");
        if (await repository.ExactPolicyScopeExistsAsync(command.LeaveTypeId, command.OrgUnitId, null, cancellationToken)) throw new InvalidOperationException("A policy already exists for this leave type and exact scope.");

        var policy = LeavePolicy.Create(leaveType.Id, command.OrgUnitId, command.AppliesToDescendants, command.IsActive ?? true, UtcNow());
        await repository.AddPolicyAsync(policy, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return (await ToPolicyDtosAsync([policy], cancellationToken)).Single();
    }

    public async Task<LeavePolicyDto?> UpdatePolicyAsync(Guid id, UpdateLeavePolicyCommand command, CancellationToken cancellationToken)
    {
        var policy = await repository.GetPolicyAsync(id, cancellationToken);
        if (policy is null) return null;
        if (await repository.HasPublishedVersionsAsync(id, cancellationToken) && (policy.OrgUnitId != command.OrgUnitId || policy.AppliesToDescendants != (command.OrgUnitId is null ? false : command.AppliesToDescendants)))
            throw new InvalidOperationException("Cannot change scope for a policy with published history.");
        if (command.OrgUnitId is not null && await repository.GetOrgUnitAsync(command.OrgUnitId.Value, cancellationToken) is null) throw new InvalidOperationException("Organizational unit does not exist.");
        if (await repository.ExactPolicyScopeExistsAsync(policy.LeaveTypeId, command.OrgUnitId, id, cancellationToken)) throw new InvalidOperationException("A policy already exists for this leave type and exact scope.");

        policy.UpdateScope(command.OrgUnitId, command.AppliesToDescendants, command.IsActive, UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return (await ToPolicyDtosAsync([policy], cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<LeavePolicyVersionDto>> ListVersionsAsync(Guid policyId, CancellationToken cancellationToken)
    {
        if (await repository.GetPolicyAsync(policyId, cancellationToken) is null) return [];
        var versions = await repository.ListVersionsAsync(policyId, cancellationToken);
        return await ToVersionDtosAsync(versions, cancellationToken);
    }

    public async Task<LeavePolicyVersionDto?> GetVersionAsync(Guid id, CancellationToken cancellationToken)
    {
        var version = await repository.GetVersionAsync(id, cancellationToken);
        if (version is null) return null;
        return (await ToVersionDtosAsync([version], cancellationToken)).Single();
    }

    public async Task<LeavePolicyVersionDto> CreateVersionAsync(Guid policyId, CreateLeavePolicyVersionCommand command, CancellationToken cancellationToken)
    {
        var policy = await repository.GetPolicyAsync(policyId, cancellationToken) ?? throw new InvalidOperationException("Leave policy does not exist.");
        await ValidateRuleReferencesAsync(policy, command.ConsumesBalance, command.BalanceBucketId, requireActiveForPublish: false, cancellationToken);
        var nextVersion = await repository.GetNextVersionNumberAsync(policyId, cancellationToken);
        var version = LeavePolicyVersion.CreateDraft(policyId, nextVersion, command.EffectiveFrom, command.EffectiveTo, ParseDayCountMode(command.DayCountMode), command.AllowHalfDay, command.MinimumNoticeDays, ParseDayCountMode(command.NoticeDayCountMode), command.MaximumRequestDays, ParseOverlapBehavior(command.OverlapBehavior), command.ConsumesBalance, command.BalanceBucketId, UtcNow());
        await repository.AddVersionAsync(version, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return (await ToVersionDtosAsync([version], cancellationToken)).Single();
    }

    public async Task<LeavePolicyVersionDto?> UpdateVersionAsync(Guid id, UpdateLeavePolicyVersionCommand command, CancellationToken cancellationToken)
    {
        var version = await repository.GetVersionAsync(id, cancellationToken);
        if (version is null) return null;
        var policy = await repository.GetPolicyAsync(version.LeavePolicyId, cancellationToken) ?? throw new InvalidOperationException("Leave policy does not exist.");
        await ValidateRuleReferencesAsync(policy, command.ConsumesBalance, command.BalanceBucketId, requireActiveForPublish: false, cancellationToken);
        version.UpdateDraft(command.EffectiveFrom, command.EffectiveTo, ParseDayCountMode(command.DayCountMode), command.AllowHalfDay, command.MinimumNoticeDays, ParseDayCountMode(command.NoticeDayCountMode), command.MaximumRequestDays, ParseOverlapBehavior(command.OverlapBehavior), command.ConsumesBalance, command.BalanceBucketId, UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return (await ToVersionDtosAsync([version], cancellationToken)).Single();
    }

    public async Task<LeavePolicyVersionDto?> PublishVersionAsync(Guid id, CancellationToken cancellationToken)
    {
        var version = await repository.GetVersionAsync(id, cancellationToken);
        if (version is null) return null;
        var policy = await repository.GetPolicyAsync(version.LeavePolicyId, cancellationToken) ?? throw new InvalidOperationException("Leave policy does not exist.");
        if (!policy.IsActive) throw new InvalidOperationException("Inactive policies cannot publish new versions.");
        await ValidateRuleReferencesAsync(policy, version.ConsumesBalance, version.BalanceBucketId, requireActiveForPublish: true, cancellationToken);
        if (await repository.HasOverlappingPublishedVersionAsync(version.LeavePolicyId, version.EffectiveFrom, version.EffectiveTo, version.Id, cancellationToken)) throw new InvalidOperationException("Published policy effective periods cannot overlap.");
        version.Publish(UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return (await ToVersionDtosAsync([version], cancellationToken)).Single();
    }

    public async Task<ResolveLeavePolicyResultDto> ResolvePolicyAsync(Guid leaveTypeId, Guid? targetOrgUnitId, DateOnly date, CancellationToken cancellationToken)
    {
        var policies = await repository.ListPoliciesForLeaveTypeWithPublishedVersionsAsync(leaveTypeId, date, cancellationToken);
        var activePolicies = policies.Where(x => x.IsActive).ToList();
        if (activePolicies.Count == 0) return new(false, null, null, "No published applicable policy exists.");

        var orgUnits = await repository.ListOrgUnitsAsync(cancellationToken);
        var ancestors = targetOrgUnitId is null ? [] : GetAncestorChain(targetOrgUnitId.Value, orgUnits);
        var candidates = activePolicies.Select(policy => new { Policy = policy, Depth = GetScopeDepth(policy, targetOrgUnitId, ancestors) }).Where(x => x.Depth is not null).OrderByDescending(x => x.Depth!.Value).ToList();
        if (candidates.Count == 0) return new(false, null, null, "No policy applies to the requested scope.");

        var selectedPolicy = candidates.First().Policy;
        var selectedVersion = selectedPolicy.Versions.Where(IsPublishedEffective(date)).OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.VersionNumber).First();
        return new(true, (await ToPolicyDtosAsync([selectedPolicy], cancellationToken)).Single(), (await ToVersionDtosAsync([selectedVersion], cancellationToken)).Single(), null);
    }

    private async Task ValidateRuleReferencesAsync(LeavePolicy policy, bool consumesBalance, Guid? balanceBucketId, bool requireActiveForPublish, CancellationToken cancellationToken)
    {
        var leaveType = await repository.GetLeaveTypeAsync(policy.LeaveTypeId, cancellationToken) ?? throw new InvalidOperationException("Leave type does not exist.");
        if (requireActiveForPublish && !leaveType.IsActive) throw new InvalidOperationException("Inactive leave types cannot receive newly published policy versions.");
        if (!consumesBalance)
        {
            if (balanceBucketId is not null) throw new InvalidOperationException("BalanceBucketId must be empty when ConsumesBalance is false.");
            return;
        }

        if (balanceBucketId is not { } requiredBalanceBucketId) throw new InvalidOperationException("BalanceBucketId is required when ConsumesBalance is true.");
        var bucket = await repository.GetBalanceBucketAsync(requiredBalanceBucketId, cancellationToken) ?? throw new InvalidOperationException("Balance bucket does not exist.");
        if (requireActiveForPublish && !bucket.IsActive) throw new InvalidOperationException("Inactive balance buckets cannot be used by newly published policy versions.");
    }

    private async Task<List<LeavePolicyDto>> ToPolicyDtosAsync(IEnumerable<LeavePolicy> policies, CancellationToken cancellationToken)
    {
        var leaveTypes = (await Task.WhenAll(policies.Select(x => repository.GetLeaveTypeAsync(x.LeaveTypeId, cancellationToken)))).Where(x => x is not null).ToDictionary(x => x!.Id, x => x!);
        var orgIds = policies.Where(x => x.OrgUnitId is not null).Select(x => x.OrgUnitId!.Value).Distinct().ToList();
        var orgs = (await Task.WhenAll(orgIds.Select(x => repository.GetOrgUnitAsync(x, cancellationToken)))).Where(x => x is not null).ToDictionary(x => x!.Id, x => x!);
        return policies.Select(x => new LeavePolicyDto(x.Id, x.LeaveTypeId, leaveTypes[x.LeaveTypeId].Code, leaveTypes[x.LeaveTypeId].Name, x.OrgUnitId, x.OrgUnitId is null ? null : orgs[x.OrgUnitId.Value].Code, x.OrgUnitId is null ? null : orgs[x.OrgUnitId.Value].Name, x.AppliesToDescendants, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc)).ToList();
    }

    private async Task<List<LeavePolicyVersionDto>> ToVersionDtosAsync(IEnumerable<LeavePolicyVersion> versions, CancellationToken cancellationToken)
    {
        var bucketIds = versions.Where(x => x.BalanceBucketId is not null).Select(x => x.BalanceBucketId!.Value).Distinct().ToList();
        var buckets = (await Task.WhenAll(bucketIds.Select(x => repository.GetBalanceBucketAsync(x, cancellationToken)))).Where(x => x is not null).ToDictionary(x => x!.Id, x => x!);
        return versions.Select(x => new LeavePolicyVersionDto(x.Id, x.LeavePolicyId, x.VersionNumber, ToStatusCode(x.Status), x.EffectiveFrom, x.EffectiveTo, ToDayCountCode(x.DayCountMode), x.AllowHalfDay, x.MinimumNoticeDays, ToDayCountCode(x.NoticeDayCountMode), x.MaximumRequestDays, ToOverlapCode(x.OverlapBehavior), x.ConsumesBalance, x.BalanceBucketId, x.BalanceBucketId is null ? null : buckets[x.BalanceBucketId.Value].Code, x.BalanceBucketId is null ? null : buckets[x.BalanceBucketId.Value].Name, x.CreatedAtUtc, x.UpdatedAtUtc, x.PublishedAtUtc)).ToList();
    }

    private static Func<LeavePolicyVersion, bool> IsPublishedEffective(DateOnly date) => x => x.Status == LeavePolicyVersionStatus.Published && x.EffectiveFrom <= date && (x.EffectiveTo is null || x.EffectiveTo >= date);
    private static int? GetScopeDepth(LeavePolicy policy, Guid? targetOrgUnitId, IReadOnlyList<Guid> ancestorChain)
    {
        if (policy.OrgUnitId is null) return 0;
        if (targetOrgUnitId is null) return null;
        if (policy.OrgUnitId == targetOrgUnitId) return ancestorChain.Count;
        if (!policy.AppliesToDescendants) return null;
        var index = -1;
        for (var i = 0; i < ancestorChain.Count; i++)
        {
            if (ancestorChain[i] == policy.OrgUnitId.Value)
            {
                index = i;
                break;
            }
        }
        return index >= 0 ? index + 1 : null;
    }

    private static List<Guid> GetAncestorChain(Guid targetOrgUnitId, IReadOnlyList<OrgUnit> orgUnits)
    {
        var byId = orgUnits.ToDictionary(x => x.Id);
        var result = new List<Guid>();
        var current = targetOrgUnitId;
        while (byId.TryGetValue(current, out var unit))
        {
            result.Insert(0, current);
            if (unit.ParentId is null) break;
            current = unit.ParentId.Value;
        }
        return result;
    }

    public static PolicyDayCountMode ParseDayCountMode(string value) => value?.Trim().ToUpperInvariant() switch { "BUSINESS_DAYS" => PolicyDayCountMode.BusinessDays, "CALENDAR_DAYS" => PolicyDayCountMode.CalendarDays, _ => throw new ArgumentException("Day count mode is invalid.") };
    public static PolicyOverlapBehavior ParseOverlapBehavior(string value) => value?.Trim().ToUpperInvariant() switch { "BLOCK" => PolicyOverlapBehavior.Block, "WARN" => PolicyOverlapBehavior.Warn, "ALLOW" => PolicyOverlapBehavior.Allow, _ => throw new ArgumentException("Overlap behavior is invalid.") };
    private static string ToStatusCode(LeavePolicyVersionStatus status) => status switch { LeavePolicyVersionStatus.Draft => "DRAFT", LeavePolicyVersionStatus.Published => "PUBLISHED", _ => throw new ArgumentOutOfRangeException(nameof(status)) };
    private static string ToDayCountCode(PolicyDayCountMode mode) => mode switch { PolicyDayCountMode.BusinessDays => "BUSINESS_DAYS", PolicyDayCountMode.CalendarDays => "CALENDAR_DAYS", _ => throw new ArgumentOutOfRangeException(nameof(mode)) };
    private static string ToOverlapCode(PolicyOverlapBehavior behavior) => behavior switch { PolicyOverlapBehavior.Block => "BLOCK", PolicyOverlapBehavior.Warn => "WARN", PolicyOverlapBehavior.Allow => "ALLOW", _ => throw new ArgumentOutOfRangeException(nameof(behavior)) };
    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}

