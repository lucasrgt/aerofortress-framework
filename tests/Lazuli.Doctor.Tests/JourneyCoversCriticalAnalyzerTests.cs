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

    private static CSharpAnalyzerTest<JourneyCoversCriticalAnalyzer, DefaultVerifier> Make(string source, string journeys) =>
        new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.Errors,
            TestState =
            {
                Sources = { ("Deposit.cs", source) },
                AdditionalFiles = { ("WalletFlow.Tests.cs", journeys) },
            },
        };
}
