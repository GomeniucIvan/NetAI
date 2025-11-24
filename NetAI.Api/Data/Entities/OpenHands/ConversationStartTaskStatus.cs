namespace NetAI.Api.Data.Entities.OpenHands;

public enum ConversationStartTaskStatus
{
    Working,
    WaitingForSandbox,
    PreparingRepository,
    RunningSetupScript,
    SettingUpGitHooks,
    StartingConversation,
    Ready,
    Error,
}
