using System;
using System.Collections.Generic;

namespace NetAI.RuntimeGateway.Models;

public sealed class RuntimeConversationState
{
    public string Id { get; set; }

    public string SessionApiKey { get; set; }

    public string RuntimeId { get; set; }

    public string SessionId { get; set; }

    public string RuntimeUrl { get; set; }

    public string ConversationUrl { get; set; }

    public string VscodeUrl { get; set; }

    public string ConversationStatus { get; set; }

    public string RuntimeStatus { get; set; }

    public string StatusMessage { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public IReadOnlyList<string> Providers { get; set; } = Array.Empty<string>();
}
