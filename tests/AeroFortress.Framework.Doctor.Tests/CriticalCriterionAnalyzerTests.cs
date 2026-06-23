using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace AeroFortress.Framework.Doctor.Tests;

public class CriticalCriterionAnalyzerTests
{
    [Fact]
    public Task Critical_slice_declared_with_a_criterion_reports_nothing() =>
        Make(CriticalSlice, Manifest).RunAsync();

    [Fact]
    public Task Critical_slice_with_no_manifest_is_flagged()
    {
        var test = Make(CriticalSlice, manifest: null);
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(CriticalCriterionAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
                .WithSpan("Deposit.cs", 7, 7, 7, 14)
                .WithArguments("Deposit", "Wallets.spec.toml"));
        return test.RunAsync();
    }

    [Fact]
    public Task Critical_slice_declared_with_no_criteria_is_flagged()
    {
        // The manifest declares the slice's table but lists no criteria — still a gap on the criterion axis.
        var test = Make(CriticalSlice, EmptyManifest);
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(CriticalCriterionAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
                .WithSpan("Deposit.cs", 7, 7, 7, 14)
                .WithArguments("Deposit", "Wallets.spec.toml"));
        return test.RunAsync();
    }

    [Fact]
    public Task Non_critical_slice_needs_no_criterion() =>
        Make(PlainSlice, manifest: null).RunAsync();

    [Fact]
    public Task Strict_policy_requires_a_criterion_on_an_undecided_slice()
    {
        // No [Critical] marker, but the strict policy makes an undecided slice critical — so a slice with no
        // declared criterion is flagged exactly as if it carried [Critical].
        var test = Make(PlainSlice, manifest: null, criticality: "strict");
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(CriticalCriterionAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
                .WithSpan("Deposit.cs", 6, 7, 6, 14)
                .WithArguments("Deposit", "Wallets.spec.toml"));
        return test.RunAsync();
    }

    [Fact]
    public Task Strict_policy_lets_a_non_critical_slice_skip_the_criterion() =>
        // [NonCritical] is the explicit opt-out: even under strict it needs no criterion.
        Make(NonCriticalSlice, manifest: null, criticality: "strict").RunAsync();

    // A critical [Slice] in a module — the obligation to declare a criterion lives in the module's manifest.
    private const string CriticalSlice = """
        using System;

        namespace App.Modules.Wallets;

        [Slice]
        [Critical]
        class Deposit { }

        sealed class SliceAttribute : Attribute { }
        sealed class CriticalAttribute : Attribute { }
        """;

    private const string PlainSlice = """
        using System;

        namespace App.Modules.Wallets;

        [Slice]
        class Deposit { }

        sealed class SliceAttribute : Attribute { }
        """;

    private const string NonCriticalSlice = """
        using System;

        namespace App.Modules.Wallets;

        [Slice]
        [NonCritical]
        class Deposit { }

        sealed class SliceAttribute : Attribute { }
        sealed class NonCriticalAttribute : Attribute { }
        """;

    // Declares the slice with a criterion — closes the AF0031 obligation.
    private const string Manifest = """
        module = "Wallets"

        [slices.Deposit]
        criteria = ["own-resource-only"]
        """;

    // Declares the slice's table but with an empty criteria array — does not satisfy AF0031.
    private const string EmptyManifest = """
        module = "Wallets"

        [slices.Deposit]
        criteria = []
        """;

    // Builds the analyzer test: the slice source, an optional module manifest (AdditionalFile), and an
    // optional criticality dial projected as the analyzer's global MSBuild property.
    private static CSharpAnalyzerTest<CriticalCriterionAnalyzer, DefaultVerifier> Make(
        string source, string? manifest, string? criticality = null)
    {
        var test = new CSharpAnalyzerTest<CriticalCriterionAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.Errors,
            TestState = { Sources = { ("Deposit.cs", source) } },
        };
        if (manifest is not null)
            test.TestState.AdditionalFiles.Add(("Wallets.spec.toml", manifest));
        if (criticality is not null)
            test.TestState.AnalyzerConfigFiles.Add(
                ("/.globalconfig", $"is_global = true\nbuild_property.AeroFortressCriticality = {criticality}\n"));
        return test;
    }
}
