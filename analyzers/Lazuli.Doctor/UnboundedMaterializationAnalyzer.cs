using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Lazuli.Doctor;

/// <summary>
/// LZ0027 — <b>a slice must not materialize an unbounded set</b>. A <c>ToListAsync</c>/<c>ToList</c> (or the
/// array twins) at the end of a <c>DbSet</c>-rooted chain with no <c>Take</c> and no <c>ToPageAsync</c> on the
/// way serves a whole table as one response. The defect is invisible in development — ten rows always come back
/// fast — and degrades in production as the tenant's data grows, months later. Warning tier on purpose:
/// legitimately small sets exist (lookup tables, the lines of one order), and the fix is also the
/// documentation — <c>.Take(n)</c> writes the bound down, <c>ToPageAsync</c> pages it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnboundedMaterializationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier reported when a slice materializes a DbSet-rooted query with no bound.</summary>
    public const string DiagnosticId = "LZ0027";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "A slice must not materialize an unbounded set",
        messageFormat: "'{0}' materializes a DbSet-rooted query with no bound — page it with ToPageAsync or "
                     + "write the bound down with Take",
        category: "Lazuli.Convention",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A list served straight off a DbSet grows with the data: fine with ten development rows, "
                   + "a multi-second response and a memory spike once a tenant has fifty thousand. ToPageAsync "
                   + "pages it behind a stable order; an explicit Take(n) documents why the set is small.");

    private static readonly ImmutableHashSet<string> Materializers =
        ImmutableHashSet.Create("ToListAsync", "ToArrayAsync", "ToList", "ToArray");

    private static readonly ImmutableHashSet<string> Bounds = ImmutableHashSet.Create("Take", "ToPageAsync");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax member
            || !Materializers.Contains(member.Name.Identifier.Text)
            || !InsideSlice(invocation))
            return;

        // Walk the fluent chain from the materializer back to its root. A Take/ToPageAsync on the way is
        // the bound; a receiver typed DbSet on the way is the unbounded root.
        var expression = member.Expression;
        var rootsInDbSet = false;
        while (true)
        {
            if (IsDbSet(context.SemanticModel.GetTypeInfo(expression).Type))
            {
                rootsInDbSet = true;
                break;
            }
            if (expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax inner })
            {
                if (Bounds.Contains(inner.Name.Identifier.Text))
                    return;
                expression = inner.Expression;
                continue;
            }
            break;
        }

        // A chain rooted in a local (the `query = db.Points.AsQueryable(); query = query.Where(...)` shape):
        // the bound may live in any assignment to that local, and only a DbSet somewhere in those
        // assignments makes the local a database query rather than an in-memory AsQueryable.
        if (!rootsInDbSet)
        {
            if (expression is not IdentifierNameSyntax local || !IsQueryableName(context.SemanticModel.GetTypeInfo(local).Type))
                return;
            var sources = AssignmentsTo(invocation, local.Identifier.Text);
            if (sources.Count == 0 || sources.Any(ContainsBound))
                return;
            if (!sources.Any(source => source.DescendantNodesAndSelf().OfType<ExpressionSyntax>()
                    .Any(node => IsDbSet(context.SemanticModel.GetTypeInfo(node).Type))))
                return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, member.Name.GetLocation(), member.Name.Identifier.Text));
    }

    private static bool InsideSlice(SyntaxNode node) =>
        node.Ancestors().OfType<TypeDeclarationSyntax>().Any(type =>
            type.AttributeLists.SelectMany(list => list.Attributes)
                .Any(attribute => NameOf(attribute) is "Slice" or "SliceAttribute"));

    private static string NameOf(AttributeSyntax attribute) => attribute.Name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
        SimpleNameSyntax simple => simple.Identifier.Text,
        _ => "",
    };

    private static bool IsDbSet(ITypeSymbol? type) => type?.Name == "DbSet";

    private static bool IsQueryableName(ITypeSymbol? type) =>
        type?.Name is "IQueryable" or "IOrderedQueryable" or "DbSet";

    // Every expression assigned to `name` in the enclosing method — the declarator's initializer and any
    // re-assignment — so the bound is honored wherever the query was narrowed.
    private static System.Collections.Generic.List<ExpressionSyntax> AssignmentsTo(SyntaxNode from, string name)
    {
        var method = from.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
            return [];
        var sources = new System.Collections.Generic.List<ExpressionSyntax>();
        foreach (var node in method.DescendantNodes())
            switch (node)
            {
                case VariableDeclaratorSyntax declarator
                    when declarator.Identifier.Text == name && declarator.Initializer is { } initializer:
                    sources.Add(initializer.Value);
                    break;
                case AssignmentExpressionSyntax { Left: IdentifierNameSyntax left } assignment
                    when left.Identifier.Text == name:
                    sources.Add(assignment.Right);
                    break;
            }
        return sources;
    }

    private static bool ContainsBound(ExpressionSyntax expression) =>
        expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
            .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax member
                && Bounds.Contains(member.Name.Identifier.Text));
}
