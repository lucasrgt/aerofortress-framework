using Microsoft.EntityFrameworkCore;
using Sample.Api;
using Sample.Api.Modules.Wallets;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDb>(o => o.UseInMemoryDatabase("sample"));

var app = builder.Build();

// Composition root: wire DI, let each slice register its own route, seed, run. Zero logic.
WalletsModule.Map(app);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Wallets.Add(Wallet.Open(Guid.Parse("11111111-1111-1111-1111-111111111111")).Value);
    db.SaveChanges();
}

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the real app in integration/journey tests.
public partial class Program { }
