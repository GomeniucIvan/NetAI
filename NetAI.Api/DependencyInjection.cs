using System;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetAI.Api.Application;
using NetAI.Api.Authentication;
using NetAI.Api.Data;
using NetAI.Api.Data.Repositories;
using NetAI.Api.Services.Configuration;
using NetAI.Api.Services.Conversations;
using NetAI.Api.Services.Events;
using NetAI.Api.Services.Diagnostics;
using NetAI.Api.Services.Experiments;
using NetAI.Api.Services.Git;
using NetAI.Api.Services.EventCallbacks;
using NetAI.Api.Services.Installation;
using NetAI.Api.Services.Keys;
using NetAI.Api.Services.Sandboxes;
using NetAI.Api.Services.Secrets;
using NetAI.Api.Services.Settings;
using NetAI.Api.Services.Security;
using NetAI.Api.Services.Http;
using NetAI.Api.Services.Webhooks;
using NetAI.Api.Services.WebSockets;

namespace NetAI.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddNetAiServices(this IServiceCollection services, IConfiguration configuration = null)
    {
        if (configuration is not null)
        {
            services.TryAddSingleton<IConfiguration>(configuration);
        }
        else
        {
            services.TryAddSingleton<IConfiguration>(_ => new ConfigurationBuilder().AddEnvironmentVariables().Build());
        }

        OptionsBuilder<DatabaseOptions> databaseOptionsBuilder = services.AddOptions<DatabaseOptions>();
        if (configuration is not null)
        {
            databaseOptionsBuilder.Bind(configuration.GetSection("Database"));
        }

        databaseOptionsBuilder.PostConfigure<IHostEnvironment, ILogger<DatabaseOptions>>((options, hostEnvironment, logger) =>
        {
            if (string.IsNullOrWhiteSpace(options.Provider))
            {
                options.Provider = "Postgres";
            }

            if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                return;
            }

            string configurationPath = Path.Combine(hostEnvironment.ContentRootPath, "Data", "settings", "connection.json");
            if (!File.Exists(configurationPath))
            {
                logger.LogInformation(
                    "Database connection configuration file was not found at {ConfigurationPath}. Installation is required before the database can be used.",
                    configurationPath);
                return;
            }

            try
            {
                using FileStream stream = File.OpenRead(configurationPath);
                using JsonDocument document = JsonDocument.Parse(stream);
                if (TryReadConnectionString(document.RootElement, out string connectionString))
                {
                    options.ConnectionString = connectionString;
                }
                else
                {
                    logger.LogWarning(
                        "Database connection configuration file at {ConfigurationPath} does not contain a connection string.",
                        configurationPath);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read database configuration from {ConfigurationPath}", configurationPath);
            }
        });

        services.AddDbContextPool<NetAiDbContext>((serviceProvider, builder) =>
        {
            DatabaseOptions options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            string provider = options.Provider?.Trim() ?? string.Empty;
            string migrationsAssembly = typeof(NetAiDbContext).Assembly.GetName().Name
                                        ?? throw new InvalidOperationException("Unable to determine migrations assembly for NetAiDbContext.");

            if (!string.Equals(provider, "postgres", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(provider, "postgresql", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(provider, "npgsql", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"Unsupported database provider '{options.Provider}'. Only PostgreSQL is supported.");
            }

            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                ILogger<NetAiDbContext> logger = serviceProvider.GetRequiredService<ILogger<NetAiDbContext>>();
                logger.LogWarning("No PostgreSQL connection string is configured. NetAI will run without database persistence until installation is completed.");
                return;
            }

            builder.UseNpgsql(options.ConnectionString, npgsql =>
                npgsql.MigrationsAssembly(migrationsAssembly));
        });

        OptionsBuilder<ConversationFilterOptions> conversationFilterOptions = services.AddOptions<ConversationFilterOptions>();
        if (configuration is not null)
        {
            conversationFilterOptions.Bind(configuration.GetSection("Conversations"));
        }

        OptionsBuilder<ConversationRepositoryOptions> conversationRepositoryOptions = services.AddOptions<ConversationRepositoryOptions>();
        if (configuration is not null)
        {
            conversationRepositoryOptions.Bind(configuration.GetSection("Conversations:Repository"));
        }

        OptionsBuilder<RuntimeConversationGatewayOptions> runtimeGatewayOptions = services.AddOptions<RuntimeConversationGatewayOptions>();
        if (configuration is not null)
        {
            runtimeGatewayOptions.Bind(configuration.GetSection("Conversations:RuntimeGateway"));
        }

        services.AddSingleton<IHttpClientSelector, HttpClientSelector>();
        services.AddHttpClient(HttpClientSelector.RuntimeClientName, (sp, client) =>
        {
            RuntimeConversationGatewayOptions options = sp
                .GetRequiredService<IOptions<RuntimeConversationGatewayOptions>>().Value;

            if (!string.IsNullOrWhiteSpace(options.BaseUrl)
                && Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri baseUri))
            {
                client.BaseAddress = baseUri;
            }
        });
        services.AddHttpClient(HttpClientSelector.ApiClientName);
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddHttpClient(ConversationRepository.HttpClientName, (sp, client) =>
        {
            ConversationRepositoryOptions options = sp
                .GetRequiredService<IOptions<ConversationRepositoryOptions>>().Value;

            if (!string.IsNullOrWhiteSpace(options.BaseUrl)
                && Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri baseUri))
            {
                client.BaseAddress = baseUri;
            }
        });
        services.AddScoped<IExperimentConfigRepository, ExperimentConfigRepository>();
        services.AddScoped<IConversationStartTaskRepository, ConversationStartTaskRepository>();
        services.AddScoped<IAppConversationInfoRepository, AppConversationInfoRepository>();
        services.AddScoped<ISandboxRepository, SandboxRepository>();
        services.AddScoped<ISandboxSpecRepository, SandboxSpecRepository>();
        services.AddScoped<IEventCallbackRepository, EventCallbackRepository>();
        services.AddScoped<IEventCallbackResultRepository, EventCallbackResultRepository>();
        services.AddScoped<IRuntimeConversationGateway, RuntimeConversationGateway>();
        services.AddScoped<IRuntimeConversationClient, RuntimeConversationClient>();
        services.AddScoped<IConversationSessionService, ConversationSessionService>();
        services.AddScoped<IExperimentConfigService, ExperimentConfigService>();
        services.AddScoped<IPullRequestStatusService, PullRequestStatusService>();
        services.AddScoped<IMicroagentManagementService, MicroagentManagementService>();
        services.AddScoped<AppConversationStartService>();
        services.AddScoped<IAppConversationStartService>(sp => sp.GetRequiredService<AppConversationStartService>());
        services.AddScoped<IAppConversationInfoService, AppConversationInfoService>();
        services.AddSingleton<IConversationEventNotifier, ConversationEventNotifier>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<ConversationSocketIoHandler>();
        services.AddScoped<ConversationEventsWebSocketHandler>();
        services.AddScoped<IWebhookValidator, WebhookValidator>();
        services.AddScoped<IConversationWebhookService, ConversationWebhookService>();
        services.AddScoped<IEventCallbackDispatcher, SequentialEventCallbackDispatcher>();
        services.AddScoped<IEventCallbackManagementService, EventCallbackManagementService>();
        services.AddSingleton<SystemStatusService>();
        services.AddSingleton<IApplicationContext, ApplicationContext>();
        OptionsBuilder<DefaultSandboxSpecOptions> defaultSpecOptions = services.AddOptions<DefaultSandboxSpecOptions>();
        if (configuration is not null)
        {
            defaultSpecOptions.Bind(configuration.GetSection("SandboxSpecs:Default"));
        }

        services.AddScoped<ISandboxSpecService, SandboxSpecService>();
        services.AddScoped<ISandboxService, SandboxService>();
        OptionsBuilder<SandboxOrchestrationOptions> sandboxOrchestrationOptions = services.AddOptions<SandboxOrchestrationOptions>();
        if (configuration is not null)
        {
            sandboxOrchestrationOptions.Bind(configuration.GetSection("SandboxOrchestration"));
        }
        services.AddHttpClient<ISandboxOrchestrationClient, SandboxOrchestrationClient>();
        services.AddHttpClient();
        services.AddSingleton<IMicroagentContentClient, LocalMicroagentContentClient>();
        services.AddSingleton<ISystemInfoProvider, SystemInfoProvider>();
        OptionsBuilder<GitProviderOptions> gitProviderOptions = services.AddOptions<GitProviderOptions>();
        if (configuration is not null)
        {
            gitProviderOptions.Bind(configuration.GetSection("GitProviders"));
        }
        services.AddSingleton<IGitHubClientFactory, GitHubClientFactory>();
        services.AddSingleton<IMicroagentContentClient, GitHubMicroagentContentClient>();
        services.AddSingleton<IGitIntegrationService, GitIntegrationService>();
        services.AddScoped<IMcpGitService, McpGitService>();
        services.AddSingleton<ISecretsStore>(sp =>
        {
            ILogger<JsonFileSecretsStore> logger = sp.GetRequiredService<ILogger<JsonFileSecretsStore>>();
            IConfiguration cfg = sp.GetService<IConfiguration>();
            return new JsonFileSecretsStore(logger, cfg);
        });
        services.AddSingleton<IProviderTokenValidator, ProviderTokenValidator>();
        services.AddSingleton<ISecretsService, SecretsService>();
        services.AddSingleton<ISettingsStore>(sp =>
        {
            ILogger<JsonFileSettingsStore> logger = sp.GetRequiredService<ILogger<JsonFileSettingsStore>>();
            IConfiguration cfg = sp.GetService<IConfiguration>();
            return new JsonFileSettingsStore(logger, cfg);
        });
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IOptionsMetadataService, OptionsMetadataService>();
        services.AddSingleton<IApiKeyStore, InMemoryApiKeyStore>();
        services.AddSingleton<IApiKeyService, ApiKeyService>();
        services.AddSingleton<ISecurityStateStore, JsonFileSecurityStateStore>();
        services.AddScoped<ISecurityService, SecurityService>();
        services.AddSingleton<IDatabaseConfigurationStore, JsonFileDatabaseConfigurationStore>();
        services.AddSingleton<IInstallationService, InstallationService>();
        services.AddSingleton<ConversationStartTaskQueue>();
        services.AddSingleton<ConversationStartTaskNotifier>();
        services.AddHostedService<ConversationStartTaskWorker>();

        OptionsBuilder<AccessTokenValidationOptions> accessTokenOptions = services.AddOptions<AccessTokenValidationOptions>();
        if (configuration is not null)
        {
            accessTokenOptions.Bind(configuration.GetSection("Security:AccessToken"));
        }

        services.AddOptions<ConversationStartTaskOptions>();

        services
            .AddAuthentication()
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme,
                static _ => { });

        services.AddAuthorization();
        return services;
    }

    private static bool TryReadConnectionString(JsonElement rootElement, out string connectionString)
    {
        connectionString = null;

        if (rootElement.ValueKind == JsonValueKind.String)
        {
            connectionString = rootElement.GetString();
            return !string.IsNullOrWhiteSpace(connectionString);
        }

        if (rootElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (rootElement.TryGetProperty("ConnectionStrings", out JsonElement connectionStrings)
            && connectionStrings.ValueKind == JsonValueKind.Object
            && connectionStrings.TryGetProperty("Default", out JsonElement defaultConnection)
            && !string.IsNullOrWhiteSpace(defaultConnection.GetString()))
        {
            connectionString = defaultConnection.GetString();
            return true;
        }

        return false;
    }
}
