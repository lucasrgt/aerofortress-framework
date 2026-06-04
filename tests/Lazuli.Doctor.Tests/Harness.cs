using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Lazuli.Doctor.Tests;

// Runs an analyzer against a markup source: `{|LZxxxx:text|}` marks an expected diagnostic at that
// span; unmarked source must produce none. Pinned to the .NET 8 reference set so the sample sources
// compile (the analyzers are syntactic, so the references only need to make the stubs build).
internal static class Harness<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static Task Verify(string markup) =>
        new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = markup,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            CompilerDiagnostics = CompilerDiagnostics.Errors,
        }.RunAsync();
}
