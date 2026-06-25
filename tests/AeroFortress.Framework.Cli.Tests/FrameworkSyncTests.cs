using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AeroFortress.Framework.Cli;

namespace AeroFortress.Framework.Cli.Tests;

public class FrameworkSyncTests
{
    [Fact]
    public void An_app_with_no_aerofortress_references_is_in_sync()
    {
        var app = NewApp();

        var outcome = FrameworkSync.Check(app);

        Assert.True(outcome.Gating);
        Assert.True(outcome.InSync);
        Assert.Empty(outcome.Messages);
    }

    [Fact]
    public void A_stale_backend_version_fails_without_any_framework_checkout()
    {
        var app = NewApp();
        WriteCsproj(app, "Shop.Api.csproj", "AeroFortress.Framework", StaleVersion);

        var outcome = FrameworkSync.Check(app);

        Assert.True(outcome.Gating);
        Assert.False(outcome.InSync);
        Assert.Contains(outcome.Messages,
            m => m.Contains(StaleVersion) && m.Contains(FrameworkPackageVersions.Framework));
    }

    [Fact]
    public void The_version_this_doctor_ships_for_passes()
    {
        var app = NewApp();
        WriteCsproj(app, "Shop.Api.csproj",
            "AeroFortress.Framework.EntityFrameworkCore", FrameworkPackageVersions.Framework);

        var outcome = FrameworkSync.Check(app);

        Assert.True(outcome.InSync);
    }

    [Fact]
    public void A_legacy_plugin_copy_fails_even_without_a_frontend()
    {
        var app = NewApp();
        Directory.CreateDirectory(Path.Combine(app, "clients", "eslint-plugin-aerofortress"));

        var outcome = FrameworkSync.Check(app);

        Assert.False(outcome.InSync);
        Assert.Contains(outcome.Messages, m => m.Contains("legacy vendored"));
    }

    // The SSOT safety net: the version baked into the CLI (what scaffolds stamp and the gate enforces) must equal
    // the library props <Version> the packages are actually built with, so the two copies can never drift apart.
    [Fact]
    public void Baked_framework_version_matches_the_props_ssot()
    {
        var version = Regex.Match(File.ReadAllText(PropsPath()), @"<Version>([^<]+)</Version>");

        Assert.True(version.Success, "could not read <Version> from AeroFortress.Framework.Library.props");
        Assert.Equal(version.Groups[1].Value, FrameworkPackageVersions.Framework);
    }

    private const string StaleVersion = "0.0.1-stale";

    private static string NewApp() => Directory.CreateTempSubdirectory("aerofortress-sync-test").FullName;

    private static void WriteCsproj(string app, string file, string package, string version) =>
        File.WriteAllText(Path.Combine(app, file),
            $"""<Project><ItemGroup><PackageReference Include="{package}" Version="{version}" /></ItemGroup></Project>""");

    // The library props is the version SSOT; resolve it from this test's source location so the assertion holds
    // both locally and on CI (the checkout path is embedded at compile time and exists at test time).
    private static string PropsPath([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(thisFile)!, "..", "..", "build", "AeroFortress.Framework.Library.props"));
}
