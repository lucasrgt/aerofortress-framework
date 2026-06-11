using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Lazuli.Doctor.Tests;

public class JourneyAssertionAnalyzerTests
{
    [Fact]
    public Task A_journey_that_asserts_reports_nothing() =>
        Make("""
            public class WalletJourney
            {
                [Journey(typeof(Deposit), JourneyPath.Happy)]
                [Fact]
                public void Deposits_land() { var r = Run(); Assert.True(r.IsSuccess); }
            }
            """).RunAsync();

    [Fact]
    public Task A_journey_delegating_to_a_verify_helper_reports_nothing() =>
        Make("""
            public class WalletJourney
            {
                [Journey(typeof(Deposit), JourneyPath.Sad)]
                [Fact]
                public void Overdraft_is_rejected() { VerifyRejected(Run()); }
            }
            """).RunAsync();

    [Fact]
    public Task A_journey_that_asserts_nothing_is_flagged()
    {
        var test = Make("""
            public class WalletJourney
            {
                [Journey(typeof(Deposit), JourneyPath.Sad)]
                [Fact]
                public void Overdraft_does_nothing() { var r = Run(); }
            }
            """);
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(JourneyAssertionAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
                .WithSpan("WalletJourney.Tests.cs", 3, 5, 3, 48)
                .WithArguments("Deposit"));
        return test.RunAsync();
    }

    [Fact]
    public Task Stacked_journeys_on_one_empty_body_report_once()
    {
        var test = Make("""
            public class WalletJourney
            {
                [Journey(typeof(Deposit), JourneyPath.Happy)]
                [Journey(typeof(Deposit), JourneyPath.Sad)]
                [Fact]
                public void Does_nothing() { var r = Run(); }
            }
            """);
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(JourneyAssertionAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
                .WithSpan("WalletJourney.Tests.cs", 3, 5, 3, 50)
                .WithArguments("Deposit"));
        return test.RunAsync();
    }

    // The analyzer reads journeys from AdditionalFiles (they're excluded from the app compilation), so the
    // source is a trivial stand-in and the journey text is the additional file.
    private static CSharpAnalyzerTest<JourneyAssertionAnalyzer, DefaultVerifier> Make(string journeys) =>
        new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.Errors,
            TestState =
            {
                Sources = { ("Program.cs", "class P { }") },
                AdditionalFiles = { ("WalletJourney.Tests.cs", journeys) },
            },
        };
}
