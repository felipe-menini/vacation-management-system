namespace Licenses.Domain.Authorization;

public sealed class RolePermission
{
    private RolePermission() { }

    private RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId == Guid.Empty ? throw new ArgumentException("Role id is required.", nameof(roleId)) : roleId;
        PermissionId = permissionId == Guid.Empty ? throw new ArgumentException("Permission id is required.", nameof(permissionId)) : permissionId;
    }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    public static RolePermission Create(Guid roleId, Guid permissionId) => new(roleId, permissionId);
}
