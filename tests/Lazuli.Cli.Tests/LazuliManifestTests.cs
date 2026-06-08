using Lazuli.Cli;

namespace Lazuli.Cli.Tests;

public class LazuliManifestTests
{
    [Fact]
    public void A_missing_manifest_is_a_notice_not_a_failure()
    {
        var root = NewDir();

        var outcome = LazuliManifest.Validate(root);

        Assert.False(outcome.Present);
        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, m => m.Contains("Lazuli.toml"));
    }

    [Fact]
    public void A_well_formed_manifest_whose_paths_exist_is_valid()
    {
        var root = NewDir();
        Directory.CreateDirectory(Path.Combine(root, "src", "MyApp.Api"));
        File.WriteAllText(Path.Combine(root, "Lazuli.toml"), """
            [workspace]
            name = "MyApp"

            [products.app]
            backend = "src/MyApp.Api"
            """);

        var outcome = LazuliManifest.Validate(root);

        Assert.True(outcome.Present);
        Assert.True(outcome.Valid);
        Assert.Empty(outcome.Messages);
    }

    [Fact]
    public void A_declared_backend_path_that_does_not_exist_is_reported()
    {
        var root = NewDir();
        File.WriteAllText(Path.Combine(root, "Lazuli.toml"), """
            [workspace]
            name = "MyApp"

            [products.app]
            backend = "src/MyApp.Api"
            """);

        var outcome = LazuliManifest.Validate(root);

        Assert.True(outcome.Present);
        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, m => m.Contains("src/MyApp.Api") && m.Contains("does not exist"));
    }

    [Fact]
    public void A_manifest_without_a_workspace_section_is_reported()
    {
        var root = NewDir();
        File.WriteAllText(Path.Combine(root, "Lazuli.toml"), "[products.app]\nbackend = \"src\"\n");
        Directory.CreateDirectory(Path.Combine(root, "src"));

        var outcome = LazuliManifest.Validate(root);

        Assert.False(outcome.Valid);
        Assert.Contains(outcome.Messages, m => m.Contains("[workspace]"));
    }

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lazuli-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
