using Microsoft.EntityFrameworkCore;

namespace Sample.Api.Modules.Wallets;

/// <summary>Withdraw money from a wallet.</summary>
/// <remarks>
/// Why: a withdrawal is the only outflow that shrinks a balance, and it must never overdraw. That
/// invariant lives on the <see cref="Wallet"/> entity — <c>Wallet.Withdraw</c> returns a failure when the
/// balance cannot cover the amount — so no slice can bypass it and the balance stays authoritative
/// server-side. The slice only orchestrates: validate input, load, delegate, persist. Not idempotent yet:
/// a retried request double-withdraws (idempotency keys are planned), which is why it is [Critical].
/// </remarks>
[Slice]
[Critical]
public static class Withdraw
{
    public record Input(Guid WalletId, decimal Amount);

    public record Output(Guid WalletId, decimal Balance);

    public static async Task<Result<Output>> Handle(Input input, AppDb db, CancellationToken ct)
    {
        var amount = Money.From(input.Amount);
        var validation = new Validation()
            .Check(input.WalletId != Guid.Empty, "walletId", "walletId.required", "is required")
            .Collect("amount", amount);
        if (validation.Failed)
            return validation.ToError();

        var wallet = await db.Wallets.FindAsync([input.WalletId], ct);
        if (wallet is null)
            return Error.NotFound("wallets.not_found", $"wallet {input.WalletId} not found");

        // The overdraw rule lives on the entity; the slice only propagates its verdict. On failure the
        // balance is untouched and SaveChanges never runs — no partial state crosses the boundary.
        var withdrawn = wallet.Withdraw(amount.Value);
        if (withdrawn.IsFailure)
            return withdrawn.Error;

        await db.SaveChangesAsync(ct);
        return new Output(wallet.Id, wallet.Balance.Amount);
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/withdraw",
                async (Input input, AppDb db, CancellationToken ct) =>
                    (await Handle(input, db, ct)).ToHttp())
            .WithName(nameof(Withdraw));
}
