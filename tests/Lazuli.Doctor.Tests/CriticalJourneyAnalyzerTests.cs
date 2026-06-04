using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Lazuli.Doctor.Tests;

public class CriticalJourneyAnalyzerTests
{
    [Fact]
    public Task Critical_slice_with_both_journeys_reports_nothing() =>
        Make(CriticalSlice, """
            [Journey(typeof(Deposit), JourneyPath.Happy)]
            [Journey(typeof(Deposit), JourneyPath.Sad)]
            """).RunAsync();

    [Fact]
    public Task Critical_slice_missing_the_sad_journey_is_flagged()
    {
        var test = Make(CriticalSlice, "[Journey(typeof(Deposit), JourneyPath.Happy)]");
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(CriticalJourneyAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
                .WithSpan("Deposit.cs", 5, 7, 5, 14)
                .WithArguments("Deposit", "Sad"));
        return test.RunAsync();
    }

    [Fact]
    public Task Critical_slice_missing_the_happy_journey_is_flagged()
    {
        var test = Make(CriticalSlice, "[Journey(typeof(Deposit), JourneyPath.Sad)]");
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(CriticalJourneyAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
                .WithSpan("Deposit.cs", 5, 7, 5, 14)
                .WithArguments("Deposit", "Happy"));
        return test.RunAsync();
    }

    [Fact]
    public Task Non_critical_slice_needs_no_journey() =>
        Make(PlainSlice, "").RunAsync();

    private const string CriticalSlice = """
        using System;

        [Slice]
        [Critical]
        class Deposit { }

        sealed class SliceAttribute : Attribute { }
        sealed class CriticalAttribute : Attribute { }
        """;

    private const string PlainSlice = """
        using System;

        [Slice]
        class Deposit { }

        sealed class SliceAttribute : Attribute { }
        """;

    private static CSharpAnalyzerTest<CriticalJourneyAnalyzer, DefaultVerifier> Make(string source, string journeys) =>
        new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.Errors,
            TestState =
            {
                Sources = { ("Deposit.cs", source) },
                AdditionalFiles = { ("WalletJourney.Tests.cs", journeys) },
            },
        };
}
