using System.Linq;

namespace Lazuli.Cli;

/// <summary>
/// Generates a module — a bounded context that groups slices under one route prefix — and wires its
/// <c>Map</c> into <c>Program.cs</c> (best-effort, before <c>app.Run()</c>). The module starts empty;
/// add slices with <c>lazuli g slice &lt;Module&gt; &lt;Name&gt;</c>.
/// </summary>
public static class ModuleGenerator
{
    /// <summary>Generate <paramref name="name"/> module under the <paramref name="root"/> project.</summary>
    public static int Generate(string root, string name)
    {
        var csproj = Directory.GetFiles(root, "*.csproj").FirstOrDefault();
        if (csproj is null)
        {
            Console.Error.WriteLine("lazuli: no .csproj here — run this from the application project directory.");
            return 1;
        }

        var appNamespace = Path.GetFileNameWithoutExtension(csproj);
        var modulePath = Path.Combine(root, "Modules", name, name + "Module.cs");
        if (File.Exists(modulePath))
        {
            Console.Error.WriteLine($"lazuli: {modulePath} already exists.");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(modulePath)!);
        File.WriteAllText(modulePath, Module(appNamespace, name));
        Console.WriteLine($"created {modulePath}");

        WireIntoProgram(root, appNamespace, name);
        return 0;
    }

    // The convention is one explicit registration line, no discovery. The generator writes that line
    // for you, before app.Run(), and adds the using — or tells you to, if Program.cs looks unusual.
    private static void WireIntoProgram(string root, string appNamespace, string name)
    {
        var program = Path.Combine(root, "Program.cs");
        if (!File.Exists(program))
            return;

        var text = File.ReadAllText(program);
        if (text.Contains($"{name}Module.Map"))
            return;

        if (text.Contains("app.Run();"))
        {
            text = $"using {appNamespace}.Modules.{name};{Environment.NewLine}" + text;
            text = text.Replace("app.Run();", $"{name}Module.Map(app);{Environment.NewLine}{Environment.NewLine}app.Run();");
            File.WriteAllText(program, text);
            Console.WriteLine($"wired {name}Module into Program.cs");
        }
        else
        {
            Console.WriteLine($"note: register the module — add {name}Module.Map(app); to Program.cs");
        }
    }

    private static string Module(string appNamespace, string name) => $$"""
        namespace {{appNamespace}}.Modules.{{name}};

        /// <summary>The {{name}} module groups its slices under /{{name.ToLowerInvariant()}}.</summary>
        public static class {{name}}Module
        {
            public static void Map(IEndpointRouteBuilder app)
            {
                // Register this module's slices here as you generate them:
                //   var {{name.ToLowerInvariant()}} = app.MapGroup("/{{name.ToLowerInvariant()}}");
                //   <Slice>.Map({{name.ToLowerInvariant()}});
            }
        }

        """;
}
