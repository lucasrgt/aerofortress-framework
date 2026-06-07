using Sample.Api;
using Microsoft.Extensions.DependencyInjection;
using Sample.Api.Modules.Wallets;

namespace Sample.Tests;

/// <summary>
/// Boots the sample app for integration and journey tests. It takes the in-memory fast path; the
/// framework's <see cref="LazuliWebTest{TProgram}"/> owns the boot and hands over the swap hook.
/// </summary>
public sealed class TestApp : LazuliWebTest<Program>
{
    /// <inheritdoc />
    protected override void SwapStores(IServiceCollection services) =>
        services.UseIsolatedInMemory<AppDb>();
}
