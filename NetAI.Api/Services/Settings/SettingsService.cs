using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Models.Settings;
using NetAI.Api.Services.Secrets;

namespace NetAI.Api.Services.Settings;

public class SettingsService : ISettingsService
{
    private static readonly IReadOnlyDictionary<ProviderType, string> ProviderKeyMap = new Dictionary<ProviderType, string>
    {
        [ProviderType.Github] = "github",
        [ProviderType.Gitlab] = "gitlab",
        [ProviderType.Bitbucket] = "bitbucket",
        [ProviderType.EnterpriseSso] = "enterprise_sso"
    };

    private readonly ISettingsStore _settingsStore;
    private readonly ISecretsStore _secretsStore;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(ISettingsStore settingsStore, ISecretsStore secretsStore, ILogger<SettingsService> logger)
    {
        _settingsStore = settingsStore;
        _secretsStore = secretsStore;
        _logger = logger;
    }

    public async Task<SettingsQueryResult<ApiSettingsDto>> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            StoredSettings settings = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (settings is null)
            {
                return SettingsQueryResult<ApiSettingsDto>.Failure(StatusCodes.Status404NotFound, "Settings not found");
            }

            UserSecrets secrets = await _secretsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            (StoredSettings processedSettings, UserSecrets migratedSecrets) = await InvalidateLegacySecretsAsync(settings, secrets, cancellationToken).ConfigureAwait(false);

            if (migratedSecrets is not null)
            {
                secrets = migratedSecrets;
            }

            settings = processedSettings;
            ApiSettingsDto response = BuildResponse(settings, secrets);
            return SettingsQueryResult<ApiSettingsDto>.SuccessResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings");
            return SettingsQueryResult<ApiSettingsDto>.Failure(StatusCodes.Status500InternalServerError, "Something went wrong loading settings");
        }
    }

    public async Task<SettingsOperationResult> StoreSettingsAsync(UpdateSettingsRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return SettingsOperationResult.Failure(StatusCodes.Status400BadRequest, "Request body is required");
        }

        if (request.CondenserMaxSize.HasValue && request.CondenserMaxSize.Value < 20)
        {
            return SettingsOperationResult.Failure(StatusCodes.Status400BadRequest, "condenser_max_size must be at least 20");
        }

        try
        {
            StoredSettings existing = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false) ?? new StoredSettings();
            StoredSettings merged = MergeSettings(existing, request);
            ApplyRuntimeConfiguration(request, merged);
            await _settingsStore.StoreAsync(merged, cancellationToken).ConfigureAwait(false);
            return SettingsOperationResult.SuccessResult(StatusCodes.Status200OK, "Settings stored");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store settings");
            return SettingsOperationResult.Failure(StatusCodes.Status500InternalServerError, "Something went wrong storing settings");
        }
    }

    private async Task<(StoredSettings UpdatedSettings, UserSecrets MigratedSecrets)> InvalidateLegacySecretsAsync(
        StoredSettings settings,
        UserSecrets existingSecrets,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LegacySecretsStore legacy = settings.SecretsStore;
        if (legacy?.ProviderTokens is null || legacy.ProviderTokens.Count == 0)
        {
            return (settings, null);
        }

        var converted = new Dictionary<ProviderType, ProviderTokenInfo>();
        foreach ((string providerKey, LegacyProviderToken legacyToken) in legacy.ProviderTokens)
        {
            if (legacyToken is null)
            {
                continue;
            }

            if (!TryParseLegacyProviderKey(providerKey, out ProviderType providerType))
            {
                continue;
            }

            string tokenValue = Normalize(legacyToken.Token);
            string hostValue = Normalize(legacyToken.Host);

            if (string.IsNullOrEmpty(tokenValue) && string.IsNullOrEmpty(hostValue))
            {
                continue;
            }

            converted[providerType] = new ProviderTokenInfo(tokenValue, hostValue);
        }

        if (converted.Count == 0)
        {
            LegacySecretsStore clearedLegacyInner = CreateClearedLegacy(legacy);
            StoredSettings clearedSettings = settings with { SecretsStore = clearedLegacyInner };
            await _settingsStore.StoreAsync(clearedSettings, cancellationToken).ConfigureAwait(false);

            return (clearedSettings, existingSecrets);
        }

        UserSecrets workingSecrets = existingSecrets?.Clone() ?? UserSecrets.Empty.Clone();
        var mergedTokens = new Dictionary<ProviderType, ProviderTokenInfo>(workingSecrets.ProviderTokens);
        foreach ((ProviderType provider, ProviderTokenInfo info) in converted)
        {
            mergedTokens[provider] = info;
        }

        var updatedSecrets = new UserSecrets(mergedTokens, workingSecrets.CustomSecrets);
        await _secretsStore.StoreAsync(updatedSecrets, cancellationToken).ConfigureAwait(false);

        LegacySecretsStore clearedLegacy = CreateClearedLegacy(legacy);
        StoredSettings updatedSettings = settings with { SecretsStore = clearedLegacy };
        await _settingsStore.StoreAsync(updatedSettings, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Migrated legacy provider tokens from settings store to secrets store.");

        return (updatedSettings, updatedSecrets);
    }

    private void ApplyRuntimeConfiguration(UpdateSettingsRequestDto request, StoredSettings merged)
    {
        if (request.RemoteRuntimeResourceFactor.HasValue)
        {
            OpenHandsConfigurationBridge.UpdateRuntimeResourceFactor(merged.RemoteRuntimeResourceFactor);
            _logger.LogInformation("Updated remote runtime resource factor to {RemoteRuntimeResourceFactor}", merged.RemoteRuntimeResourceFactor);
        }

        if (request.GitUserName is not null || request.GitUserEmail is not null)
        {
            OpenHandsConfigurationBridge.UpdateGitConfiguration(merged.GitUserName, merged.GitUserEmail);
            _logger.LogInformation("Updated global git configuration: name={GitUserName}, email={GitUserEmail}", merged.GitUserName, merged.GitUserEmail);
        }
    }

    private static ApiSettingsDto BuildResponse(StoredSettings settings, UserSecrets secrets)
    {
        var providerTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (secrets?.ProviderTokens is not null)
        {
            foreach ((ProviderType provider, ProviderTokenInfo token) in secrets.ProviderTokens)
            {
                if (token is null)
                {
                    continue;
                }

                string key = ProviderKeyMap.TryGetValue(provider, out string value)
                    ? value
                    : provider.ToString().ToLowerInvariant();
                if (!string.IsNullOrEmpty(token.Token) || !string.IsNullOrEmpty(token.Host))
                {
                    providerTokens[key] = Normalize(token.Host);
                }
            }
        }

        return new ApiSettingsDto
        {
            Language = settings.Language,
            Agent = settings.Agent,
            MaxIterations = settings.MaxIterations,
            SecurityAnalyzer = settings.SecurityAnalyzer,
            ConfirmationMode = settings.ConfirmationMode ?? false,
            LlmModel = settings.LlmModel,
            LlmApiKey = null,
            LlmBaseUrl = settings.LlmBaseUrl,
            RemoteRuntimeResourceFactor = settings.RemoteRuntimeResourceFactor,
            EnableDefaultCondenser = settings.EnableDefaultCondenser,
            CondenserMaxSize = settings.CondenserMaxSize,
            EnableSoundNotifications = settings.EnableSoundNotifications,
            EnableProactiveConversationStarters = settings.EnableProactiveConversationStarters,
            EnableSolvabilityAnalysis = settings.EnableSolvabilityAnalysis,
            UserConsentsToAnalytics = settings.UserConsentsToAnalytics,
            SandboxBaseContainerImage = settings.SandboxBaseContainerImage,
            SandboxRuntimeContainerImage = settings.SandboxRuntimeContainerImage,
            McpConfig = settings.McpConfig?.Clone(),
            SearchApiKey = null,
            SandboxApiKey = null,
            LlmApiKeySet = HasSecret(settings.LlmApiKey),
            SearchApiKeySet = HasSecret(settings.SearchApiKey),
            ProviderTokensSet = providerTokens,
            MaxBudgetPerTask = settings.MaxBudgetPerTask,
            Email = settings.Email,
            EmailVerified = settings.EmailVerified,
            GitUserName = settings.GitUserName,
            GitUserEmail = settings.GitUserEmail,
            IsNewUser = false
        };
    }

    private static StoredSettings MergeSettings(StoredSettings existing, UpdateSettingsRequestDto request)
    {
        StoredSettings working = existing.Copy();

        if (request.Language is not null)
        {
            working = working with { Language = Normalize(request.Language) };
        }

        if (request.Agent is not null)
        {
            working = working with { Agent = Normalize(request.Agent) };
        }

        if (request.MaxIterations.HasValue)
        {
            working = working with { MaxIterations = request.MaxIterations };
        }

        if (request.SecurityAnalyzer is not null)
        {
            working = working with { SecurityAnalyzer = Normalize(request.SecurityAnalyzer) };
        }

        if (request.ConfirmationMode.HasValue)
        {
            working = working with { ConfirmationMode = request.ConfirmationMode.Value };
        }

        if (request.LlmModel is not null)
        {
            working = working with { LlmModel = Normalize(request.LlmModel) };
        }

        if (request.LlmBaseUrl is not null)
        {
            working = working with { LlmBaseUrl = Normalize(request.LlmBaseUrl) };
        }

        if (request.RemoteRuntimeResourceFactor.HasValue)
        {
            working = working with { RemoteRuntimeResourceFactor = request.RemoteRuntimeResourceFactor };
        }

        if (request.EnableDefaultCondenser.HasValue)
        {
            working = working with { EnableDefaultCondenser = request.EnableDefaultCondenser.Value };
        }

        if (request.CondenserMaxSize.HasValue)
        {
            working = working with { CondenserMaxSize = request.CondenserMaxSize };
        }

        if (request.EnableSoundNotifications.HasValue)
        {
            working = working with { EnableSoundNotifications = request.EnableSoundNotifications.Value };
        }

        if (request.EnableProactiveConversationStarters.HasValue)
        {
            working = working with { EnableProactiveConversationStarters = request.EnableProactiveConversationStarters.Value };
        }

        if (request.EnableSolvabilityAnalysis.HasValue)
        {
            working = working with { EnableSolvabilityAnalysis = request.EnableSolvabilityAnalysis.Value };
        }

        if (request.UserConsentsToAnalytics.HasValue)
        {
            working = working with { UserConsentsToAnalytics = request.UserConsentsToAnalytics.Value };
        }

        if (request.SandboxBaseContainerImage is not null)
        {
            working = working with { SandboxBaseContainerImage = Normalize(request.SandboxBaseContainerImage) };
        }

        if (request.SandboxRuntimeContainerImage is not null)
        {
            working = working with { SandboxRuntimeContainerImage = Normalize(request.SandboxRuntimeContainerImage) };
        }

        if (request.McpConfig is not null)
        {
            working = working with { McpConfig = request.McpConfig.Clone() };
        }

        if (request.MaxBudgetPerTask.HasValue)
        {
            working = working with { MaxBudgetPerTask = request.MaxBudgetPerTask };
        }

        if (request.Email is not null)
        {
            working = working with { Email = Normalize(request.Email) };
        }

        if (request.EmailVerified.HasValue)
        {
            working = working with { EmailVerified = request.EmailVerified.Value };
        }

        if (request.GitUserName is not null)
        {
            working = working with { GitUserName = Normalize(request.GitUserName) };
        }

        if (request.GitUserEmail is not null)
        {
            working = working with { GitUserEmail = Normalize(request.GitUserEmail) };
        }

        if (request.LlmApiKey is not null)
        {
            working = working with { LlmApiKey = Normalize(request.LlmApiKey) };
        }

        if (request.SearchApiKey is not null)
        {
            working = working with { SearchApiKey = Normalize(request.SearchApiKey) };
        }

        if (request.SandboxApiKey is not null)
        {
            working = working with { SandboxApiKey = Normalize(request.SandboxApiKey) };
        }

        return working;
    }

    private static LegacySecretsStore CreateClearedLegacy(LegacySecretsStore legacy)
    {
        LegacySecretsStore copy = legacy.Copy();
        return copy with
        {
            ProviderTokens = new Dictionary<string, LegacyProviderToken>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static bool HasSecret(string value)
        => !string.IsNullOrWhiteSpace(value);

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseLegacyProviderKey(string providerKey, out ProviderType providerType)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            providerType = default;
            return false;
        }

        foreach ((ProviderType provider, string value) in ProviderKeyMap)
        {
            if (string.Equals(value, providerKey, StringComparison.OrdinalIgnoreCase))
            {
                providerType = provider;
                return true;
            }
        }

        return Enum.TryParse(providerKey, true, out providerType);
    }
}
