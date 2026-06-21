using System.Linq;

namespace AeroFortress.Framework.Cli;

/// <summary>
/// Generates a complete, production-shaped authentication module from the proven blueprint: the
/// Account module (register / login / refresh / logout / me), the password building block, and — unless
/// opted out — multi-tenant scoping and web-cookie refresh delivery. The auth *mechanism* (reading the
/// caller, minting/validating JWTs, the refresh-cookie security) is NOT emitted into the app: it is the
/// framework's <c>AeroFortress.Framework.Auth</c> package, wired in one call (<c>AddJwtAccessTokens</c>). The emitted code
/// is plain, idiomatic C# a stranger can maintain; the generator only stamps the app's name onto it and
/// resolves the two flags.
/// </summary>
/// <remarks>
/// Two opt-out flags compose freely:
/// <list type="bullet">
/// <item><description><c>--skip-tenancy</c> — no org scoping. <c>User</c> drops <c>ITenantScoped</c>,
/// the JWT's <c>org</c> claim is left empty (the single-tenant app ignores it), and the <c>Tenancy/</c>
/// folder (incl. <c>RequestTenant</c>) is not emitted.</description></item>
/// <item><description><c>--skip-cookies</c> — no web-cookie delivery. Routes return the result
/// directly and refresh/logout read the token from the body only; <c>AddRefreshCookie</c> is not wired
/// and the <c>RefreshCookie</c> service is unused.</description></item>
/// </list>
/// </remarks>
public static class AuthGenerator
{
    /// <summary>Generate the auth module into the application project at <paramref name="root"/>.</summary>
    /// <param name="root">The application project directory (the one holding <c>&lt;App&gt;.Api.csproj</c>).</param>
    /// <param name="skipTenancy">Emit the single-tenant variant (no org scoping).</param>
    /// <param name="skipCookies">Emit the body-only variant (no web refresh cookie).</param>
    public static int Generate(string root, bool skipTenancy, bool skipCookies)
    {
        var csproj = Directory.GetFiles(root, "*.csproj").FirstOrDefault();
        if (csproj is null)
        {
            Console.Error.WriteLine("lazuli: no .csproj here — run this from the application project directory.");
            return 1;
        }

        var appNamespace = Path.GetFileNameWithoutExtension(csproj);            // e.g. Acme.Api
        var appName = appNamespace.EndsWith(".Api", StringComparison.Ordinal)
            ? appNamespace[..^4]
            : appNamespace;                                                     // e.g. Acme
        var appLower = appName.ToLowerInvariant();

        if (File.Exists(Path.Combine(root, "Modules", "Account", "AccountModule.cs")))
        {
            Console.Error.WriteLine("lazuli: an Account module already exists here — remove it first.");
            return 1;
        }

        var tenancy = !skipTenancy;
        var cookies = !skipCookies;
        var testDir = TestProjectDir(root, appName);

        foreach (var logical in AuthTemplates.All())
        {
            if (SkippedByFlag(logical, tenancy, cookies))
                continue;

            var body = AuthTemplates.Render(AuthTemplates.Read(logical), appName, appLower, tenancy, cookies);
            var destination = Destination(logical, root, testDir, appName, appLower);

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, body);
            Console.WriteLine($"created {destination}");
        }

        WireProgram(root, appName, tenancy);
        WireApiProject(csproj, appLower);
        WireTestProject(testDir);
        WireGlobalUsings(root, appName);

        Console.WriteLine(Summary(tenancy, cookies));
        return 0;
    }

    // Whole-file skips are decided here by path, not by markers inside the file. The auth *mechanism*
    // (ICurrentUser, AccessTokens, RefreshCookie) is framework-owned (AeroFortress.Framework.Auth), so there is nothing
    // mechanism-side to skip here — only the app-side tenancy policy file and the cookie integration test.
    private static bool SkippedByFlag(string logical, bool tenancy, bool cookies)
    {
        var normalized = logical.Replace('\\', '/');
        if (!tenancy && normalized.StartsWith("Tenancy/", StringComparison.Ordinal))
            return true;   // the Tenancy/ folder (incl. RequestTenant) is the single-tenant opt-out
        if (!cookies && normalized.EndsWith("Modules/Account/CookieDelivery.Tests.cs.cstmpl", StringComparison.Ordinal))
            return true;
        return false;
    }

    // Test artifacts (TestApp + the *.Tests.cs co-located with slices stay in the API tree, but the
    // bare TestApp belongs in the test project) land under tests/<App>.Tests; everything else under
    // the API project. Co-located *.Tests.cs live next to their slice in the API tree by convention.
    private static string Destination(string logical, string root, string testDir, string appName, string appLower)
    {
        var normalized = logical.Replace('\\', '/');
        var rendered = AuthTemplates.RenderPath(normalized, appName, appLower);

        if (normalized.StartsWith("Tests/", StringComparison.Ordinal))
            return Path.Combine(testDir, rendered["Tests/".Length..].Replace('/', Path.DirectorySeparatorChar));

        return Path.Combine(root, rendered.Replace('/', Path.DirectorySeparatorChar));
    }

    // tests/<App>.Tests by the Lazurite convention, regardless of where the API project sits relative
    // to it (we walk up to the solution root and look for tests/).
    private static string TestProjectDir(string apiRoot, string appName)
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(apiRoot, "..", ".."));
        return Path.Combine(solutionRoot, "tests", $"{appName}.Tests");
    }

    // Two clean lines in Program.cs: builder.AddAccount(); before Build(), and AccountModule.Map(app)
    // before app.Run(). No middleware wiring leaks here: WebApplication auto-adds UseAuthentication /
    // UseAuthorization once AddAccount has registered the auth + authorization services, so the composition
    // root stays the thin index LZ0017 requires.
    private static void WireProgram(string root, string appName, bool tenancy)
    {
        var program = Path.Combine(root, "Program.cs");
        if (!File.Exists(program))
        {
            Console.WriteLine("note: no Program.cs — register auth with builder.AddAccount(); and AccountModule.Map(app);");
            return;
        }

        var text = File.ReadAllText(program);
        if (text.Contains("AccountModule.Map"))
            return;

        var nl = text.Contains("\r\n") ? "\r\n" : "\n";
        var usingLine = $"using {appName}.Api.Modules.Account;{nl}";
        if (!text.Contains(usingLine))
            text = usingLine + text;

        var unusual = !text.Contains("var builder") || !text.Contains("var app =") || !text.Contains("app.Run();");
        if (unusual)
        {
            File.WriteAllText(program, text);
            Console.WriteLine("note: Program.cs looks unusual — add builder.AddAccount(); before Build(), then "
                + "AccountModule.Map(app); before app.Run(); (WebApplication auto-adds the auth middleware).");
            return;
        }

        // builder.AddAccount(); just before the app is built.
        text = ReplaceFirst(text, "var app =", $"builder.AddAccount();{nl}{nl}var app =");

        // Just the module routes before app.Run(). The auth middleware is NOT wired here: WebApplication
        // auto-adds UseAuthentication/UseAuthorization when AddAccount has registered the auth + authorization
        // services, so the composition root stays a thin index (LZ0017) with no Use* drift.
        var wiring = $"AccountModule.Map(app);{nl}{nl}app.Run();";
        text = ReplaceFirst(text, "app.Run();", wiring);

        File.WriteAllText(program, text);
        Console.WriteLine("wired auth into Program.cs (builder.AddAccount(); + AccountModule.Map(app);)");
    }

    private static void WireApiProject(string csproj, string appLower)
    {
        var text = File.ReadAllText(csproj);
        if (text.Contains("AeroFortress.Framework.Auth"))
            return;

        var nl = text.Contains("\r\n") ? "\r\n" : "\n";
        // AeroFortress.Framework.Auth carries the JWT mechanism (and JwtBearer transitively) — the app names no JWT
        // package itself. EF + argon2 are the data + crypto the Account module needs.
        var packages = string.Join(nl, new[]
        {
            "    <PackageReference Include=\"Microsoft.EntityFrameworkCore\" Version=\"10.0.8\" />",
            "    <PackageReference Include=\"Microsoft.EntityFrameworkCore.InMemory\" Version=\"10.0.8\" />",
            "    <PackageReference Include=\"Konscious.Security.Cryptography.Argon2\" Version=\"1.3.1\" />",
            "    <PackageReference Include=\"AeroFortress.Framework.Auth\" Version=\"0.1.0\" />",
        });

        // Drop the packages into the first <ItemGroup> (where the framework references already live).
        text = InsertBeforeClosingItemGroup(text, packages, nl);
        File.WriteAllText(csproj, text);
        Console.WriteLine($"added auth package references to {Path.GetFileName(csproj)}");
    }

    private static void WireTestProject(string testDir)
    {
        var csproj = Directory.Exists(testDir)
            ? Directory.GetFiles(testDir, "*.csproj").FirstOrDefault()
            : null;
        if (csproj is null)
        {
            Console.WriteLine($"note: no test project at {testDir} — add a <App>.Tests project with the "
                + "AeroFortress.Framework.Testing.InMemory package so the integration tests can boot the app.");
            return;
        }

        var text = File.ReadAllText(csproj);
        if (text.Contains("AeroFortress.Framework.Testing.InMemory"))
            return;

        var nl = text.Contains("\r\n") ? "\r\n" : "\n";
        var reference = "    <PackageReference Include=\"AeroFortress.Framework.Testing.InMemory\" Version=\"0.1.0\" />";
        // Sit it next to the existing AeroFortress.Framework.Testing reference when present, else in the first group.
        if (text.Contains("AeroFortress.Framework.Testing\""))
        {
            text = ReplaceFirst(text,
                "<PackageReference Include=\"AeroFortress.Framework.Testing\" Version=\"0.1.0\" />",
                "<PackageReference Include=\"AeroFortress.Framework.Testing\" Version=\"0.1.0\" />" + nl + reference);
        }
        else
        {
            text = InsertBeforeClosingItemGroup(text, reference, nl);
        }

        File.WriteAllText(csproj, text);
        Console.WriteLine($"added AeroFortress.Framework.Testing.InMemory to {Path.GetFileName(csproj)}");
    }

    private static void WireGlobalUsings(string root, string appName)
    {
        var path = Path.Combine(root, "GlobalUsings.cs");
        var line = $"global using {appName}.Api.BuildingBlocks;";
        if (!File.Exists(path))
        {
            File.WriteAllText(path, line + "\n");
            Console.WriteLine($"created {path}");
            return;
        }

        var text = File.ReadAllText(path);
        if (text.Contains(line))
            return;

        var nl = text.Contains("\r\n") ? "\r\n" : "\n";
        File.WriteAllText(path, text.TrimEnd() + nl + line + nl);
        Console.WriteLine("added BuildingBlocks global using");
    }

    private static string ReplaceFirst(string text, string find, string replacement)
    {
        var at = text.IndexOf(find, StringComparison.Ordinal);
        return at < 0 ? text : text[..at] + replacement + text[(at + find.Length)..];
    }

    // Insert lines just before the first "</ItemGroup>", on their own lines above it — preserving that
    // closing tag's own indentation (so the inserted refs keep their 4-space indent, not 6).
    private static string InsertBeforeClosingItemGroup(string text, string lines, string nl)
    {
        var at = text.IndexOf("</ItemGroup>", StringComparison.Ordinal);
        if (at < 0)
            return text;
        var lineStart = text.LastIndexOf('\n', at) + 1;   // start of the </ItemGroup> line
        return text[..lineStart] + lines + nl + text[lineStart..];
    }

    private static string Summary(bool tenancy, bool cookies) =>
        "auth generated — "
        + (tenancy ? "multi-tenant" : "single-tenant")
        + ", "
        + (cookies ? "web-cookie + body delivery" : "body-only delivery")
        + ". Run `af doctor` then `af test`.";
}
