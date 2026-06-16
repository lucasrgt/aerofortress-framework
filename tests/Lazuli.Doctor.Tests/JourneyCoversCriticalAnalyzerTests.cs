using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Lazuli.Doctor.Tests;

public class JourneyCoversCriticalAnalyzerTests
{
    [Fact]
    public Task A_journey_covering_a_critical_slice_is_fine() =>
        Make(CriticalSlice, """
            [Journey(typeof(Deposit), JourneyPath.Happy)]
            [Journey(typeof(Deposit), JourneyPath.Sad)]
            """).RunAsync();

    [Fact]
    public Task A_journey_covering_a_non_critical_slice_is_flagged()
    {
        var test = Make(PlainSlice, "[Journey(typeof(Deposit), JourneyPath.Happy)]");
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(JourneyCoversCriticalAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
                .WithSpan("WalletFlow.Tests.cs", 1, 17, 1, 24)   // the `Deposit` in typeof(Deposit)
                .WithArguments("Deposit"));
        return test.RunAsync();
    }

    [Fact]
    public Task A_plain_e2e_flow_without_a_journey_is_fine() =>
        // No [Journey] at all — a voluntary end-to-end flow needs no critical slice.
        Make(PlainSlice, "// just an [E2E] flow, no [Journey] attribute").RunAsync();

    [Fact]
    public Task Strict_policy_accepts_a_journey_on_an_undecided_slice() =>
        // Under strict the undecided slice is treated as critical, so a journey on it is no longer inert.
        Make(PlainSlice, "[Journey(typeof(Deposit), JourneyPath.Happy)]", criticality: "strict").RunAsync();

    [Fact]
    public Task Strict_policy_still_flags_a_journey_on_a_non_critical_slice()
    {
        // [NonCritical] keeps the slice out of the critical set even under strict, so the journey is inert.
        var test = Make(NonCriticalSlice, "[Journey(typeof(Deposit), JourneyPath.Happy)]", criticality: "strict");
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(JourneyCoversCriticalAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
                .WithSpan("WalletFlow.Tests.cs", 1, 17, 1, 24)   // the `Deposit` in typeof(Deposit)
                .WithArguments("Deposit"));
        return test.RunAsync();
    }

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

    private static CSharpAnalyzerTest<JourneyCoversCriticalAnalyzer, DefaultVerifier> Make(
        string source, string journeys, string? criticality = null)
    {
        var test = new CSharpAnalyzerTest<JourneyCoversCriticalAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.Errors,
            TestState =
            {
                Sources = { ("Deposit.cs", source) },
                AdditionalFiles = { ("WalletFlow.Tests.cs", journeys) },
            },
        };
        if (criticality is not null)
            test.TestState.AnalyzerConfigFiles.Add(
                ("/.globalconfig", $"is_global = true\nbuild_property.LazuliCriticality = {criticality}\n"));
        return test;
    }
}
