using Lazuli.Cli;

return args switch
{
    ["new", var name] => Tooling.Dotnet("new", ["lazuli", "-n", name]),
    ["g", "module", var name] => ModuleGenerator.Generate(Directory.GetCurrentDirectory(), name),
    ["g", "slice", var module, var name] => SliceGenerator.Generate(Directory.GetCurrentDirectory(), module, name),
    ["g", "slice", var module, var name, "--critical"] => SliceGenerator.Generate(Directory.GetCurrentDirectory(), module, name, critical: true),
    ["g", "crud", var module, var entity] => CrudGenerator.Generate(Directory.GetCurrentDirectory(), module, entity),
    ["g", "auth", .. var flags] => AuthGenerator.Generate(Directory.GetCurrentDirectory(), skipTenancy: flags.Contains("--skip-tenancy"), skipCookies: flags.Contains("--skip-cookies")),
    ["g", "auth:otp"] => AuthFlowGenerator.Generate(Directory.GetCurrentDirectory(), AuthFlow.Otp),
    ["g", "auth:oauth"] => AuthFlowGenerator.Generate(Directory.GetCurrentDirectory(), AuthFlow.OAuth),
    ["g", "auth:email"] => AuthFlowGenerator.Generate(Directory.GetCurrentDirectory(), AuthFlow.Email),
    ["doctor", .. var rest] => Doctor(rest),
    ["mutate", .. var rest] => Tooling.Dotnet("stryker", rest),
    ["test", .. var rest] => Tooling.Dotnet("test", TestArgs(rest)),
    _ => Usage(),
};

// The doctor is the build-time analyzers: a clean build is a clean bill of health; an LZ#### error
// fails it. The branded entry point runs the build and frames the verdict.
static int Doctor(string[] rest)
{
    Console.WriteLine("lazuli doctor — running the convention analyzers (build)...");
    var code = Tooling.Dotnet("build", rest);
    Console.WriteLine(code == 0 ? "doctor: conventions pass." : "doctor: violations reported above.");
    return code;
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
          lazuli g crud <Module> <Entity>   generate CRUD slices (list/lookup/create/update/delete +me) for an entity
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
