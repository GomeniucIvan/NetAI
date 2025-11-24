using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NetAI.Api.Data;

public class NetAiDbContextFactory : IDesignTimeDbContextFactory<NetAiDbContext>
{
    private const string DefaultConnectionString = "";

    public NetAiDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("NETAI_DB_CONNECTION") ?? DefaultConnectionString;
        string migrationsAssembly = typeof(NetAiDbContext).Assembly.GetName().Name
            ?? throw new InvalidOperationException("Unable to determine migrations assembly for NetAiDbContext.");

        DbContextOptionsBuilder<NetAiDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(migrationsAssembly));

        return new NetAiDbContext(optionsBuilder.Options);
    }
}
