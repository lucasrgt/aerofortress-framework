using Microsoft.EntityFrameworkCore;

namespace Sample.Api.Modules.Wallets;

/// <summary>Read a wallet's current balance.</summary>
[Slice]
public static class GetBalance
{
    public record Input(Guid WalletId);

    public record Output(Guid WalletId, decimal Balance);

    public static async Task<Result<Output>> Handle(Input input, AppDb db, CancellationToken ct)
    {
        var wallet = await db.Wallets.FindAsync([input.WalletId], ct);
        return wallet is null
            ? Error.NotFound(WalletsErrorCodes.NotFound, $"wallet {input.WalletId} not found")
            : new Output(wallet.Id, wallet.Balance.Amount);
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/{walletId:guid}/balance",
                async (Guid walletId, AppDb db, CancellationToken ct) =>
                    (await Handle(new Input(walletId), db, ct)).ToHttp())
            .WithName(nameof(GetBalance));
}
