using Lazuli.Starter.Api.Modules.Health;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Composition root: each module registers its own routes. Add modules as you generate them.
HealthModule.Map(app);

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the real app in integration/journey tests.
public partial class Program { }
