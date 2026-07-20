using Sample.Api;
using Microsoft.EntityFrameworkCore;
using Sample.Api.Modules.Wallets;
using Assay.Net;

namespace Sample.Tests.Modules.Wallets;

public class ListWalletsTests
{
    private static AppDb NewDb() =>
        new(new DbContextOptionsBuilder<AppDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [AVP(typeof(ListWallets), "returns-stable-page")]
    [Unit]
    [Fact]
    public async Task A_page_carries_its_slice_and_the_whole_sets_count()
    {
        await using var db = NewDb();
        await Seed(db, wallets: 5);

        var result = await ListWallets.Handle(new ListWallets.Input(Page: 2, PageSize: 2), db, default);

        Assert.True(result.IsSuccess);
        var page = result.Value.Wallets;
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(2, page.PageSize);
    }

    [Unit]
    [Fact]
    public async Task Hostile_paging_inputs_are_clamped_server_side()
    {
        // MaxPageSize is the slice's policy, not the client's: a huge pageSize lands on the slice's
        // ceiling and the echoed values are the effective ones the pager can trust.
        await using var db = NewDb();
        await Seed(db, wallets: 3);

        var result = await ListWallets.Handle(new ListWallets.Input(Page: -2, PageSize: 10_000), db, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Wallets.PageNumber);
        Assert.Equal(100, result.Value.Wallets.PageSize);
        Assert.Equal(3, result.Value.Wallets.Items.Count);
    }

    private static async Task Seed(AppDb db, int wallets)
    {
        for (var i = 0; i < wallets; i++)
            db.Wallets.Add(Wallet.Open(Guid.NewGuid()).Value);
        await db.SaveChangesAsync();
    }
}
