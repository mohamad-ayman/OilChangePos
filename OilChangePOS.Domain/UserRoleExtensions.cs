namespace OilChangePOS.Domain;

public static class UserRoleExtensions
{
    public static bool IsAdmin(this UserRole role) => role == UserRole.Admin;

    public static bool IsBranchStaff(this UserRole role) =>
        role is UserRole.Manager or UserRole.Cashier;
}
