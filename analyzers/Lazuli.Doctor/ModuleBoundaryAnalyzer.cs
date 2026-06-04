using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Lazuli.Doctor;

/// <summary>
/// LZ0009 — write-ownership across modules. In the modular monolith every module shares one
/// <c>DbContext</c>, so reading or joining another module's tables is fine (a dashboard is one query). What
/// stays forbidden is <em>writing</em> another module's entity: a slice may only <c>Add</c>/<c>Update</c>/
/// <c>Remove</c> entities of its own module. To affect another module, call its service (in-process) or
/// enqueue a job — never mutate its tables. This is the cheap discipline that keeps a context carvable into
/// its own service later: its writes are already self-contained, so an extraction re-platforms only the
/// cross-module <em>reads</em>, never the writes.
///
/// The check is semantic: a <c>DbSet&lt;T&gt;</c> write call (Add / AddRange / Update / UpdateRange / Remove
/// / RemoveRange / Attach / AttachRange and the <c>*Async</c> forms) whose entity <c>T</c> lives under a
/// different <c>Modules.&lt;X&gt;</c> than the calling type. Reads (Where / Any / First / …), joins, and
/// cross-module service calls are not flagged. <c>.Tests.cs</c> files are exempt — a test legitimately seeds
/// any module's data. (The untyped <c>DbContext.Add(entity)</c> form is not yet covered; <c>DbSet</c> writes
/// are the common case.)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModuleBoundaryAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier reported for a write into another module's entity.</summary>
    public const string DiagnosticId = "LZ0009";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "A module must write only its own entities",
        messageFormat: "Module '{0}' writes module '{1}'s entity '{2}' — a module owns its writes; call "
                     + "{1}'s service or enqueue a job instead of mutating its tables (reads/joins are fine)",
        category: "Lazuli.Convention",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "In the modular monolith, reads/joins across modules are free but writes are owned: a "
                   + "slice may only Add/Update/Remove its own module's entities. Cross-module effects go "
                   + "through the owning module's service or a job, which keeps each context carvable later.");

    private static readonly ImmutableHashSet<string> WriteMethods = ImmutableHashSet.Create(
        "Add", "AddAsync", "AddRange", "AddRangeAsync",
        "Update", "UpdateRange", "Remove", "RemoveRange", "Attach", "AttachRange");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
            return;
        if (!WriteMethods.Contains(member.Name.Identifier.Text))
            return;
        if (IsTestFile(invocation.SyntaxTree.FilePath))
            return;   // a test legitimately seeds any module's data

        // The receiver must be a DbSet<TEntity>.
        if (context.SemanticModel.GetTypeInfo(member.Expression, context.CancellationToken).Type is not INamedTypeSymbol receiver)
            return;
        if (receiver.OriginalDefinition.Name != "DbSet"
            || receiver.OriginalDefinition.ContainingNamespace?.ToDisplayString() != "Microsoft.EntityFrameworkCore"
            || receiver.TypeArguments.Length != 1)
            return;

        var entityModule = ModuleOf(receiver.TypeArguments[0].ContainingNamespace?.ToDisplayString());
        if (entityModule is null)
            return;   // the entity is not under Modules.<X> (kernel / shared) — nothing to own

        var callerModule = ModuleOf(EnclosingNamespace(context.SemanticModel, invocation, context.CancellationToken));
        if (callerModule is null || callerModule == entityModule)
            return;   // caller is the composition root / kernel, or it is writing its own module

        context.ReportDiagnostic(Diagnostic.Create(
            Rule, member.Name.GetLocation(), callerModule, entityModule, receiver.TypeArguments[0].Name));
    }

    // The module segment of a namespace like "App.Api.Modules.Account[.Slices]" → "Account"; null when the
    // namespace is not under a Modules.<X> root.
    private static string? ModuleOf(string? ns)
    {
        const string marker = ".Modules.";
        var at = ns?.IndexOf(marker, System.StringComparison.Ordinal) ?? -1;
        if (at < 0)
            return null;
        var rest = ns!.Substring(at + marker.Length);
        var dot = rest.IndexOf('.');
        return dot < 0 ? rest : rest.Substring(0, dot);
    }

    // A test file under the framework's <Concern>.Tests.cs convention (the same signal LZ0003 keys on).
    private static bool IsTestFile(string? path) =>
        path is not null && path.EndsWith(".Tests.cs", System.StringComparison.Ordinal);

    // The namespace of the type that lexically contains the invocation (the calling slice).
    private static string? EnclosingNamespace(SemanticModel model, SyntaxNode node, CancellationToken ct)
    {
        for (var n = node; n is not null; n = n.Parent)
            if (n is BaseTypeDeclarationSyntax type)
                return model.GetDeclaredSymbol(type, ct)?.ContainingNamespace?.ToDisplayString();
        return null;
    }
}
