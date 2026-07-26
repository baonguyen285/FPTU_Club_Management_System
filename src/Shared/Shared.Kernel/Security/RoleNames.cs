namespace Shared.Kernel.Security;

public static class SystemRoleNames
{
    public const string StudentAffairsAdmin = "StudentAffairsAdmin";
    public const string ClubManager = "ClubManager";
    public const string Student = "Student";

    public static readonly IReadOnlySet<string> Canonical =
        new HashSet<string>(StringComparer.Ordinal)
        {
            StudentAffairsAdmin,
            ClubManager,
            Student
        };
}
