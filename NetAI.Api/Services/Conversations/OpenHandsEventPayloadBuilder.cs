using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetAI.Api.Services.Conversations;

internal static class OpenHandsEventPayloadBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string CreateUserMessageEvent(int eventId, DateTimeOffset timestamp, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payload = new
        {
            id = eventId,
            source = "user",
            timestamp = timestamp.ToString("O"),
            action = "message",
            message,
            args = new
            {
                content = message,
                image_urls = (string[])null,
                file_urls = (string[])null,
                wait_for_response = false,
                security_risk = -1
            }
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public static string CreateConversationInstructionsEvent(
        int eventId,
        DateTimeOffset timestamp,
        string instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);

        var payload = new
        {
            id = eventId,
            source = "environment",
            timestamp = timestamp.ToString("O"),
            observation = "recall",
            message = "Added workspace context",
            content = string.Empty,
            extras = new
            {
                recall_type = "workspace_context",
                repo_name = string.Empty,
                repo_directory = string.Empty,
                repo_branch = string.Empty,
                repo_instructions = string.Empty,
                runtime_hosts = new Dictionary<string, int>(),
                additional_agent_instructions = string.Empty,
                custom_secrets_descriptions = new Dictionary<string, string>(),
                date = string.Empty,
                working_dir = string.Empty,
                conversation_instructions = instructions,
                microagent_knowledge = Array.Empty<object>()
            }
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }
}
