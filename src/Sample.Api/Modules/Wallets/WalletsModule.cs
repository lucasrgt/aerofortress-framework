using Lazuli.Abstractions;

namespace Sample.Api.Modules.Wallets;

/// <summary>
/// The Wallets module's wiring root. A module owns both halves of its composition: <see cref="AddServices"/>
/// (its own DI) and <see cref="Map"/> (its routes, under the /wallets base, each slice relative to it). The host
/// calls neither directly — the module registry does, one line per module — so the composition root stays a thin
/// index. Module-wide endpoint config (auth, tags, versioning) belongs in <see cref="Map"/> too, on the group.
/// </summary>
[Module]
public static class WalletsModule
{
    /// <summary>The module's own service registration. Wallets needs none yet — the app-wide DbContext is wired
    /// in the composition root — but the seam is declared uniformly, the way every module carries both halves.</summary>
    public static IServiceCollection AddServices(IServiceCollection services, IConfiguration configuration) =>
        services;

    public static void Map(IEndpointRouteBuilder app)
    {
        var wallets = app.MapGroup("/wallets");

        Deposit.Map(wallets);
        Withdraw.Map(wallets);
        GetBalance.Map(wallets);
    }
}
