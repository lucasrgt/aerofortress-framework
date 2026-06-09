using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Lazuli.Doctor;

/// <summary>
/// LZ0026 — <b>a <c>[Critical]</c> write declares its concurrency posture</b>. A <c>[Critical]</c> slice whose
/// <c>Handle</c> saves changes against an entity that carries no concurrency token runs last-write-wins: two
/// concurrent requests both read the same row, both mutate, and the second save silently erases the first —
/// the classic double-spend on exactly the slices marked high-stakes. The token is one property:
/// <c>[Timestamp] public byte[]? RowVersion {{ get; private set; }}</c> (or <c>[ConcurrencyCheck]</c> on a
/// domain field), after which a conflicting save surfaces as <c>DbUpdateConcurrencyException</c> instead of
/// silent loss. Warning-tier: a fluent-only configuration the doctor cannot see is legal — name the property
/// <c>RowVersion</c> (recognized) or tune the severity in <c>.editorconfig</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CriticalConcurrencyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier reported when a critical slice saves an entity that has no concurrency token.</summary>
    public const string DiagnosticId = "LZ0026";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "A [Critical] write should be guarded by a concurrency token",
        messageFormat: "Critical slice '{0}' saves entity '{1}' which declares no concurrency token — "
                     + "concurrent requests are last-write-wins; add a [Timestamp] RowVersion (or "
                     + "[ConcurrencyCheck]) so a conflicting save fails loudly instead of silently losing a write",
        category: "Lazuli.Convention",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A [Critical] slice that saves changes against an entity with no concurrency token "
                   + "(no [Timestamp]/[ConcurrencyCheck] member, no RowVersion property) runs last-write-wins "
                   + "on exactly the operations marked high-stakes. Warning-tier: fluent-only configuration "
                   + "is invisible to the doctor — name the property RowVersion or tune the severity.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var cls = (ClassDeclarationSyntax)context.Node;
        if (!HasAttribute(cls, "Slice") || !HasAttribute(cls, "Critical"))
            return;

        var handle = cls.Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "Handle");
        if (handle is null || !SavesChanges(handle))
            return; // a read-only critical slice has no write to guard

        foreach (var entity in TouchedEntities(handle, context))
            if (!HasConcurrencyToken(entity))
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule, cls.Identifier.GetLocation(), cls.Identifier.Text, entity.Name));
    }

    private static bool SavesChanges(MethodDeclarationSyntax handle) =>
        handle.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(ma => ma.Name.Identifier.Text is "SaveChanges" or "SaveChangesAsync");

    // Every entity the Handle touches through a DbSet<T> — the set of rows the save can affect.
    private static IEnumerable<INamedTypeSymbol> TouchedEntities(
        MethodDeclarationSyntax handle, SyntaxNodeAnalysisContext context)
    {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var expression in handle.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            if (context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type
                    is INamedTypeSymbol { Name: "DbSet", TypeArguments.Length: 1 } set
                && set.ContainingNamespace?.ToDisplayString() == "Microsoft.EntityFrameworkCore"
                && set.TypeArguments[0] is INamedTypeSymbol entity
                && seen.Add(entity))
                yield return entity;
    }

    // The token the doctor can see: a member marked [Timestamp]/[ConcurrencyCheck], or a property named
    // RowVersion (the conventional name, also the escape hatch for fluent-only configuration).
    private static bool HasConcurrencyToken(INamedTypeSymbol entity) =>
        entity.GetMembers().Any(member =>
            member.Name == "RowVersion"
            || member.GetAttributes().Any(a => a.AttributeClass?.Name
                    is "Timestamp" or "TimestampAttribute"
                    or "ConcurrencyCheck" or "ConcurrencyCheckAttribute"));

    private static bool HasAttribute(ClassDeclarationSyntax cls, string name) =>
        cls.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attr => attr.Name.ToString())
            .Any(n => n == name || n == name + "Attribute"
                   || n.EndsWith("." + name) || n.EndsWith("." + name + "Attribute"));
}
