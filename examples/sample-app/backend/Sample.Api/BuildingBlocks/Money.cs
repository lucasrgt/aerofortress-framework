using System.Text.Json.Serialization;

namespace Sample.Api.BuildingBlocks;

/// <summary>
/// A non-negative monetary amount. The type <em>is</em> the rule: you cannot construct a
/// negative <see cref="Money"/>, so any <see cref="Money"/> in the system is already valid.
/// It lives in BuildingBlocks because it is generic and domain-agnostic — shared by any
/// module, owned by none. (A module-specific value object would live inside the module.)
/// On the wire it is transparent: the <see cref="ScalarJsonConverter{TValueObject,TPrimitive}"/>
/// serializes it as its <see cref="Amount"/> number (and the framework's OpenAPI wiring mirrors
/// that in the contract), so the richness is a backend guarantee, not a contract change.
/// </summary>
[ValueObject]
[JsonConverter(typeof(MoneyJsonConverter))]
public readonly record struct Money
{
    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static readonly Money Zero = new(0m);

    /// <summary>Build a <see cref="Money"/>, rejecting negative amounts as a domain error.</summary>
    public static Result<Money> From(decimal amount) =>
        amount >= 0
            ? Result<Money>.Ok(new Money(amount))
            : Error.Validation(MoneyErrorCodes.Negative, "money cannot be negative");

    public Money Add(Money other) => new(Amount + other.Amount);

    /// <summary>
    /// Debit <paramref name="other"/>, rejecting an overdraw: the difference would be negative, which
    /// <see cref="Money"/> forbids. Returns the failure instead of throwing so the caller — typically an
    /// entity mutator — can restate it in its own domain language (e.g. "insufficient funds").
    /// </summary>
    public Result<Money> Subtract(Money other) => From(Amount - other.Amount);

    public override string ToString() => Amount.ToString("0.00");

    // Wire transparency: Money crosses the boundary as its amount, validated back in through From.
    private sealed class MoneyJsonConverter() : ScalarJsonConverter<Money, decimal>(m => m.Amount, From);
}
