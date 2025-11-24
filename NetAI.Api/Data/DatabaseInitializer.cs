using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetAI.Api.Services.Installation;

namespace NetAI.Api.Data;

//TODO ISetting

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        ILoggerFactory loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        ILogger logger = loggerFactory.CreateLogger("DatabaseInitializer");
        DatabaseOptions databaseOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (!IsPostgresProvider(databaseOptions.Provider))
        {
            logger.LogWarning(
                "Database provider '{Provider}' is not supported. Skipping database migrations and seed operations.",
                databaseOptions.Provider);
            return;
        }

        string connectionString = databaseOptions.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            IDatabaseConfigurationStore store = scope.ServiceProvider
                .GetService<IDatabaseConfigurationStore>();
            if (store is not null)
            {
                connectionString = await store.LoadConnectionStringAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning(
                "Database provider is configured but no connection string is available. Skipping database migrations and seed operations.");
            return;
        }

        var optionsBuilder = new DbContextOptionsBuilder<NetAiDbContext>();
        optionsBuilder.UseLoggerFactory(loggerFactory);
        string migrationsAssembly = typeof(NetAiDbContext).Assembly.GetName().Name
            ?? throw new InvalidOperationException("Unable to determine migrations assembly for NetAiDbContext.");

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(migrationsAssembly));

        await using NetAiDbContext dbContext = new(optionsBuilder.Options);
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await SeedAsync(dbContext, logger, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SeedAsync(NetAiDbContext dbContext, ILogger logger, CancellationToken cancellationToken)
    {
        if (await dbContext.Conversations.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        logger.LogInformation("Database initialized with zero conversations. Seeding skipped.");
    }

    private static bool IsPostgresProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return true;
        }

        return string.Equals(provider, "postgres", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "postgresql", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "npgsql", StringComparison.OrdinalIgnoreCase);
    }
}
