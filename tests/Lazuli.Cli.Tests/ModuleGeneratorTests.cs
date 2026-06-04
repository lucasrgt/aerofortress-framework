using Lazuli.Cli;

namespace Lazuli.Cli.Tests;

public class ModuleGeneratorTests
{
    [Fact]
    public void Generates_a_module_and_wires_it_into_program()
    {
        var root = NewProject("Shop.Api");

        var code = ModuleGenerator.Generate(root, "Orders");

        Assert.Equal(0, code);
        var module = File.ReadAllText(Path.Combine(root, "Modules", "Orders", "OrdersModule.cs"));
        Assert.Contains("namespace Shop.Api.Modules.Orders;", module);
        Assert.Contains("public static class OrdersModule", module);
        Assert.Contains("Map(IEndpointRouteBuilder app)", module);

        var program = File.ReadAllText(Path.Combine(root, "Program.cs"));
        Assert.Contains("using Shop.Api.Modules.Orders;", program);
        Assert.Contains("OrdersModule.Map(app);", program);
        Assert.True(program.IndexOf("OrdersModule.Map(app);", StringComparison.Ordinal)
                  < program.IndexOf("app.Run();", StringComparison.Ordinal));
    }

    [Fact]
    public void Refuses_to_overwrite_an_existing_module()
    {
        var root = NewProject("Shop.Api");

        Assert.Equal(0, ModuleGenerator.Generate(root, "Orders"));
        Assert.Equal(1, ModuleGenerator.Generate(root, "Orders"));
    }

    private static string NewProject(string name)
    {
        var root = Directory.CreateTempSubdirectory("lazuli-module-test").FullName;
        File.WriteAllText(Path.Combine(root, name + ".csproj"), "<Project />");
        File.WriteAllText(Path.Combine(root, "Program.cs"), "var app = WebApplication.Create();\n\napp.Run();\n");
        return root;
    }
}
