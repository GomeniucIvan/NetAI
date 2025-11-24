namespace NetAI.Extensions
{
    public static class HostExtensions
    {
        public static string BuildFullUrl(string host, string conversationUrl)
        {
            var newHost = new Uri(host);

            if (Uri.TryCreate(conversationUrl, UriKind.Absolute, out var existing))
            {
                var builder = new UriBuilder(existing)
                {
                    Scheme = newHost.Scheme,
                    Host = newHost.Host,
                    Port = newHost.Port
                };

                return builder.Uri.ToString();
            }

            return $"{host}{conversationUrl}";
        }
    }
}
