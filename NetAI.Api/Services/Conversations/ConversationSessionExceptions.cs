namespace NetAI.Api.Services.Conversations;

public class ConversationSessionException : Exception
{
    public ConversationSessionException(string message) : base(message)
    {
    }
}

public class ConversationNotFoundException : ConversationSessionException
{
    public ConversationNotFoundException(string conversationId)
        : base($"Conversation '{conversationId}' was not found.")
    {
        ConversationId = conversationId;
    }

    public string ConversationId { get; }
}

public class ConversationUnauthorizedException : ConversationSessionException
{
    public ConversationUnauthorizedException(string conversationId)
        : base($"Conversation '{conversationId}' requires a valid session key.")
    {
        ConversationId = conversationId;
    }

    public string ConversationId { get; }
}

public class ConversationResourceNotFoundException : ConversationSessionException
{
    public ConversationResourceNotFoundException(string conversationId, string resource)
        : base($"Resource '{resource}' was not found for conversation '{conversationId}'.")
    {
        ConversationId = conversationId;
        Resource = resource;
    }

    public string ConversationId { get; }

    public string Resource { get; }
}

public class ConversationRuntimeUnavailableException : ConversationSessionException
{
    public ConversationRuntimeUnavailableException(string conversationId, string reason)
        : base($"Runtime for conversation '{conversationId}' is unavailable ({reason}).")
    {
        ConversationId = conversationId;
        Reason = reason;
    }

    public string ConversationId { get; }

    public string Reason { get; }
}

public class ConversationRuntimeActionException : ConversationSessionException
{
    public ConversationRuntimeActionException(string conversationId, string reason)
        : base($"Runtime action for conversation '{conversationId}' failed ({reason}).")
    {
        ConversationId = conversationId;
        Reason = reason;
    }

    public string ConversationId { get; }

    public string Reason { get; }
}
