using AeroFortress.Framework.Cli;

namespace AeroFortress.Framework.Cli.Tests;

public class DoctorCommandTests
{
    [Fact]
    public void Every_package_under_clients_is_gated_even_when_its_eslint_config_is_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "af-doctor-clients-" + Guid.NewGuid().ToString("N"));
        var web = Path.Combine(root, "clients", "web");
        var nonPackage = Path.Combine(root, "clients", "notes");
        Directory.CreateDirectory(web);
        Directory.CreateDirectory(nonPackage);
        File.WriteAllText(Path.Combine(web, "package.json"), "{}");

        try
        {
            var client = Assert.Single(DoctorCommand.FrontendClients(root));
            Assert.Equal(web, client);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void An_explicit_harness_frontend_is_the_only_gate_coordinator()
    {
        var root = Path.Combine(Path.GetTempPath(), "af-doctor-harness-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "apps", "web");
        var unrelated = Path.Combine(root, "clients", "design-tokens");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(unrelated);
        File.WriteAllText(Path.Combine(app, "package.json"), "{}");
        File.WriteAllText(Path.Combine(unrelated, "package.json"), "{}");
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "App"

            [harness]
            frontend = "apps/web"
            """);

        try
        {
            Assert.Equal(app, Assert.Single(DoctorCommand.FrontendClients(root)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_nested_product_core_resolves_to_its_owning_package()
    {
        var root = Path.Combine(Path.GetTempPath(), "af-doctor-core-" + Guid.NewGuid().ToString("N"));
        var package = Path.Combine(root, "clients", "app");
        Directory.CreateDirectory(Path.Combine(package, "core"));
        File.WriteAllText(Path.Combine(package, "package.json"), "{}");
        File.WriteAllText(Path.Combine(root, "AeroFortress.toml"), """
            [workspace]
            name = "App"

            [products.app]
            core = "clients/app/core"
            """);

        try
        {
            Assert.Equal(package, Assert.Single(DoctorCommand.FrontendClients(root)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
