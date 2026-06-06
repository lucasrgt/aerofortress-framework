namespace Sample.Api.Modules.Wallets;

/// <summary>
/// The Wallets module's wiring entry point. It owns the module's route base (/wallets) once,
/// and registers each slice's endpoint relative to it. The host calls this — one line per
/// module — instead of mapping each slice individually. Module-wide endpoint config (auth,
/// tags, versioning) belongs here too, applied to the group so every slice inherits it.
/// </summary>
public static class WalletsModule
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var wallets = app.MapGroup("/wallets");

        Deposit.Map(wallets);
        Withdraw.Map(wallets);
        GetBalance.Map(wallets);
    }
}
