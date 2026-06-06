using Sample.Api;
using Microsoft.EntityFrameworkCore;
using Sample.Api.BuildingBlocks;
using Sample.Api.Modules.Wallets;

namespace Sample.Tests.Modules.Wallets;

public class GetBalanceTests
{
    private static AppDb NewDb() =>
        new(new DbContextOptionsBuilder<AppDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Unit]
    [Fact]
    public async Task Returns_the_stored_balance()
    {
        await using var db = NewDb();
        var id = Guid.NewGuid();
        var wallet = Wallet.Open(id).Value;
        wallet.Deposit(Money.From(42m).Value);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var result = await GetBalance.Handle(new GetBalance.Input(id), db, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(42m, result.Value.Balance);
    }

    [Unit]
    [Fact]
    public async Task Missing_wallet_is_not_found()
    {
        await using var db = NewDb();

        var result = await GetBalance.Handle(new GetBalance.Input(Guid.NewGuid()), db, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
    }
}
