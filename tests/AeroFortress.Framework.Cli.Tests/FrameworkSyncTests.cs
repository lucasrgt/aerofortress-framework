using AeroFortress.Framework.Cli;

namespace AeroFortress.Framework.Cli.Tests;

public class FrameworkSyncTests
{
    [Fact]
    public void No_framework_section_is_a_notice_not_a_gate()
    {
        var app = NewApp("[workspace]\nname = \"shop\"\n");

        var outcome = FrameworkSync.Check(app);

        Assert.False(outcome.Gating);
        Assert.True(outcome.InSync);
        Assert.Single(outcome.Messages);
    }

    [Fact]
    public void An_unreachable_repo_skips_instead_of_failing_the_ci_machine()
    {
        var app = NewApp("[workspace]\nname = \"shop\"\n\n[framework]\nrepo = \"../nowhere\"\n");

        var outcome = FrameworkSync.Check(app);

        Assert.False(outcome.Gating);
        Assert.True(outcome.InSync);
    }

    [Fact]
    public void A_stale_package_version_fails_the_sync()
    {
        var (app, _) = NewPair(frameworkVersion: "0.2.0", appPackageVersion: "0.1.0");

        var outcome = FrameworkSync.Check(app);

        Assert.True(outcome.Gating);
        Assert.False(outcome.InSync);
        Assert.Contains(outcome.Messages, m => m.Contains("0.2.0") && m.Contains("0.1.0"));
    }

    [Fact]
    public void A_drifted_plugin_mirror_fails_the_sync()
    {
        var (app, repo) = NewPair(frameworkVersion: "0.2.0", appPackageVersion: "0.2.0");
        WritePlugin(repo, "// canonical v2");
        WriteMirror(app, "// stale v1");

        var outcome = FrameworkSync.Check(app);

        Assert.False(outcome.InSync);
        Assert.Contains(outcome.Messages, m => m.Contains("eslint-plugin-aerofortress"));
    }

    [Fact]
    public void Matching_version_and_mirror_pass_even_across_line_endings()
    {
        var (app, repo) = NewPair(frameworkVersion: "0.2.0", appPackageVersion: "0.2.0");
        WritePlugin(repo, "// same rules\n");
        WriteMirror(app, "// same rules\r\n");   // a CRLF copy is not drift

        var outcome = FrameworkSync.Check(app);

        Assert.True(outcome.Gating);
        Assert.True(outcome.InSync);
    }

    private static string NewApp(string manifest)
    {
        var root = Directory.CreateTempSubdirectory("aerofortress-sync-test").FullName;
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), manifest);
        return root;
    }

    // An app + a framework checkout side by side, with the manifest pointing at the checkout.
    private static (string App, string Repo) NewPair(string frameworkVersion, string appPackageVersion)
    {
        var parent = Directory.CreateTempSubdirectory("aerofortress-sync-pair").FullName;
        var repo = Path.Combine(parent, "aerofortress-framework");
        var app = Path.Combine(parent, "app");
        Directory.CreateDirectory(Path.Combine(repo, "build"));
        Directory.CreateDirectory(app);

        File.WriteAllText(Path.Combine(repo, "build", "AeroFortress.Framework.Library.props"),
            $"<Project><PropertyGroup><Version>{frameworkVersion}</Version></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(app, "AeroFortress.toml"),
            "[workspace]\nname = \"shop\"\n\n[framework]\nrepo = \"../aerofortress-framework\"\n");
        File.WriteAllText(Path.Combine(app, "Shop.Api.csproj"),
            $"""<Project><ItemGroup><PackageReference Include="AeroFortress" Version="{appPackageVersion}" /></ItemGroup></Project>""");
        return (app, repo);
    }

    private static void WritePlugin(string repo, string content)
    {
        var dir = Path.Combine(repo, "frontend-sdk", "packages", "eslint-plugin");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.cjs"), content);
    }

    private static void WriteMirror(string app, string content)
    {
        var dir = Path.Combine(app, "clients", "eslint-plugin-aerofortress");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.cjs"), content);
    }
}
