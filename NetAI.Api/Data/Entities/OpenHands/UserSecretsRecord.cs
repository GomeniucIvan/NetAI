using System.ComponentModel.DataAnnotations;

namespace NetAI.Api.Data.Entities.OpenHands;

public class UserSecretsRecord
{
    public Guid Id { get; set; }

    public Guid SettingsId { get; set; }

    public IDictionary<ProviderType, ProviderTokenRecord> ProviderTokens { get; set; }
        = new Dictionary<ProviderType, ProviderTokenRecord>();

    public IDictionary<string, CustomSecretRecord> CustomSecrets { get; set; }
        = new Dictionary<string, CustomSecretRecord>(StringComparer.OrdinalIgnoreCase);

    [MaxLength(128)]
    public string ExternalAuthId { get; set; }

    [MaxLength(512)]
    public string ExternalAuthToken { get; set; }

    public OpenHandsSettingsRecord Settings { get; set; } = null!;
}
