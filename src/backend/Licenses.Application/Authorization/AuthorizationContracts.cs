namespace Licenses.Application.Authorization;

public static class PermissionCodes
{
    public const string OrgUnitsRead = "org.units.read";
    public const string OrgUnitsManage = "org.units.manage";
    public const string OrgUsersRead = "org.users.read";
    public const string OrgUsersManage = "org.users.manage";
    public const string OrgAssignmentsRead = "org.assignments.read";
    public const string OrgAssignmentsManage = "org.assignments.manage";
    public const string LeaveCatalogRead = "leave.catalog.read";
    public const string LeaveCatalogManage = "leave.catalog.manage";
    public const string LeavePoliciesRead = "leave.policies.read";
    public const string LeavePoliciesManage = "leave.policies.manage";
    public const string LeaveCalendarsRead = "leave.calendars.read";
    public const string LeaveCalendarsManage = "leave.calendars.manage";
}

public sealed record CurrentActor(Guid UserId);
public sealed record DevelopmentActorDto(Guid Id, string DisplayName, string Email, string? PrimaryOrgUnitName);
