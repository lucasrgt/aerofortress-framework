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

// Seed a wallet so the sample has something to read on first run.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Wallets.Add(Wallet.Open(Guid.Parse("11111111-1111-1111-1111-111111111111")).Value);
    db.SaveChanges();
}

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the real app in integration/journey tests.
public partial class Program { }
