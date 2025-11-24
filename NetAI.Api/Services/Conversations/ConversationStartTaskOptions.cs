using System;

namespace NetAI.Api.Services.Conversations;

public class ConversationStartTaskOptions
{
    public TimeSpan CompletedTaskRetention { get; set; } = TimeSpan.FromHours(1);
}
