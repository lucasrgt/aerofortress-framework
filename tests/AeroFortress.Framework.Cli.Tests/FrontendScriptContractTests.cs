using AeroFortress.Framework.Cli;

namespace AeroFortress.Framework.Cli.Tests;

public class FrontendScriptContractTests
{
    [Fact]
    public void A_real_runner_command_is_executable_evidence()
    {
        var root = Package("""{ "scripts": { "test": "vitest run" } }""");
        try
        {
            Assert.Null(FrontendScriptContract.Validate(root, "test"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("echo no tests configured")]
    [InlineData("printf 'ok'")]
    [InlineData("true")]
    [InlineData("exit 0")]
    public void A_placeholder_is_not_executable_evidence(string command)
    {
        var root = Package($$"""{ "scripts": { "test": "{{command}}" } }""");
        try
        {
            var error = FrontendScriptContract.Validate(root, "test");

            Assert.NotNull(error);
            Assert.Contains("placeholder", error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_missing_required_script_is_not_executable_evidence()
    {
        var root = Package("""{ "scripts": {} }""");
        try
        {
            var error = FrontendScriptContract.Validate(root, "test:e2e");

            Assert.NotNull(error);
            Assert.Contains("test:e2e", error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Package(string json)
    {
        var root = Path.Combine(Path.GetTempPath(), "af-frontend-script-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "package.json"), json);
        return root;
    }
}
