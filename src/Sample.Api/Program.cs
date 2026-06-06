using Lazuli.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Sample.Api;
using Sample.Api.Modules;
using Sample.Api.Modules.Wallets;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDb>(o => o.UseInMemoryDatabase("sample"));
builder.Services.AddLazuli();                        // framework conventions: slice-aware OpenAPI + enum-as-name JSON
builder.Services.AddModules(builder.Configuration);  // each module's own services (the explicit registry)

var app = builder.Build();

app.UseLazuli();    // serve the OpenAPI contract at /openapi/v1.json
app.MapModules();   // each module's routes (the explicit registry)

// Boot: seed demo data so the sample reads non-empty on first run. The composition root only orchestrates the
// scope + order; the seed content lives in the module that owns it (WalletsModule.Seed).
using (var scope = app.Services.CreateScope())
    WalletsModule.Seed(scope.ServiceProvider.GetRequiredService<AppDb>());

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the real app in integration/journey tests.
public partial class Program { }
