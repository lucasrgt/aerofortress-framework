using Sample.Api;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sample.Api.BuildingBlocks;
using Sample.Api.Modules.Wallets;

namespace Sample.Tests.Modules.Wallets;

public class WithdrawTests
{
    private static AppDb NewDb() =>
        new(new DbContextOptionsBuilder<AppDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // Open a wallet and fund it through the same encapsulated path the app uses — no setter back door.
    private static async Task<Guid> SeedFunded(AppDb db, decimal balance)
    {
        var id = Guid.NewGuid();
        var wallet = Wallet.Open(id).Value;
        wallet.Deposit(Money.From(balance).Value);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();
        return id;
    }

    [Unit]
    [Fact]
    public async Task Withdraw_decreases_the_balance()
    {
        await using var db = NewDb();
        var id = await SeedFunded(db, 50m);

        var result = await Withdraw.Handle(new Withdraw.Input(id, 20m), db, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(30m, result.Value.Balance);
    }

    // The invariant that justifies the entity owning Withdraw: an overdraw is refused as a business rule
    // (422, not a 400 input error) and changes nothing — the rule cannot be bypassed by any caller.
    [Unit]
    [Fact]
    public async Task Overdrawing_is_a_business_rule_and_leaves_the_balance_intact()
    {
        await using var db = NewDb();
        var id = await SeedFunded(db, 10m);

        var result = await Withdraw.Handle(new Withdraw.Input(id, 999m), db, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.BusinessRule, result.Error.Kind);
        var balance = await GetBalance.Handle(new GetBalance.Input(id), db, default);
        Assert.Equal(10m, balance.Value.Balance);
    }

    [Unit]
    [Fact]
    public async Task Withdraw_rejects_a_negative_amount()
    {
        await using var db = NewDb();

        var result = await Withdraw.Handle(new Withdraw.Input(Guid.NewGuid(), -1m), db, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Contains(result.Error.Fields!, f => f.Field == "amount");
    }

    [Unit]
    [Fact]
    public async Task Withdraw_from_a_missing_wallet_is_not_found()
    {
        await using var db = NewDb();

        var result = await Withdraw.Handle(new Withdraw.Input(Guid.NewGuid(), 5m), db, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
    }

    [Integration]
    [Fact]
    public async Task Post_withdraw_endpoint_returns_the_updated_balance()
    {
        await using var app = new TestApp();
        var client = app.CreateClient();
        var walletId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await client.PostAsJsonAsync("/wallets/deposit", new { walletId, amount = 25m });

        var response = await client.PostAsJsonAsync("/wallets/withdraw", new { walletId, amount = 10m });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Withdraw.Output>();
        Assert.Equal(15m, body!.Balance);
    }
}
