namespace AeroFortress.Framework.Cli;

/// <summary>
/// Package versions emitted by the CLI scaffolds. Keeping the framework package line in one place prevents
/// generators from silently stamping historical versions into otherwise-current applications.
/// </summary>
internal static class FrameworkPackageVersions
{
    /// <summary>The current AeroFortress.Framework.* release consumed by generated applications.</summary>
    public const string Framework = "2.2.3";
}
