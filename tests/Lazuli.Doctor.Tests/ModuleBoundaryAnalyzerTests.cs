using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Lazuli.Doctor.Tests;

public class ModuleBoundaryAnalyzerTests
{
    [Fact]
    public Task Writing_your_own_modules_entity_is_fine() =>
        Make("Own.cs", """
            namespace Demo.Modules.Account;

            class RegisterSlice
            {
                void Do(Demo.AppDb db) => db.Users.Add(null!);
            }
            """).RunAsync();

    [Fact]
    public Task Reading_another_modules_entity_is_fine() =>
        // A cross-module read (not a write method) — allowed in the modular monolith.
        Make("Read.cs", """
            namespace Demo.Modules.Host;

            class HomeSlice
            {
                void Do(Demo.AppDb db) { var _ = db.Users.Find(); }
            }
            """).RunAsync();

    [Fact]
    public Task Writing_another_modules_entity_is_flagged()
    {
        var test = Make("Bad.cs", """
            namespace Demo.Modules.Host;

            class BadSlice
            {
                void Do(Demo.AppDb db) => db.Users.Add(null!);
            }
            """);
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(ModuleBoundaryAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
                .WithSpan("Bad.cs", 5, 40, 5, 43)
                .WithArguments("Host", "Account", "User"));
        return test.RunAsync();
    }

    [Fact]
    public Task A_test_file_may_write_any_module() =>
        // Integration/unit tests legitimately seed another module's data; the rule guards production.
        Make("Seed.Tests.cs", """
            namespace Demo.Tests.Modules.Host;

            class Seeder
            {
                void Do(Demo.AppDb db) => db.Users.Add(null!);
            }
            """).RunAsync();

    // Stub DbSet + two modules' entities + the shared AppDb (no EF package needed — the analyzer keys on the
    // DbSet type name + namespace).
    private const string Harness = """
        namespace Microsoft.EntityFrameworkCore { public class DbSet<T> { public void Add(T e) { } public T Find() => default!; } }
        namespace Demo.Modules.Account { public class User { } }
        namespace Demo.Modules.Host { public class Profile { } }
        namespace Demo
        {
            public class AppDb
            {
                public Microsoft.EntityFrameworkCore.DbSet<Demo.Modules.Account.User> Users => new();
                public Microsoft.EntityFrameworkCore.DbSet<Demo.Modules.Host.Profile> Profiles => new();
            }
        }
        """;

    private static CSharpAnalyzerTest<ModuleBoundaryAnalyzer, DefaultVerifier> Make(string sliceFile, string sliceSource) =>
        new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestState =
            {
                Sources = { ("Harness.cs", Harness), (sliceFile, sliceSource) },
            },
        };
}
