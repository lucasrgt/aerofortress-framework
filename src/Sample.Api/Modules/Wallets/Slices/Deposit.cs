using Microsoft.EntityFrameworkCore;

namespace Sample.Api.Modules.Wallets;

/// <summary>Deposit money into a wallet.</summary>
/// <remarks>
/// Why: deposits are the only inflow that grows a balance; all top-ups funnel here. The
/// balance is authoritative server-side — recompute from the stored value, never trust a
/// client-sent total. Not idempotent yet: a retried request double-deposits (idempotency
/// keys are planned). The "why" lives in this header because the slice is self-contained; it
/// graduates to a separate Deposit.ctx.md only if it ever outgrows a header.
/// </remarks>
[Slice]
[Critical]
public static class Deposit
{
    public record Input(Guid WalletId, decimal Amount);

    public record Output(Guid WalletId, decimal Balance);

    public static async Task<Result<Output>> Handle(Input input, AppDb db, CancellationToken ct)
    {
        // Accumulate every input error, then report them together. Money stays the single source
        // of the amount rule — we only collect its failure, never restate "amount >= 0" here.
        var amount = Money.From(input.Amount);
        var validation = new Validation()
            .Check(input.WalletId != Guid.Empty, "walletId", "is required")
            .Collect("amount", amount);
        if (validation.Failed)
            return validation.ToError();

        var wallet = await db.Wallets.FindAsync([input.WalletId], ct);
        if (wallet is null)
            return Error.NotFound($"wallet {input.WalletId} not found");

        var updated = wallet.Balance.Add(amount.Value);
        wallet.Balance = updated;
        await db.SaveChangesAsync(ct);

        return new Output(wallet.Id, updated.Amount);
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/deposit",
            async (Input input, AppDb db, CancellationToken ct) =>
                (await Handle(input, db, ct)).ToHttp());
}
