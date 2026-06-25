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
    public void A_stale_frontend_package_fails_the_sync()
    {
        var (app, repo) = NewPair(frameworkVersion: "0.2.0", appPackageVersion: "0.2.0");
        WriteFrontendPackages(repo, pluginVersion: "0.11.0", reactVersion: "0.6.0", sdkVersion: "0.1.0");
        WriteFrontend(app, pluginVersion: "^0.10.0", reactVersion: "^0.6.0", sdkVersion: "^0.1.0");

        var outcome = FrameworkSync.Check(app);

        Assert.False(outcome.InSync);
        Assert.Contains(outcome.Messages, m => m.Contains("eslint-plugin-aerofortress") && m.Contains("0.10.0"));
    }

    [Fact]
    public void Matching_backend_and_frontend_package_versions_pass()
    {
        var (app, repo) = NewPair(frameworkVersion: "0.2.0", appPackageVersion: "0.2.0");
        WriteFrontendPackages(repo, pluginVersion: "0.11.0", reactVersion: "0.6.0", sdkVersion: "0.1.0");
        WriteFrontend(app, pluginVersion: "^0.11.0", reactVersion: "^0.6.0", sdkVersion: "^0.1.0");

        var outcome = FrameworkSync.Check(app);

        Assert.True(outcome.Gating);
        Assert.True(outcome.InSync);
    }

    [Fact]
    public void A_legacy_plugin_copy_fails_even_without_a_frontend()
    {
        var (app, _) = NewPair(frameworkVersion: "0.2.0", appPackageVersion: "0.2.0");
        Directory.CreateDirectory(Path.Combine(app, "clients", "eslint-plugin-aerofortress"));

        var outcome = FrameworkSync.Check(app);

        Assert.False(outcome.InSync);
        Assert.Contains(outcome.Messages, m => m.Contains("legacy vendored"));
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

    private static void WriteFrontendPackages(string repo, string pluginVersion, string reactVersion, string sdkVersion)
    {
        WritePackage(
            Path.Combine(repo, "frontend-sdk"),
            "@aerofortress/frontend-sdk",
            sdkVersion);
        WritePackage(
            Path.Combine(repo, "frontend-sdk", "packages", "eslint-plugin"),
            "eslint-plugin-aerofortress",
            pluginVersion);
        WritePackage(
            Path.Combine(repo, "frontend-sdk", "packages", "aerofortress-react"),
            "@aerofortress/react",
            reactVersion);
    }

    private static void WriteFrontend(string app, string pluginVersion, string reactVersion, string sdkVersion)
    {
        var dir = Path.Combine(app, "clients", "web");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "eslint.config.mjs"), "export default [];");
        File.WriteAllText(Path.Combine(dir, "package.json"),
            $$"""
            {
              "dependencies": {
                "@aerofortress/react": "{{reactVersion}}"
              },
              "devDependencies": {
                "@aerofortress/frontend-sdk": "{{sdkVersion}}",
                "eslint-plugin-aerofortress": "{{pluginVersion}}"
              }
            }
            """);
    }

    private static void WritePackage(string dir, string name, string version)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "package.json"),
            $$"""{"name":"{{name}}","version":"{{version}}"}""");
    }
}
