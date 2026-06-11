using System.Linq;
using System.Text.Json;

namespace Sample.Tests;

// The numeric pin, document-wide: NumberHandling's read-from-string tolerance is a runtime affordance
// the serializer never writes, so it must not leak into the contract as `type: ["number","string"]` —
// a client generator faithfully turns that into `number | string` and every ViewModel grows a
// Number(x) || 0 coercion. The page test pins the four page members; this one pins numerics OUTSIDE
// the page — an item's body property and a query parameter — so the whole document speaks the wire's
// actual types. Nullability is the one union allowed to survive.
public class OpenApiNumerics
{
    [Fact]
    public async Task A_numeric_body_property_outside_the_page_is_plainly_typed()
    {
        await using var app = new TestApp();
        var client = app.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var balance = document.RootElement
            .GetProperty("components").GetProperty("schemas")
            .GetProperty("ListWalletsWalletView").GetProperty("properties").GetProperty("balance");

        Assert.Equal(JsonValueKind.String, balance.GetProperty("type").ValueKind);
        Assert.Equal("number", balance.GetProperty("type").GetString());
        Assert.False(balance.TryGetProperty("pattern", out _));
    }

    [Fact]
    public async Task A_scalar_value_object_reached_only_through_a_nullable_property_still_mirrors_its_primitive()
    {
        await using var app = new TestApp();
        var client = app.CreateClient();

        // WalletView.LastDeposit is the document's ONLY Money occurrence and it is Money?, so the Money
        // component is created from the Nullable<Money> visit — the case where the mirror must unwrap
        // Nullable to find the ScalarJsonConverter (a pilot's nullable-only scalars leaked as bare {}).
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var money = document.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("Money");

        Assert.Equal("number", money.GetProperty("type").GetString());
        Assert.False(money.TryGetProperty("properties", out _));
    }

    [Fact]
    public async Task A_numeric_query_parameter_is_plainly_typed()
    {
        await using var app = new TestApp();
        var client = app.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var operation = document.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .First(entry => entry.Value.ValueKind == JsonValueKind.Object
                && entry.Value.TryGetProperty("operationId", out var id)
                && id.GetString() == "ListWallets")
            .Value;
        var page = operation.GetProperty("parameters").EnumerateArray()
            .First(parameter => parameter.GetProperty("name").GetString() == "page")
            .GetProperty("schema");

        var types = page.GetProperty("type").ValueKind == JsonValueKind.Array
            ? page.GetProperty("type").EnumerateArray().Select(t => t.GetString()).ToList()
            : [page.GetProperty("type").GetString()];
        Assert.Contains("integer", types);
        Assert.DoesNotContain("string", types);
        Assert.False(page.TryGetProperty("pattern", out _));
    }
}
