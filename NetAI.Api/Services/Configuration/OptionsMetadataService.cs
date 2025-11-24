using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.Configuration;
using NetAI.Api.Models.Configuration;

namespace NetAI.Api.Services.Configuration;

public class OptionsMetadataService : IOptionsMetadataService
{
    private readonly IConfiguration _configuration;

    public OptionsMetadataService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IReadOnlyList<string> GetModels()
        => GetConfiguredStrings(
            new[] { "Options:Models" },
            new[] { "OPTIONS_MODELS", "LLM_MODELS" })
            ?? OptionsDefaults.Models;

    public IReadOnlyList<string> GetAgents()
        => GetConfiguredStrings(
            new[] { "Options:Agents" },
            new[] { "OPTIONS_AGENTS", "AGENTS" })
            ?? OptionsDefaults.Agents;

    public IReadOnlyList<string> GetSecurityAnalyzers()
        => GetConfiguredStrings(
            new[] { "Options:SecurityAnalyzers" },
            new[] { "OPTIONS_SECURITY_ANALYZERS", "SECURITY_ANALYZERS" })
            ?? OptionsDefaults.SecurityAnalyzers;

    public GetConfigResponseDto GetConfig()
    {
        IReadOnlyList<ProviderOption> providers = GetProviders();
        MaintenanceWindowDto maintenance = GetMaintenanceWindow();

        return new GetConfigResponseDto
        {
            AppMode = GetAppMode(),
            AppSlug = Normalize(_configuration["APP_SLUG"] ?? _configuration["Options:Config:AppSlug"]),
            GithubClientId = NormalizeOrEmpty(_configuration["GITHUB_CLIENT_ID"] ?? _configuration["Options:Config:GithubClientId"]),
            PosthogClientKey = NormalizeOrEmpty(_configuration["POSTHOG_CLIENT_KEY"] ?? _configuration["Options:Config:PosthogClientKey"]),
            AuthUrl = Normalize(_configuration["AUTH_URL"] ?? _configuration["Options:Config:AuthUrl"]),
            FeatureFlags = new FeatureFlagsDto
            {
                EnableBilling = GetBoolean("ENABLE_BILLING", "Options:FeatureFlags:EnableBilling"),
                HideLlmSettings = GetBoolean("HIDE_LLM_SETTINGS", "Options:FeatureFlags:HideLlmSettings"),
                EnableJira = GetBoolean("ENABLE_JIRA", "Options:FeatureFlags:EnableJira"),
                EnableJiraDc = GetBoolean("ENABLE_JIRA_DC", "Options:FeatureFlags:EnableJiraDc"),
                EnableLinear = GetBoolean("ENABLE_LINEAR", "Options:FeatureFlags:EnableLinear")
            },
            ProvidersConfigured = providers != null && providers.Count > 0 ? providers : null,
            Maintenance = maintenance
        };
    }

    private AppMode GetAppMode()
    {
        string configured = Normalize(_configuration["APP_MODE"] ?? _configuration["Options:Config:AppMode"]);
        if (!string.IsNullOrWhiteSpace(configured) && Enum.TryParse<AppMode>(configured, true, out var mode))
        {
            return mode;
        }

        return AppMode.Oss;
    }

    //todo esp
    private IReadOnlyList<ProviderOption> GetProviders()
    {
        IReadOnlyList<string> configuredProviders = GetConfiguredStrings(
            new[] { "Options:Config:ProvidersConfigured" },
            new[] { "PROVIDERS_CONFIGURED" });

        List<ProviderOption> providers = new();

        if (configuredProviders is not null)
        {
            foreach (string providerValue in configuredProviders)
            {
                if (ProviderOptionExtensions.TryParse(providerValue, out ProviderOption provider) && !providers.Contains(provider))
                {
                    providers.Add(provider);
                }
            }
        }

        if (providers.Count == 0)
        {
            AddProviderIfConfigured(providers, ProviderOption.Github, _configuration["GITHUB_APP_CLIENT_ID"] ?? _configuration["GITHUB_CLIENT_ID"]);
            AddProviderIfConfigured(providers, ProviderOption.Gitlab, _configuration["GITLAB_APP_CLIENT_ID"]);
            AddProviderIfConfigured(providers, ProviderOption.Bitbucket, _configuration["BITBUCKET_APP_CLIENT_ID"]);

            if (GetBoolean("ENABLE_ENTERPRISE_SSO", "Options:Config:EnableEnterpriseSso"))
            {
                providers.Add(ProviderOption.EnterpriseSso);
            }
        }

        if (providers.Count == 0)
        {
            return null;
        }

        return new ReadOnlyCollection<ProviderOption>(providers);
    }

    private static void AddProviderIfConfigured(List<ProviderOption> providers, ProviderOption provider, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !providers.Contains(provider))
        {
            providers.Add(provider);
        }
    }

    private MaintenanceWindowDto GetMaintenanceWindow()
    {
        string startTime = Normalize(_configuration["MAINTENANCE_START_TIME"] ?? _configuration["Options:Maintenance:StartTime"]);
        if (string.IsNullOrEmpty(startTime))
        {
            return null;
        }

        return new MaintenanceWindowDto
        {
            StartTime = startTime
        };
    }

    private IReadOnlyList<string> GetConfiguredStrings(string[] sectionPaths, string[] environmentKeys)
    {
        var values = new List<string>();

        foreach (string sectionPath in sectionPaths)
        {
            string[] sectionValues = _configuration.GetSection(sectionPath).Get<string[]>();
            if (sectionValues is null)
            {
                continue;
            }

            foreach (string value in sectionValues)
            {
                AddNormalized(values, value);
            }
        }

        if (values.Count == 0)
        {
            foreach (string key in environmentKeys)
            {
                string raw = _configuration[key];
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string[] segments = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string segment in segments)
                {
                    AddNormalized(values, segment);
                }
            }
        }

        if (values.Count == 0)
        {
            return null;
        }

        return new ReadOnlyCollection<string>(values);
    }

    //todo esp
    private static void AddNormalized(ICollection<string> values, string value)
    {
        string normalized = Normalize(value);
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        if (!values.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(normalized);
        }
    }

    //todo esp
    private bool GetBoolean(params string[] keys)
    {
        foreach (string key in keys)
        {
            string raw = _configuration[key];
            if (raw is null)
            {
                continue;
            }

            raw = raw.Trim();
            if (raw.Length == 0)
            {
                continue;
            }

            if (bool.TryParse(raw, out bool parsed))
            {
                return parsed;
            }

            if (int.TryParse(raw, out int numeric))
            {
                return numeric != 0;
            }

            if (string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "y", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(raw, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "n", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return false;
    }

    //todo esp
    private static string NormalizeOrEmpty(string value)
        => Normalize(value) ?? string.Empty;
    //todo esp
    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
