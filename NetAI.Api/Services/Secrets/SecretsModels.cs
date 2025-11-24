using System.Collections.Generic;
using System.Linq;
using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Services.Secrets;

public record class ProviderTokenInfo(string Token, string Host);

public record class CustomSecretInfo(string Secret, string Description);

public class UserSecrets
{
    public static UserSecrets Empty { get; } = new();

    public IDictionary<ProviderType, ProviderTokenInfo> ProviderTokens { get; }

    public IDictionary<string, CustomSecretInfo> CustomSecrets { get; }

    public UserSecrets()
        : this(null, null)
    {
    }

    public UserSecrets(
        IDictionary<ProviderType, ProviderTokenInfo> providerTokens,
        IDictionary<string, CustomSecretInfo> customSecrets)
    {
        ProviderTokens = providerTokens is not null
            ? new Dictionary<ProviderType, ProviderTokenInfo>(providerTokens)
            : new Dictionary<ProviderType, ProviderTokenInfo>();

        CustomSecrets = customSecrets is not null
            ? new Dictionary<string, CustomSecretInfo>(customSecrets, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, CustomSecretInfo>(StringComparer.OrdinalIgnoreCase);
    }

    public UserSecrets Clone()
    {
        var providerTokensCopy = ProviderTokens.ToDictionary(pair => pair.Key, pair => pair.Value);
        var customSecretsCopy = CustomSecrets.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        return new UserSecrets(providerTokensCopy, customSecretsCopy);
    }
}
