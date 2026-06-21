using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace AeroFortress.Framework.Doctor.Tests;

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

    [Fact]
    public Task Strict_policy_treats_an_undecided_slice_as_critical()
    {
        // No [Critical] marker, but the strict policy makes an undecided slice critical — so the missing
        // sad journey is flagged exactly as if it carried [Critical].
        var test = Make(PlainSlice, "[Journey(typeof(Deposit), JourneyPath.Happy)]", criticality: "strict");
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(CriticalJourneyAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
                .WithSpan("Deposit.cs", 4, 7, 4, 14)
                .WithArguments("Deposit", "Sad"));
        return test.RunAsync();
    }

    [Fact]
    public Task Strict_policy_lets_a_non_critical_slice_skip_journeys() =>
        // [NonCritical] is the explicit opt-out: even under strict it needs no journeys.
        Make(NonCriticalSlice, "", criticality: "strict").RunAsync();

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

    private const string NonCriticalSlice = """
        using System;

        [Slice]
        [NonCritical]
        class Deposit { }

        sealed class SliceAttribute : Attribute { }
        sealed class NonCriticalAttribute : Attribute { }
        """;

    private static CSharpAnalyzerTest<CriticalJourneyAnalyzer, DefaultVerifier> Make(
        string source, string journeys, string? criticality = null)
    {
        var test = new CSharpAnalyzerTest<CriticalJourneyAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.Errors,
            TestState =
            {
                Sources = { ("Deposit.cs", source) },
                AdditionalFiles = { ("WalletJourney.Tests.cs", journeys) },
            },
        };
        if (criticality is not null)
            test.TestState.AnalyzerConfigFiles.Add(
                ("/.globalconfig", $"is_global = true\nbuild_property.AeroFortressCriticality = {criticality}\n"));
        return test;
    }
}
