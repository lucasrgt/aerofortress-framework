using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace AeroFortress.Framework.Doctor.Tests;

public class CriticalityPolicyAnalyzerTests
{
    [Fact]
    public Task Opt_in_policy_does_not_force_a_decision() =>
        // The default: an undecided slice is fine — only [Critical] matters, exactly as before the policy.
        Make(UndecidedSlice, criticality: "opt-in").RunAsync();

    [Fact]
    public Task Absent_policy_behaves_like_opt_in() =>
        // No dial at all ⇒ opt-in: the rule is inert, so the undecided slice is not flagged.
        Make(UndecidedSlice, criticality: null).RunAsync();

    [Fact]
    public Task Explicit_policy_flags_an_undecided_slice()
    {
        var test = Make(UndecidedSlice, criticality: "explicit");
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(CriticalityPolicyAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
                .WithSpan("Deposit.cs", 4, 7, 4, 14)
                .WithArguments("Deposit"));
        return test.RunAsync();
    }

    [Fact]
    public Task Explicit_policy_accepts_a_critical_slice() =>
        Make(CriticalSlice, criticality: "explicit").RunAsync();

    [Fact]
    public Task Explicit_policy_accepts_a_non_critical_slice() =>
        Make(NonCriticalSlice, criticality: "explicit").RunAsync();

    [Fact]
    public Task Strict_policy_does_not_force_a_marker() =>
        // Under strict an undecided slice is simply treated as critical (AF0008/AF0010 realize that); it is
        // not an AF0029 error — strict needs no marker, it needs the journeys.
        Make(UndecidedSlice, criticality: "strict").RunAsync();

    [Fact]
    public Task A_non_slice_class_is_never_flagged() =>
        // The decision is required of slices only; a plain helper class is none of AF0029's business.
        Make(PlainClass, criticality: "explicit").RunAsync();

    private const string UndecidedSlice = """
        using System;

        [Slice]
        class Deposit { }

        sealed class SliceAttribute : Attribute { }
        """;

    private const string CriticalSlice = """
        using System;

        [Slice]
        [Critical]
        class Deposit { }

        sealed class SliceAttribute : Attribute { }
        sealed class CriticalAttribute : Attribute { }
        """;

    private const string NonCriticalSlice = """
        using System;

        [Slice]
        [NonCritical]
        class Deposit { }

        sealed class SliceAttribute : Attribute { }
        sealed class NonCriticalAttribute : Attribute { }
        """;

    private const string PlainClass = """
        class Helper { }
        """;

    private static CSharpAnalyzerTest<CriticalityPolicyAnalyzer, DefaultVerifier> Make(string source, string? criticality)
    {
        var test = new CSharpAnalyzerTest<CriticalityPolicyAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.Errors,
            TestState = { Sources = { ("Deposit.cs", source) } },
        };
        if (criticality is not null)
            test.TestState.AnalyzerConfigFiles.Add(
                ("/.globalconfig", $"is_global = true\nbuild_property.AeroFortressCriticality = {criticality}\n"));
        return test;
    }
}
