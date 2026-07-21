using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace AeroFortress.Framework.Doctor.Tests;

public class WriteConcurrencyAnalyzerTests
{
    [Fact]
    public Task Write_against_a_tokened_entity_reports_nothing() =>
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
            """, walletExtra: "public byte[] RowVersion { get; private set; }").RunAsync();

    [Fact]
    public Task Write_against_a_tokenless_entity_is_flagged()
    {
        var test = Make("Slice.cs", """
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
            """, walletExtra: "");
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(WriteConcurrencyAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
                .WithSpan("Slice.cs", 4, 14, 4, 21)
                .WithArguments("Deposit", "Wallet"));
        return test.RunAsync();
    }

    [Fact]
    public Task Read_only_slice_reports_nothing() =>
        Make("Slice.cs", """
            namespace Demo.Modules.Wallets;

            [Slice]
            static class GetBalance
            {
                static void Handle(Demo.AppDb db)
                {
                    var w = db.Wallets.Find();
                }
            }
            """, walletExtra: "").RunAsync();

    [Fact]
    public Task Every_write_is_this_rules_concern()
    {
        var test = Make("Slice.cs", """
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
            """, walletExtra: "");
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(WriteConcurrencyAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
                .WithSpan("Slice.cs", 4, 14, 4, 21)
                .WithArguments("Deposit", "Wallet"));
        return test.RunAsync();
    }

    private static CSharpAnalyzerTest<WriteConcurrencyAnalyzer, DefaultVerifier> Make(
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
                        """),
                    (sliceFile, sliceSource),
                },
            },
        };
}
