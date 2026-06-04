using System.Net;
using System.Net.Http.Json;
using Sample.Api.Modules.Wallets;

namespace Sample.Tests.Journeys;

// Journeys sit beside Modules: a cross-module flow is an app-level concept, and its e2e test has no
// single slice to co-locate with. The file is *.Tests.cs, so the test project compiles it (via glob)
// and the production assembly excludes it — same mechanism as the slice tests.
//
// Deposit is [Critical], so LZ0008 requires both a happy and a sad journey here. Each is declared with
// [Journey(typeof(Deposit), JourneyPath.…)] so the doctor can match them to the slice.
public class WalletJourney
{
    private static readonly Guid Seeded = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [E2E]
    [Journey(typeof(Deposit), JourneyPath.Happy)]
    [Fact]
    public async Task Deposit_then_balance_reflects_the_deposit()
    {
        await using var app = new TestApp();
        var client = app.CreateClient();

        await client.PostAsJsonAsync("/wallets/deposit", new { walletId = Seeded, amount = 30m });
        var balance = await client.GetFromJsonAsync<GetBalance.Output>($"/wallets/{Seeded}/balance");

        Assert.Equal(30m, balance!.Balance);
    }

    // The sad journey proves the property only end-to-end can: a rejected deposit returns the failure
    // status AND leaves the balance untouched — no partial state crosses the boundary.
    [E2E]
    [Journey(typeof(Deposit), JourneyPath.Sad)]
    [Fact]
    public async Task Rejected_deposit_leaves_the_balance_untouched()
    {
        await using var app = new TestApp();
        var client = app.CreateClient();
        var before = await client.GetFromJsonAsync<GetBalance.Output>($"/wallets/{Seeded}/balance");

        var response = await client.PostAsJsonAsync("/wallets/deposit", new { walletId = Seeded, amount = -5m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var after = await client.GetFromJsonAsync<GetBalance.Output>($"/wallets/{Seeded}/balance");
        Assert.Equal(before!.Balance, after!.Balance);
    }
}
