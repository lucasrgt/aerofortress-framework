using Lazuli.EntityFrameworkCore;

namespace Sample.Api.Modules.Wallets;

/// <summary>List wallets one page at a time — the canonical paginated list slice. The query is ordered
/// by a unique key (<c>Id</c>) so page boundaries are deterministic, paged by <c>ToPageAsync</c> (clamp,
/// count and page over the same queryable, effective values echoed), and projected <em>after</em> paging
/// with <c>Page.Select</c> — a <c>.Select</c> before would erase the <c>IOrderedQueryable</c> the
/// extension requires. The page travels inside <c>Output</c> by composition, never record inheritance.</summary>
[Slice]
public static class ListWallets
{
    public record Input(int Page = 1, int PageSize = 20);

    public record WalletView(Guid WalletId, decimal Balance);

    public record Output(Page<WalletView> Wallets);

    private const int MaxPageSize = 100;

    public static async Task<Result<Output>> Handle(Input input, AppDb db, CancellationToken ct)
    {
        var wallets = await db.Wallets.OrderBy(w => w.Id)
            .ToPageAsync(input.Page, input.PageSize, MaxPageSize, ct);
        return new Output(wallets.Select(w => new WalletView(w.Id, w.Balance.Amount)));
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/", async (int? page, int? pageSize, AppDb db, CancellationToken ct) =>
                (await Handle(new Input(page ?? 1, pageSize ?? 20), db, ct)).ToHttp())
            .WithName(nameof(ListWallets));
}
