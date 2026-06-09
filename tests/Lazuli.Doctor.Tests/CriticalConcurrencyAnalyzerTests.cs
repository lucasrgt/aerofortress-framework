using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Lazuli.Doctor.Tests;

public class CriticalConcurrencyAnalyzerTests
{
    [Fact]
    public Task Critical_write_against_a_tokened_entity_reports_nothing() =>
        Make("Slice.cs", """
            namespace Demo.Modules.Wallets;

            [Slice]
            [Critical]
            static class Deposit
            {
                static void Handle(Demo.AppDb db)
                {
                    var w = db.Wallets.Find();
                    db.SaveChanges();
                }
            }
            """, walletExtra: "public byte[] RowVersion { get; private set; }").RunAsync();

    [Fact]
    public Task Critical_write_against_a_tokenless_entity_is_flagged()
    {
        var test = Make("Slice.cs", """
            namespace Demo.Modules.Wallets;

            [Slice]
            [Critical]
            static class Deposit
            {
                static void Handle(Demo.AppDb db)
                {
                    var w = db.Wallets.Find();
                    db.SaveChanges();
                }
            }
            """, walletExtra: "");
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(CriticalConcurrencyAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
                .WithSpan("Slice.cs", 5, 14, 5, 21)
                .WithArguments("Deposit", "Wallet"));
        return test.RunAsync();
    }

    [Fact]
    public Task Read_only_critical_slice_reports_nothing() =>
        Make("Slice.cs", """
            namespace Demo.Modules.Wallets;

            [Slice]
            [Critical]
            static class GetBalance
            {
                static void Handle(Demo.AppDb db)
                {
                    var w = db.Wallets.Find();
                }
            }
            """, walletExtra: "").RunAsync();

    [Fact]
    public Task Non_critical_write_is_not_this_rules_concern() =>
        Make("Slice.cs", """
            namespace Demo.Modules.Wallets;

            [Slice]
            static class Deposit
            {
                static void Handle(Demo.AppDb db)
                {
                    var w = db.Wallets.Find();
                    db.SaveChanges();
                }
            }
            """, walletExtra: "").RunAsync();

    private static CSharpAnalyzerTest<CriticalConcurrencyAnalyzer, DefaultVerifier> Make(
        string sliceFile, string sliceSource, string walletExtra) =>
        new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestState =
            {
                Sources =
                {
                    ("Harness.cs", $$"""
                        namespace Microsoft.EntityFrameworkCore
                        {
                            public class DbSet<T> { public T Find() => default!; }
                        }
                        namespace Demo.Modules.Wallets { public class Wallet { {{walletExtra}} } }
                        namespace Demo
                        {
                            public class AppDb
                            {
                                public Microsoft.EntityFrameworkCore.DbSet<Demo.Modules.Wallets.Wallet> Wallets => new();
                                public void SaveChanges() { }
                            }
                        }
                        sealed class SliceAttribute : System.Attribute { }
                        sealed class CriticalAttribute : System.Attribute { }
                        """),
                    (sliceFile, sliceSource),
                },
            },
        };
}
