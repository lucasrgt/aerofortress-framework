namespace Sample.Api.Modules.Wallets;

/// <summary>
/// A wallet with a running balance. The entity lives inside the Wallets module, next to the
/// slices that use it — not in a root Domain folder — so the module stays the unit you could
/// later lift into its own service.
/// </summary>
public class Wallet
{
    public Guid Id { get; set; }
    public Money Balance { get; set; }
}
