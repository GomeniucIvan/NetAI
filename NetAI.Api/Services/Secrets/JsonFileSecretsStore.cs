using System.Text.Json;
using System.Text.Json.Serialization;
using NetAI.Api.Data.Entities.OpenHands;

namespace NetAI.Api.Services.Secrets;

public class JsonFileSecretsStore : ISecretsStore
{
    private readonly string _filePath;
    private readonly ILogger<JsonFileSecretsStore> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonFileSecretsStore(ILogger<JsonFileSecretsStore> logger, IConfiguration configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _filePath = ResolvePath(configuration);

        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
        _serializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    }

    public async Task<UserSecrets> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            await using FileStream stream = File.OpenRead(_filePath);
            SerializableSecrets serialized = await JsonSerializer.DeserializeAsync<SerializableSecrets>(stream, _serializerOptions, cancellationToken).ConfigureAwait(false);
            return serialized?.ToUserSecrets();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize secrets from {Path}", _filePath);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to read secrets from {Path}", _filePath);
            return null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task StoreAsync(UserSecrets secrets, CancellationToken cancellationToken = default)
    {
        if (secrets is null)
        {
            throw new ArgumentNullException(nameof(secrets));
        }

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string directory = Path.GetDirectoryName(_filePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            SerializableSecrets serializable = SerializableSecrets.FromUserSecrets(secrets);
            await using FileStream stream = new(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, serializable, _serializerOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to persist secrets to {Path}", _filePath);
            throw;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private static string ResolvePath(IConfiguration configuration)
    {
        string configuredPath = Environment.GetEnvironmentVariable("OPENHANDS_SECRETS_PATH")
                                ?? configuration?["Storage:SecretsPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string baseDirectory = Environment.GetEnvironmentVariable("OPENHANDS_HOME")
            ?? configuration?["Storage:RootPath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openhands");

        return Path.Combine(baseDirectory, "secrets.json");
    }

    private sealed record SerializableSecrets(
        Dictionary<ProviderType, SerializableProviderToken> ProviderTokens,
        Dictionary<string, SerializableCustomSecret> CustomSecrets)
    {
        public UserSecrets ToUserSecrets()
        {
            var providerTokens = new Dictionary<ProviderType, ProviderTokenInfo>();
            if (ProviderTokens is not null)
            {
                foreach ((ProviderType provider, SerializableProviderToken token) in ProviderTokens)
                {
                    if (token is null || string.IsNullOrWhiteSpace(token.Token))
                    {
                        continue;
                    }

                    providerTokens[provider] = new ProviderTokenInfo(token.Token, token.Host);
                }
            }

            var customSecrets = new Dictionary<string, CustomSecretInfo>(StringComparer.OrdinalIgnoreCase);
            if (CustomSecrets is not null)
            {
                foreach ((string name, SerializableCustomSecret secret) in CustomSecrets)
                {
                    if (secret is null)
                    {
                        continue;
                    }

                    customSecrets[name] = new CustomSecretInfo(secret.Value, secret.Description ?? string.Empty);
                }
            }

            return new UserSecrets(providerTokens, customSecrets);
        }

        public static SerializableSecrets FromUserSecrets(UserSecrets secrets)
        {
            var providerTokens = new Dictionary<ProviderType, SerializableProviderToken>();
            foreach ((ProviderType provider, ProviderTokenInfo token) in secrets.ProviderTokens)
            {
                if (token is null || string.IsNullOrWhiteSpace(token.Token))
                {
                    continue;
                }

                providerTokens[provider] = new SerializableProviderToken(token.Token, token.Host);
            }

            var customSecrets = new Dictionary<string, SerializableCustomSecret>(StringComparer.OrdinalIgnoreCase);
            foreach ((string name, CustomSecretInfo secret) in secrets.CustomSecrets)
            {
                customSecrets[name] = new SerializableCustomSecret(secret.Secret, secret.Description);
            }

            return new SerializableSecrets(providerTokens, customSecrets);
        }
    }

    private sealed record SerializableProviderToken(string Token, string Host);

    private sealed record SerializableCustomSecret(string Value, string Description);
}
