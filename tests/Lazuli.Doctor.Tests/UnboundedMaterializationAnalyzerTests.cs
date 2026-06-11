namespace Lazuli.Doctor.Tests;

public class UnboundedMaterializationAnalyzerTests
{
    [Fact]
    public Task A_dbset_materialized_with_no_bound_is_flagged() =>
        Harness<UnboundedMaterializationAnalyzer>.Verify("""
            using Lazuli.Abstractions;
            using Microsoft.EntityFrameworkCore;

            [Slice]
            static class ListAll
            {
                static async System.Threading.Tasks.Task Handle(Db db, System.Threading.CancellationToken ct)
                {
                    var all = await db.Wallets.{|LZ0027:ToListAsync|}(ct);
                }
            }
            """ + Stubs);

    [Fact]
    public Task A_take_in_the_chain_is_the_bound() =>
        Harness<UnboundedMaterializationAnalyzer>.Verify("""
            using System.Linq;
            using Lazuli.Abstractions;
            using Microsoft.EntityFrameworkCore;

            [Slice]
            static class ListSome
            {
                static async System.Threading.Tasks.Task Handle(Db db, System.Threading.CancellationToken ct)
                {
                    var some = await db.Wallets.OrderBy(w => w.Id).Skip(20).Take(20).ToListAsync(ct);
                }
            }
            """ + Stubs);

    [Fact]
    public Task A_dbset_rooted_local_with_no_bound_anywhere_is_flagged() =>
        Harness<UnboundedMaterializationAnalyzer>.Verify("""
            using System.Linq;
            using Lazuli.Abstractions;
            using Microsoft.EntityFrameworkCore;

            [Slice]
            static class Search
            {
                static async System.Threading.Tasks.Task Handle(Db db, string? q, System.Threading.CancellationToken ct)
                {
                    var query = db.Wallets.AsQueryable();
                    if (q is not null)
                        query = query.Where(w => w.Id.ToString() == q);
                    var hits = await query.{|LZ0027:ToListAsync|}(ct);
                }
            }
            """ + Stubs);

    [Fact]
    public Task A_bound_on_any_assignment_to_the_local_counts() =>
        Harness<UnboundedMaterializationAnalyzer>.Verify("""
            using System.Linq;
            using Lazuli.Abstractions;
            using Microsoft.EntityFrameworkCore;

            [Slice]
            static class Search
            {
                static async System.Threading.Tasks.Task Handle(Db db, System.Threading.CancellationToken ct)
                {
                    var query = db.Wallets.AsQueryable();
                    query = query.Take(50);
                    var hits = await query.ToListAsync(ct);
                }
            }
            """ + Stubs);

    [Fact]
    public Task An_in_memory_queryable_local_is_not_a_database_query() =>
        Harness<UnboundedMaterializationAnalyzer>.Verify("""
            using System.Linq;
            using Lazuli.Abstractions;
            using Microsoft.EntityFrameworkCore;

            [Slice]
            static class Tally
            {
                static async System.Threading.Tasks.Task Handle(System.Collections.Generic.List<int> ids, System.Threading.CancellationToken ct)
                {
                    var query = ids.AsQueryable();
                    var all = await query.ToListAsync(ct);
                }
            }
            """ + Stubs);

    [Fact]
    public Task Outside_a_slice_the_rule_stays_silent() =>
        Harness<UnboundedMaterializationAnalyzer>.Verify("""
            using Microsoft.EntityFrameworkCore;

            static class Seeder
            {
                static async System.Threading.Tasks.Task Run(Db db, System.Threading.CancellationToken ct)
                {
                    var all = await db.Wallets.ToListAsync(ct);
                }
            }
            """ + Stubs);

    // A tiny stand-in for the EF + Lazuli surface so the tests need no package references; the rule
    // matches DbSet/IQueryable by type name and the chain shape syntactically.
    private const string Stubs = """


        namespace Lazuli.Abstractions
        {
            public sealed class SliceAttribute : System.Attribute { }
        }

        namespace Microsoft.EntityFrameworkCore
        {
            public class DbSet<T> : System.Linq.IQueryable<T>
            {
                public System.Type ElementType => typeof(T);
                public System.Linq.Expressions.Expression Expression => null!;
                public System.Linq.IQueryProvider Provider => null!;
                public System.Collections.Generic.IEnumerator<T> GetEnumerator() => null!;
                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null!;
            }

            public static class EfQueryableExtensions
            {
                public static System.Threading.Tasks.Task<System.Collections.Generic.List<T>> ToListAsync<T>(
                    this System.Linq.IQueryable<T> query, System.Threading.CancellationToken ct = default) => null!;
            }
        }

        class Db
        {
            public Microsoft.EntityFrameworkCore.DbSet<Wallet> Wallets => null!;
        }

        class Wallet
        {
            public System.Guid Id { get; set; }
        }
        """;
}
