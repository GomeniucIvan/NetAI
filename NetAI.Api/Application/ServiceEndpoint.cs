namespace NetAI.Api.Application;

public class ServiceEndpoint
{
    public ServiceEndpoint(string name, string host, int? port, bool? useHttps, string url)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Host = host;
        Port = port;
        UseHttps = useHttps;
        Url = !string.IsNullOrWhiteSpace(url) ? url : BuildUrl(host, port, useHttps);
    }

    public string Name { get; }

    public string Host { get; }

    public int? Port { get; }

    public bool? UseHttps { get; }

    public string Url { get; }

    private static string BuildUrl(string host, int? port, bool? useHttps)
    {
        //TODO R
        if (string.IsNullOrWhiteSpace(host) || port is null or <= 0)
        {
            return null;
        }

        string scheme = useHttps == true ? "https" : "http";
        return $"{scheme}://{host}:{port}";
    }
}
