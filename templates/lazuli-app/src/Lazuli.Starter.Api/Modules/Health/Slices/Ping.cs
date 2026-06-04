namespace Lazuli.Starter.Api.Modules.Health;

/// <summary>Ping — a liveness slice; replace it with your first real feature (`lazuli g slice`).</summary>
[Slice]
public static class Ping
{
    public record Input(string Message);
    public record Output(string Message);

    public static Task<Result<Output>> Handle(Input input, CancellationToken ct) =>
        Task.FromResult<Result<Output>>(new Output(input.Message));

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/ping", async (Input input, CancellationToken ct) =>
            (await Handle(input, ct)).ToHttp());
}
