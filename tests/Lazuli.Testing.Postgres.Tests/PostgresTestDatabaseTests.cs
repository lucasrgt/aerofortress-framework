using Lazuli.Testing.Postgres;

namespace Lazuli.Testing.Postgres.Tests;

// The container-and-clone path needs Docker, so it is exercised by the pilots (the hostpoint suite runs it on
// every CI pass). What is pinned here is the connection-string derivation — the part whose regression would be
// silent: a clone that keeps pooling on exhausts the server's slots mid-suite, hundreds of tests in.
public class PostgresTestDatabaseTests
{
    private const string Maintenance = "Host=localhost;Port=55432;Database=postgres;Username=postgres;Password=postgres";

    [Fact]
    public void A_clone_connection_targets_the_clone_with_pooling_off()
    {
        var connection = PostgresTestDatabase.IsolatedConnectionString(Maintenance, "t_abc");

        Assert.Contains("Database=t_abc", connection);
        Assert.Contains("Pooling=False", connection);
    }

    [Fact]
    public void A_clone_connection_keeps_the_containers_host_and_credentials()
    {
        var connection = PostgresTestDatabase.IsolatedConnectionString(Maintenance, "t_abc");

        Assert.Contains("Port=55432", connection);
        Assert.Contains("Username=postgres", connection);
    }
}
