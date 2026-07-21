using AeroFortress.Framework.Cli;

namespace AeroFortress.Framework.Cli.Tests;

public class AeroFortressManifestTests
{
    [Fact]
    public void A_missing_manifest_is_a_notice_not_a_failure()
    {
        var root = NewDir();

        var outcome = AeroFortressManifest.Validate(root);

        Assert.False(outcome.Present);
        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, m => m.Contains("AeroFortress.toml"));
    }

    [Fact]
    public void A_well_formed_manifest_whose_paths_exist_is_valid()
    {
        var root = NewDir();
        var backend = Path.Combine(root, "src", "MyApp.Api");
        Directory.CreateDirectory(backend);
        WriteLaunchSettings(backend, environment: "Development", applicationUrl: "http://localhost:8080");
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "MyApp"

            [products.app]
            backend = "src/MyApp.Api"
            """);

        var outcome = AeroFortressManifest.Validate(root);

        Assert.True(outcome.Present);
        Assert.True(outcome.Valid);
        Assert.Empty(outcome.Messages);
    }

    [Fact]
    public void A_backend_without_launchSettings_is_reported()
    {
        // The pilot bug: no launchSettings → a dev run defaults to Production on the .NET default port (port drift
        // + rate limiting → 429). The doctor must catch the missing file, not the runtime.
        var root = NewDir();
        Directory.CreateDirectory(Path.Combine(root, "src", "MyApp.Api"));
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "MyApp"

            [products.app]
            backend = "src/MyApp.Api"
            """);

        var outcome = AeroFortressManifest.Validate(root);

        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, m => m.Contains("launchSettings.json") && m.Contains("Production"));
    }

    [Fact]
    public void A_backend_launchSettings_without_Development_is_reported()
    {
        var root = NewDir();
        var backend = Path.Combine(root, "src", "MyApp.Api");
        Directory.CreateDirectory(backend);
        // Pins a port but leaves the environment unset → a dev run is still Production.
        WriteLaunchSettings(backend, environment: null, applicationUrl: "http://localhost:8080");
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "MyApp"

            [products.app]
            backend = "src/MyApp.Api"
            """);

        var outcome = AeroFortressManifest.Validate(root);

        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, m => m.Contains("ASPNETCORE_ENVIRONMENT=Development"));
    }

    [Fact]
    public void A_core_path_needs_no_launchSettings()
    {
        // `core` is the frontend data layer, not a .NET API — the dev-env check applies only to a backend.
        var root = NewDir();
        var backend = Path.Combine(root, "src", "MyApp.Api");
        Directory.CreateDirectory(backend);
        WriteLaunchSettings(backend, environment: "Development", applicationUrl: "http://localhost:8080");
        Directory.CreateDirectory(Path.Combine(root, "clients", "core"));
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "MyApp"

            [products.app]
            backend = "src/MyApp.Api"
            core = "clients/core"
            """);

        var outcome = AeroFortressManifest.Validate(root);

        Assert.True(outcome.Valid);
        Assert.Empty(outcome.Messages);
    }

    [Fact]
    public void A_declared_backend_path_that_does_not_exist_is_reported()
    {
        var root = NewDir();
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "MyApp"

            [products.app]
            backend = "src/MyApp.Api"
            """);

        var outcome = AeroFortressManifest.Validate(root);

        Assert.True(outcome.Present);
        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, m => m.Contains("src/MyApp.Api") && m.Contains("does not exist"));
    }

    [Fact]
    public void A_declared_frontend_must_be_an_executable_package_root()
    {
        var root = NewDir();
        Directory.CreateDirectory(Path.Combine(root, "apps", "web"));
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "MyApp"

            [products.app]
            frontend = "apps/web"
            """);

        var outcome = AeroFortressManifest.Validate(root);

        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, m => m.Contains("apps/web") && m.Contains("package.json"));
    }

    [Fact]
    public void Verification_has_no_manifest_mode()
    {
        var root = NewDir();
        var backend = Path.Combine(root, "src", "MyApp.Api");
        Directory.CreateDirectory(backend);
        WriteLaunchSettings(backend, environment: "Development", applicationUrl: "http://localhost:8080");
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "MyApp"

            [products.app]
            backend = "src/MyApp.Api"

            [proofs]
            depth = "complete"
            """);

        var outcome = AeroFortressManifest.Validate(root);

        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, message => message.Contains("unsupported section [proofs]")
            && message.Contains("no configurable mode"));
    }

    [Fact]
    public void A_frontend_proof_package_omitted_from_products_is_reported()
    {
        var root = NewDir();
        var app = Path.Combine(root, "apps", "web");
        Directory.CreateDirectory(Path.Combine(app, "src", "features"));
        File.WriteAllText(Path.Combine(app, "package.json"), "{}");
        File.WriteAllText(Path.Combine(app, "src", "features", "Checkout.viewModel.ts"), "export {};");
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "MyApp"
            """);

        var outcome = AeroFortressManifest.Validate(root);

        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, message => message.Contains("apps") && message.Contains("not declared"));
    }

    [Fact]
    public void A_declared_frontend_proof_package_is_valid_inventory()
    {
        var root = NewDir();
        var app = Path.Combine(root, "apps", "web");
        Directory.CreateDirectory(Path.Combine(app, "src", "features"));
        File.WriteAllText(Path.Combine(app, "package.json"), "{}");
        File.WriteAllText(Path.Combine(app, "src", "features", "Checkout.viewModel.ts"), "export {};");
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "MyApp"

            [products.app]
            frontend = "apps/web"
            """);

        var outcome = AeroFortressManifest.Validate(root);

        Assert.True(outcome.Valid);
        Assert.Empty(outcome.Messages);
    }

    [Fact]
    public void A_manifest_without_an_af_gate_workflow_is_reported()
    {
        var root = NewDir(withGateWorkflow: false);
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "MyApp"
            """);

        var outcome = AeroFortressManifest.Validate(root);

        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, message => message.Contains("af gate") && message.Contains("workflow"));
    }

    [Fact]
    public void A_comment_that_mentions_af_gate_does_not_wire_the_workflow()
    {
        var root = NewDir(withGateWorkflow: false);
        var workflows = Path.Combine(root, ".github", "workflows");
        Directory.CreateDirectory(workflows);
        File.WriteAllText(Path.Combine(workflows, "ci.yml"), "# remember to run af gate\n");
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "MyApp"
            """);

        var outcome = AeroFortressManifest.Validate(root);

        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, message => message.Contains("af gate") && message.Contains("workflow"));
    }

    [Theory]
    [InlineData("on: [pull_request]\njobs:\n  gate:\n    steps:\n      - run: af gate || true\n")]
    [InlineData("on: [pull_request]\njobs:\n  gate:\n    continue-on-error: true\n    steps:\n      - run: af gate\n")]
    [InlineData("on: [push]\njobs:\n  gate:\n    steps:\n      - run: af gate\n")]
    public void A_gate_whose_verdict_can_be_ignored_is_not_wired(string yaml)
    {
        var root = NewDir(withGateWorkflow: false);
        var workflows = Path.Combine(root, ".github", "workflows");
        Directory.CreateDirectory(workflows);
        File.WriteAllText(Path.Combine(workflows, "ci.yml"), yaml);
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "MyApp"
            """);

        var outcome = AeroFortressManifest.Validate(root);

        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, message => message.Contains("af gate") && message.Contains("workflow"));
    }

    [Fact]
    public void A_manifest_without_a_workspace_section_is_reported()
    {
        var root = NewDir();
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), "[products.app]\nbackend = \"src\"\n");
        Directory.CreateDirectory(Path.Combine(root, "src"));

        var outcome = AeroFortressManifest.Validate(root);

        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, m => m.Contains("[workspace]"));
    }

    private static string NewDir(bool withGateWorkflow = true)
    {
        var dir = Path.Combine(Path.GetTempPath(), "aerofortress-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        if (withGateWorkflow)
        {
            var workflows = Path.Combine(dir, ".github", "workflows");
            Directory.CreateDirectory(workflows);
            File.WriteAllText(Path.Combine(workflows, "ci.yml"), "on: [pull_request]\njobs:\n  gate:\n    steps:\n      - run: af gate\n");
        }
        return dir;
    }

    /// <summary>Write a Properties/launchSettings.json under <paramref name="backendDir"/> with an optional
    /// environment + applicationUrl, so a manifest test can model a pinned (or unpinned) dev environment.</summary>
    private static void WriteLaunchSettings(string backendDir, string? environment, string? applicationUrl)
    {
        var properties = Path.Combine(backendDir, "Properties");
        Directory.CreateDirectory(properties);
        var env = environment is null ? "" : $"\n        \"environmentVariables\": {{ \"ASPNETCORE_ENVIRONMENT\": \"{environment}\" }},";
        var url = applicationUrl is null ? "" : $"\n        \"applicationUrl\": \"{applicationUrl}\",";
        File.WriteAllText(Path.Combine(properties, "launchSettings.json"), $$"""
            {
              "profiles": {
                "App": {
                  "commandName": "Project",{{url}}{{env}}
                  "dotnetRunMessages": true
                }
              }
            }
            """);
    }
}
