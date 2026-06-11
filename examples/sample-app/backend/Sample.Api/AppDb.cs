using Microsoft.EntityFrameworkCore;
using Sample.Api.BuildingBlocks;
using Sample.Api.Modules.Wallets;

namespace Sample.Api;

/// <summary>
/// The application's single database — one logical store for every module's tables. In the modular monolith
/// a module is a bounded context <em>by convention</em>: it owns and writes only its own entities, and
/// references another module by id (a <see cref="System.Guid"/>), never an EF relationship — so a context
/// could be carved into its own database later. But all modules share one <see cref="DbContext"/>, so a read
/// can join across them in-process (the dashboard case). Storage facts (converters, precision, indexes) live
/// here; domain invariants live in the value objects, never restated here.
/// </summary>
public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Wallet> Wallets => Set<Wallet>();

    protected override void OnModelCreating(ModelBuilder model) =>
        // Money persists as its decimal Amount and rehydrates via Money.From — the lone defensive .Value at
        // the DB↔domain boundary, so a corrupted stored amount fails loud at materialization. The
        // "balance >= 0" rule is NOT restated; Money owns it. Precision pins money's scale.
        model.Entity<Wallet>()
            .Property(w => w.Balance)
            .HasConversion(m => m.Amount, d => Money.From(d).Value)
            .HasPrecision(18, 2);
}
