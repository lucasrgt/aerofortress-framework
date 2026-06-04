namespace Sample.Api.BuildingBlocks;

/// <summary>
/// A non-negative monetary amount. The type <em>is</em> the rule: you cannot construct a
/// negative <see cref="Money"/>, so any <see cref="Money"/> in the system is already valid.
/// It lives in BuildingBlocks because it is generic and domain-agnostic — shared by any
/// module, owned by none. (A module-specific value object would live inside the module.)
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static readonly Money Zero = new(0m);

    /// <summary>Build a <see cref="Money"/>, rejecting negative amounts as a domain error.</summary>
    public static Result<Money> From(decimal amount) =>
        amount >= 0
            ? Result<Money>.Ok(new Money(amount))
            : Error.Validation("money cannot be negative");

    public Money Add(Money other) => new(Amount + other.Amount);

    public override string ToString() => Amount.ToString("0.00");
}
