using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace AeroFortress.Framework.Doctor.Tests;

public class CriticalCriterionAnalyzerTests
{
    [Fact]
    public Task Critical_slice_with_a_class_level_verify_reports_nothing() =>
        Make(CriticalWithVerify).RunAsync();

    [Fact]
    public Task Critical_slice_with_a_verify_on_a_method_reports_nothing() =>
        Make(CriticalVerifyOnMethod).RunAsync();

    [Fact]
    public Task Critical_slice_without_any_verify_is_flagged()
    {
        var test = Make(CriticalNoVerify);
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(CriticalCriterionAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
                .WithSpan("Deposit.cs", 5, 7, 5, 14)
                .WithArguments("Deposit"));
        return test.RunAsync();
    }

    [Fact]
    public Task Non_critical_slice_needs_no_criterion() =>
        Make(PlainSlice).RunAsync();

    [Fact]
    public Task Strict_policy_requires_a_criterion_on_an_undecided_slice()
    {
        // No [Critical] marker, but the strict policy makes an undecided slice critical — so a slice with no
        // [Verify] criterion is flagged exactly as if it carried [Critical].
        var test = Make(PlainSlice, criticality: "strict");
        test.TestState.ExpectedDiagnostics.Add(
            new DiagnosticResult(CriticalCriterionAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
                .WithSpan("Deposit.cs", 4, 7, 4, 14)
                .WithArguments("Deposit"));
        return test.RunAsync();
    }

    [Fact]
    public Task Strict_policy_lets_a_non_critical_slice_skip_the_criterion() =>
        // [NonCritical] is the explicit opt-out: even under strict it needs no criterion.
        Make(NonCriticalSlice, criticality: "strict").RunAsync();

    private const string CriticalWithVerify = """
        using System;

        [Slice]
        [Critical]
        [Verify("own-resource-only")]
        class Deposit { }

        sealed class SliceAttribute : Attribute { }
        sealed class CriticalAttribute : Attribute { }
        sealed class VerifyAttribute : Attribute { public VerifyAttribute(string id) { } }
        """;

    private const string CriticalVerifyOnMethod = """
        using System;
        using System.Threading.Tasks;

        [Slice]
        [Critical]
        class Deposit
        {
            [Verify("own-resource-only")]
            public static Task Handle() => Task.CompletedTask;
        }

        sealed class SliceAttribute : Attribute { }
        sealed class CriticalAttribute : Attribute { }
        sealed class VerifyAttribute : Attribute { public VerifyAttribute(string id) { } }
        """;

    private const string CriticalNoVerify = """
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

    private static CSharpAnalyzerTest<CriticalCriterionAnalyzer, DefaultVerifier> Make(
        string source, string? criticality = null)
    {
        var test = new CSharpAnalyzerTest<CriticalCriterionAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.Errors,
            TestState =
            {
                Sources = { ("Deposit.cs", source) },
            },
        };
        if (criticality is not null)
            test.TestState.AnalyzerConfigFiles.Add(
                ("/.globalconfig", $"is_global = true\nbuild_property.AeroFortressCriticality = {criticality}\n"));
        return test;
    }
}
