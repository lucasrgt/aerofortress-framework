namespace Lazuli.Doctor.Tests;

public class CompositionRootAnalyzerTests
{
    [Fact]
    public Task Index_with_only_the_blessed_calls_reports_nothing() =>
        Harness<CompositionRootAnalyzer>.VerifyProgram(Program(""));

    [Fact]
    public Task Service_registration_in_the_index_is_flagged() =>
        Harness<CompositionRootAnalyzer>.VerifyProgram(Program("services.{|LZ0017:AddDbContext|}();"));

    [Fact]
    public Task Pipeline_step_in_the_index_is_flagged() =>
        Harness<CompositionRootAnalyzer>.VerifyProgram(Program("app.{|LZ0017:UseCors|}();"));

    [Fact]
    public Task Endpoint_mapping_in_the_index_is_flagged() =>
        Harness<CompositionRootAnalyzer>.VerifyProgram(Program("app.{|LZ0017:MapControllers|}();"));

    // A composition root: the three blessed Add* layers, an optional extra statement, the matching Use*/Map*,
    // and Run(). app.Run() proves a non-Use/Map call on the app is left alone; the stubs below mirror the real
    // receiver types (IServiceCollection for registration, IApplicationBuilder / IEndpointRouteBuilder for
    // pipeline + endpoints) so the rule classifies them exactly as it would the framework's.
    private static string Program(string extra) => $$"""
        using Stubs;

        IServiceCollection services = null!;
        WebApplication app = null!;

        services.AddLazuli();
        services.AddPlatform();
        services.AddModules();
        {{extra}}
        app.UseLazuli();
        app.UsePlatform();
        app.MapModules();
        app.Run();

        namespace Stubs
        {
            public interface IServiceCollection { }
            public interface IApplicationBuilder { }
            public interface IEndpointRouteBuilder { }
            public class WebApplication : IApplicationBuilder, IEndpointRouteBuilder { public void Run() { } }

            public static class Wiring
            {
                public static IServiceCollection AddLazuli(this IServiceCollection s) => s;
                public static IServiceCollection AddPlatform(this IServiceCollection s) => s;
                public static IServiceCollection AddModules(this IServiceCollection s) => s;
                public static WebApplication UseLazuli(this WebApplication a) => a;
                public static WebApplication UsePlatform(this WebApplication a) => a;
                public static WebApplication MapModules(this WebApplication a) => a;

                public static IServiceCollection AddDbContext(this IServiceCollection s) => s;
                public static IApplicationBuilder UseCors(this IApplicationBuilder a) => a;
                public static IEndpointRouteBuilder MapControllers(this IEndpointRouteBuilder a) => a;
            }
        }
        """;
}
