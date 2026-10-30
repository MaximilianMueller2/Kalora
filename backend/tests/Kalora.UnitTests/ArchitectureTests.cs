namespace Kalora.UnitTests;

/// <summary>Source-level guards for rules in CLAUDE.md that the compiler cannot enforce.</summary>
public class ArchitectureTests
{
    // Files that may bypass tenant isolation. Extend this list consciously (platform admin, registration, scheduler).
    private static readonly string[] TenantBypassAllowedFiles =
    [
        "Kalora.Application/Auth/AuthService.cs", // password reset: finds the salon of a user who is not signed in
        "Kalora.Application/Auth/InvitationService.cs", // the invitation token identifies the salon
        "Kalora.Application/Auth/MeResponseFactory.cs", // profile of a user right after login, before a tenant exists
        "Kalora.Application/Common/IAppDbContext.cs",
        "Kalora.Application/Email/EmailOutboxProcessor.cs", // the mail sender is a background job without a tenant
        "Kalora.Application/Appointments/AppointmentScheduler.cs", // the scheduler is a background job without a tenant
        "Kalora.Application/Calendar/AppointmentCalendarService.cs", // the management token, not a salon, identifies the appointment
        "Kalora.Application/Salons/SlugAllocator.cs", // slugs are unique across all salons
        "Kalora.Application/Tenancy/SalonResolver.cs",
        "Kalora.Infrastructure/Identity/KaloraClaimsPrincipalFactory.cs", // reads the membership while the cookie is created
        "Kalora.Infrastructure/Persistence/AppDbContext.cs",
    ];

    [Fact]
    public void Only_allowed_files_bypass_the_tenant_filter()
    {
        var offenders = SourceFiles("src")
            .Where(file => File.ReadAllText(file).Contains("IgnoreQueryFilters") || UsesBypassMethod(File.ReadAllText(file)))
            .Select(RelativeToSrc)
            .Where(path => !TenantBypassAllowedFiles.Contains(path))
            .ToList();

        Assert.True(offenders.Count == 0, $"Tenant filter bypassed in: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Controllers_do_not_touch_the_database()
    {
        var offenders = SourceFiles("src/Kalora.Api/Controllers")
            .Where(file => File.ReadAllText(file).Contains("DbContext"))
            .Select(RelativeToSrc)
            .ToList();

        Assert.True(offenders.Count == 0, $"DbContext used in controllers: {string.Join(", ", offenders)}");
    }

    private static bool UsesBypassMethod(string source) =>
        source.Contains(".AllSalons(") || source.Contains(".AllTenants<");

    private static IEnumerable<string> SourceFiles(string relativeDirectory) =>
        Directory.EnumerateFiles(Path.Combine(BackendRoot(), relativeDirectory), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"));

    private static string RelativeToSrc(string file) =>
        Path.GetRelativePath(Path.Combine(BackendRoot(), "src"), file).Replace('\\', '/');

    private static string BackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kalora.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Kalora.slnx not found above the test output directory.");
    }
}
