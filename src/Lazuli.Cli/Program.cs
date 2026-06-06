using Lazuli.Cli;

return args switch
{
    ["new", var name] => Tooling.Dotnet("new", ["lazuli", "-n", name]),
    ["g", "module", var name] => ModuleGenerator.Generate(Directory.GetCurrentDirectory(), name),
    ["g", "slice", var module, var name] => SliceGenerator.Generate(Directory.GetCurrentDirectory(), module, name),
    ["g", "slice", var module, var name, "--critical"] => SliceGenerator.Generate(Directory.GetCurrentDirectory(), module, name, critical: true),
    ["g", "entity", var module, var name] => EntityGenerator.Generate(Directory.GetCurrentDirectory(), module, name),
    ["g", "vo", var name] => ValueObjectGenerator.Generate(Directory.GetCurrentDirectory(), name),
    ["g", "crud", var module, var entity] => CrudGenerator.Generate(Directory.GetCurrentDirectory(), module, entity),
    ["g", "hub", var module, var name] => HubGenerator.Generate(Directory.GetCurrentDirectory(), module, name),
    ["g", "auth", .. var flags] => AuthGenerator.Generate(Directory.GetCurrentDirectory(), skipTenancy: flags.Contains("--skip-tenancy"), skipCookies: flags.Contains("--skip-cookies")),
    ["g", "auth:otp"] => AuthFlowGenerator.Generate(Directory.GetCurrentDirectory(), AuthFlow.Otp),
    ["g", "auth:oauth"] => AuthFlowGenerator.Generate(Directory.GetCurrentDirectory(), AuthFlow.OAuth),
    ["g", "auth:email"] => AuthFlowGenerator.Generate(Directory.GetCurrentDirectory(), AuthFlow.Email),
    ["doctor", .. var rest] => Doctor(rest),
    ["mutate", .. var rest] => Tooling.Dotnet("stryker", rest),
    ["test", .. var rest] => Tooling.Dotnet("test", TestArgs(rest)),
    _ => Usage(),
};

// The doctor is bidirectional — one verdict over both halves of the stack. The backend leg is the build-time
// Roslyn analyzers (a clean build = a clean bill of health; an LZ#### error fails it). The frontend leg, for
// each Lazuli client (a dir under clients/ carrying eslint.config.js), is the TS-world harness: eslint
// (eslint-plugin-lazuli, the LZFE* rules) + tsc (the "wired" gate against the generated client). One command,
// both sides — so nothing is left loose in either direction.
static int Doctor(string[] rest)
{
    Console.WriteLine("lazuli doctor — backend conventions (build)...");
    var code = Tooling.Dotnet("build", rest);

    foreach (var client in FrontendClients(Directory.GetCurrentDirectory()))
    {
        Console.WriteLine($"lazuli doctor — frontend conventions ({Path.GetFileName(client)}: eslint + tsc)...");
        code = Math.Max(code, Tooling.Run("npm", ["run", "lint"], client));
        code = Math.Max(code, Tooling.Run("npm", ["run", "typecheck"], client));
    }

    Console.WriteLine(code == 0 ? "doctor: conventions pass." : "doctor: violations reported above.");
    return code;
}

// A Lazuli frontend lives under clients/<app>/ with an eslint.config.js (wiring eslint-plugin-lazuli). The
// doctor discovers them by convention rather than configuration — no frontend, no frontend leg.
static IEnumerable<string> FrontendClients(string root)
{
    var clients = Path.Combine(root, "clients");
    if (!Directory.Exists(clients))
        return [];
    return Directory.EnumerateDirectories(clients)
        .Where(dir => File.Exists(Path.Combine(dir, "eslint.config.js"))
                   && File.Exists(Path.Combine(dir, "package.json")));
}

// The fast leg: dotnet test, with a category shorthand. --unit/--integration/--e2e map to the
// xUnit Category trait so a single layer can be run; anything else is passed straight through.
static string[] TestArgs(string[] rest) => rest switch
{
    ["--unit", .. var more] => ["--filter", "Category=Unit", .. more],
    ["--integration", .. var more] => ["--filter", "Category=Integration", .. more],
    ["--e2e", .. var more] => ["--filter", "Category=E2E", .. more],
    _ => rest,
};

static int Usage()
{
    Console.Error.WriteLine(
        """
        lazuli — the Lazuli convention CLI

        usage:
          lazuli new <Name>                 scaffold a new Lazuli project (dotnet new lazuli)
          lazuli g module <Name>            generate a module + wire it into Program.cs
          lazuli g slice <Module> <Name> [--critical]   generate a slice + tests (+ journeys if critical)
          lazuli g entity <Module> <Name>   generate a rich [Entity] — encapsulated, with an EnsureValid invariant funnel
          lazuli g vo <Name>                generate an always-valid [ValueObject] in BuildingBlocks
          lazuli g crud <Module> <Entity>   generate CRUD slices (list/lookup/create/update/delete +me) for a data-bag entity
          lazuli g hub <Module> <Name>      generate a SignalR hub (real-time: typing/presence/live fan-out)
          lazuli g auth [--skip-tenancy] [--skip-cookies]   generate the auth module (register/login/refresh/logout/me)
          lazuli g auth:otp                 augment auth with phone verification by SMS code
          lazuli g auth:oauth               augment auth with Google sign-up/sign-in
          lazuli g auth:email               augment auth with email verification + password reset
          lazuli doctor                     run the convention analyzers (build)
          lazuli test [--unit|--integration|--e2e]   run the .NET tests (fast leg)
          lazuli mutate                     run mutation testing via Stryker (deep leg)

        Run from the relevant project directory.
        """);
    return 1;
}
