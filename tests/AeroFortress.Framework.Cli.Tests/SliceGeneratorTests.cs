using AeroFortress.Framework.Cli;

namespace AeroFortress.Framework.Cli.Tests;

public class SliceGeneratorTests
{
    [Fact]
    public void Generates_a_slice_in_the_canonical_shape()
    {
        var root = NewProject("Shop.Api");

        var code = SliceGenerator.Generate(root, "Orders", "Place");

        Assert.Equal(0, code);
        var slice = File.ReadAllText(Path.Combine(root, "Modules", "Orders", "Slices", "Place.cs"));
        Assert.Contains("namespace Shop.Api.Modules.Orders;", slice);
        Assert.Contains("[Slice]", slice);
        Assert.Contains("public static class Place", slice);
        Assert.Contains("public record Input", slice);
        Assert.Contains("public record Output", slice);
        Assert.Contains("Task<Result<Output>> Handle", slice);
        Assert.Contains("void Map(", slice);

        // The endpoint is named after the slice (AF0012) — the operationId the typed client hooks from.
        Assert.Contains(".WithName(nameof(Place))", slice);

        // The scaffolded validation uses the accumulator's sugar with a registry constant (AF0018), and the
        // module's registry is created with it.
        Assert.Contains(".Require(input.Id, \"id\", OrdersErrorCodes.IdRequired)", slice);
        var registry = File.ReadAllText(Path.Combine(root, "Modules", "Orders", "OrdersErrorCodes.cs"));
        Assert.Contains("public const string IdRequired = \"id.required\";", registry);

        // Canonical order: Input → Output → Handle → Map, the same order AF0001 enforces.
        Assert.True(slice.IndexOf("record Input", StringComparison.Ordinal)
                  < slice.IndexOf("record Output", StringComparison.Ordinal));
        Assert.True(slice.IndexOf("record Output", StringComparison.Ordinal)
                  < slice.IndexOf("Handle", StringComparison.Ordinal));
        Assert.True(slice.IndexOf("Handle", StringComparison.Ordinal)
                  < slice.IndexOf("void Map", StringComparison.Ordinal));
    }

    [Fact]
    public void Generates_a_co_located_unit_test()
    {
        var root = NewProject("Shop.Api");

        SliceGenerator.Generate(root, "Orders", "Place");

        var test = File.ReadAllText(Path.Combine(root, "Modules", "Orders", "Slices", "Place.Tests.cs"));
        Assert.Contains("namespace Shop.Tests.Modules.Orders;", test);
        Assert.Contains("using Shop.Api.Modules.Orders;", test);
        Assert.Contains("[Unit]", test);
        Assert.Contains("public class PlaceTests", test);
    }

    [Fact]
    public void Refuses_to_overwrite_an_existing_slice()
    {
        var root = NewProject("Shop.Api");

        Assert.Equal(0, SliceGenerator.Generate(root, "Orders", "Place"));
        Assert.Equal(1, SliceGenerator.Generate(root, "Orders", "Place"));
    }

    [Fact]
    public void A_plain_slice_has_no_critical_marker_or_journeys()
    {
        var root = NewProject("Shop.Api");

        SliceGenerator.Generate(root, "Orders", "Place");

        var slice = File.ReadAllText(Path.Combine(root, "Modules", "Orders", "Slices", "Place.cs"));
        Assert.DoesNotContain("[Critical]", slice);
        Assert.False(Directory.Exists(Path.Combine(root, "Journeys")));
    }

    [Fact]
    public void A_critical_slice_is_marked_and_gets_happy_and_sad_journeys()
    {
        var root = NewProject("Shop.Api");

        SliceGenerator.Generate(root, "Orders", "Place", critical: true);

        var slice = File.ReadAllText(Path.Combine(root, "Modules", "Orders", "Slices", "Place.cs"));
        Assert.Contains("[Critical]", slice);

        var journey = File.ReadAllText(Path.Combine(root, "Journeys", "PlaceJourney.Tests.cs"));
        Assert.Contains("namespace Shop.Tests.Journeys;", journey);
        Assert.Contains("[Journey(typeof(Place), JourneyPath.Happy)]", journey);
        Assert.Contains("[Journey(typeof(Place), JourneyPath.Sad)]", journey);
    }

    private static string NewProject(string name)
    {
        var root = Directory.CreateTempSubdirectory("aerofortress-framework-cli-test").FullName;
        File.WriteAllText(Path.Combine(root, name + ".csproj"), "<Project />");
        return root;
    }
}
