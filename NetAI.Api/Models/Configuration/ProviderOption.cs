using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using NetAI.Api.Serialization;

namespace NetAI.Api.Models.Configuration;

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum ProviderOption
{
    [EnumMember(Value = "github")]
    Github,

    [EnumMember(Value = "gitlab")]
    Gitlab,

    [EnumMember(Value = "bitbucket")]
    Bitbucket,

    [EnumMember(Value = "enterprise_sso")]
    EnterpriseSso
}

public static class ProviderOptionExtensions
{
    public static string GetSerializedName(this ProviderOption option)
        => option switch
        {
            ProviderOption.Github => "github",
            ProviderOption.Gitlab => "gitlab",
            ProviderOption.Bitbucket => "bitbucket",
            ProviderOption.EnterpriseSso => "enterprise_sso",
            _ => option.ToString().ToLowerInvariant()
        };


    //todo est
    public static bool TryParse(string value, out ProviderOption option)
    {
        option = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim();
        foreach (ProviderOption candidate in Enum.GetValues<ProviderOption>())
        {
            if (string.Equals(candidate.GetSerializedName(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                option = candidate;
                return true;
            }
        }

        return false;
    }
}
