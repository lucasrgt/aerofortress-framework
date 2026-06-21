using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace AeroFortress.Framework.Doctor.Tests;

public class VerifyProofAnalyzerTests
{
    [Fact]
    public Task Verify_with_a_matching_avp_proof_in_source_reports_nothing() =>
        Harness<VerifyProofAnalyzer>.Verify(ProvenInSource);

    [Fact]
    public Task Verify_with_an_avp_proof_in_a_test_file_reports_nothing() =>
        Make(Production, AvpProofFile).RunAsync();

    [Fact]
    public Task Verify_without_any_avp_proof_is_flagged() =>
        Harness<VerifyProofAnalyzer>.Verify(Unproven);

    private const string ProvenInSource = """
        using System;

        [Verify("own-resource-only")]
        public class Deposit { }

        public class DepositProof
        {
            [AVP("own-resource-only")]
            public void Proves() { }
        }

        sealed class VerifyAttribute : Attribute { public VerifyAttribute(string id) { } }
        sealed class AVPAttribute : Attribute { public AVPAttribute(string id) { } }
        """;

    private const string Unproven = """
        using System;

        [{|LZ0030:Verify("own-resource-only")|}]
        public class Deposit { }

        sealed class VerifyAttribute : Attribute { public VerifyAttribute(string id) { } }
        """;

    // The production code declares the obligation; the AVP proof lives in an excluded test file.
    private const string Production = """
        using System;

        [Verify("own-resource-only")]
        public class Deposit { }

        sealed class VerifyAttribute : Attribute { public VerifyAttribute(string id) { } }
        """;

    private const string AvpProofFile = """
        [AVP("own-resource-only")]
        public void Proves() { }
        """;

    private static CSharpAnalyzerTest<VerifyProofAnalyzer, DefaultVerifier> Make(string source, string proofs) =>
        new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.Errors,
            TestState =
            {
                Sources = { ("Deposit.cs", source) },
                AdditionalFiles = { ("DepositProof.Tests.cs", proofs) },
            },
        };
}
