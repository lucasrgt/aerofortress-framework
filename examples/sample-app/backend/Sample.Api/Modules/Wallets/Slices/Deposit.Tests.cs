using Sample.Api;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sample.Api.BuildingBlocks;
using Sample.Api.Modules.Wallets;

namespace Sample.Tests.Modules.Wallets;

public class DepositTests
{
    private static AppDb NewDb() =>
        new(new DbContextOptionsBuilder<AppDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Unit]
    [Fact]
    public async Task Deposit_increases_the_balance()
    {
        await using var db = NewDb();
        var id = Guid.NewGuid();
        var wallet = Wallet.Open(id).Value;
        wallet.Deposit(Money.From(10m).Value);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var result = await Deposit.Handle(new Deposit.Input(id, 5m), db, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(15m, result.Value.Balance);
    }

    [Unit]
    [Fact]
    public async Task Deposit_rejects_a_negative_amount()
    {
        await using var db = NewDb();

        var result = await Deposit.Handle(new Deposit.Input(Guid.NewGuid(), -1m), db, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        // Collect inherits the value object's own code (Money's), so the field error is localizable end-to-end.
        Assert.Contains(result.Error.Fields!, f => f.Field == "amount" && f.Code == MoneyErrorCodes.Negative);
    }

    [Unit]
    [Fact]
    public async Task Deposit_reports_all_invalid_fields_at_once()
    {
        await using var db = NewDb();

        var result = await Deposit.Handle(new Deposit.Input(Guid.Empty, -1m), db, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal(2, result.Error.Fields!.Count);
    }

    [Unit]
    [Fact]
    public async Task Deposit_into_a_missing_wallet_is_not_found()
    {
        await using var db = NewDb();

        var result = await Deposit.Handle(new Deposit.Input(Guid.NewGuid(), 5m), db, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
    }

    // Integration covers what the unit tests skip: the real HTTP boundary — routing, model
    // binding, and the Result→status mapping — through the booted app.
    [Integration]
    [Fact]
    public async Task Post_deposit_endpoint_returns_the_updated_balance()
    {
        await using var app = new TestApp();
        var client = app.CreateClient();
        var walletId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var response = await client.PostAsJsonAsync("/wallets/deposit", new { walletId, amount = 25m });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Deposit.Output>();
        Assert.Equal(25m, body!.Balance);
    }
}
