using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetAI.Api.Data.Entities.Conversations;
using NetAI.Api.Data.Entities.OpenHands;
using NetAI.Api.Data.Entities.Sandboxes;

namespace NetAI.Api.Data;

public class NetAiDbContext : DbContext
{
    public NetAiDbContext(DbContextOptions<NetAiDbContext> options)
        : base(options)
    {
    }

    public DbSet<ConversationMetadataRecord> Conversations => Set<ConversationMetadataRecord>();

    public DbSet<ConversationEventRecord> ConversationEvents => Set<ConversationEventRecord>();

    public DbSet<ConversationRuntimeInstanceRecord> ConversationRuntimeInstances => Set<ConversationRuntimeInstanceRecord>();

    public DbSet<ConversationRuntimeHostRecord> ConversationRuntimeHosts => Set<ConversationRuntimeHostRecord>();

    public DbSet<ConversationRuntimeProviderRecord> ConversationRuntimeProviders => Set<ConversationRuntimeProviderRecord>();

    public DbSet<ConversationMicroagentRecord> ConversationMicroagents => Set<ConversationMicroagentRecord>();

    public DbSet<ConversationFileRecord> ConversationFiles => Set<ConversationFileRecord>();

    public DbSet<ConversationGitDiffRecord> ConversationGitDiffs => Set<ConversationGitDiffRecord>();

    public DbSet<ConversationFeedbackRecord> ConversationFeedbackEntries => Set<ConversationFeedbackRecord>();

    public DbSet<ConversationRememberPromptRecord> ConversationRememberPrompts => Set<ConversationRememberPromptRecord>();

    public DbSet<SandboxRecord> Sandboxes => Set<SandboxRecord>();

    public DbSet<SandboxSpecRecord> SandboxSpecs => Set<SandboxSpecRecord>();

    public DbSet<ConversationStartTaskRecord> ConversationStartTasks => Set<ConversationStartTaskRecord>();

    public DbSet<EventCallbackRecord> EventCallbacks => Set<EventCallbackRecord>();

    public DbSet<EventCallbackResultRecord> EventCallbackResults => Set<EventCallbackResultRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureConversationMetadata(modelBuilder);
        ConfigureEvents(modelBuilder);
        ConfigureRuntime(modelBuilder);
        ConfigureMicroagents(modelBuilder);
        ConfigureFiles(modelBuilder);
        ConfigureGitDiffs(modelBuilder);
        ConfigureFeedback(modelBuilder);
        ConfigureRememberPrompts(modelBuilder);
        ConfigureSandboxes(modelBuilder);
        ConfigureStartTasks(modelBuilder);
        ConfigureEventCallbacks(modelBuilder);
    }

    private static void ConfigureConversationMetadata(ModelBuilder modelBuilder)
    {
        var pullRequestConverter = new ValueConverter<List<int>, string>(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions)null),
            json => string.IsNullOrWhiteSpace(json)
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(json, (JsonSerializerOptions)null)
                    ?? new List<int>());

        var pullRequestComparer = new ValueComparer<List<int>>(
            (left, right) =>
                left != null && right != null && left.SequenceEqual(right),
            value => value == null
                ? 0
                : value.Aggregate(0, (current, item) => unchecked((current * 397) ^ item)),
            value => value == null ? new List<int>() : value.ToList());

        modelBuilder.Entity<ConversationMetadataRecord>(builder =>
        {
            builder.ToTable("ConversationMetadata");
            builder.HasKey(record => record.Id);
            builder.HasIndex(record => record.ConversationId).IsUnique();

            builder.Property(record => record.Trigger)
                .HasConversion<string>();

            builder.Property(record => record.GitProvider)
                .HasConversion<string>();

            builder.Property(record => record.Status)
                .HasConversion<string>();

            builder.Property(record => record.PullRequestNumbers)
                .HasConversion(pullRequestConverter)
                .Metadata.SetValueComparer(pullRequestComparer);

            builder.Property(record => record.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(record => record.LastUpdatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.HasMany(record => record.Events)
                .WithOne(evt => evt.Conversation)
                .HasForeignKey(evt => evt.ConversationMetadataRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(record => record.RuntimeInstance)
                .WithOne(runtime => runtime.Conversation)
                .HasForeignKey<ConversationRuntimeInstanceRecord>(runtime => runtime.ConversationMetadataRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(record => record.Microagents)
                .WithOne(microagent => microagent.Conversation)
                .HasForeignKey(microagent => microagent.ConversationMetadataRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(record => record.Files)
                .WithOne(file => file.Conversation)
                .HasForeignKey(file => file.ConversationMetadataRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(record => record.GitDiffs)
                .WithOne(diff => diff.Conversation)
                .HasForeignKey(diff => diff.ConversationMetadataRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(record => record.FeedbackEntries)
                .WithOne(feedback => feedback.Conversation)
                .HasForeignKey(feedback => feedback.ConversationMetadataRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(record => record.RememberPrompts)
                .WithOne(prompt => prompt.Conversation)
                .HasForeignKey(prompt => prompt.ConversationMetadataRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureEvents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationEventRecord>(builder =>
        {
            builder.ToTable("ConversationEvents");
            builder.HasKey(evt => new { evt.ConversationMetadataRecordId, evt.EventId });
            builder.Property(evt => evt.EventId).ValueGeneratedNever();
            builder.Property(evt => evt.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }

    private static void ConfigureRuntime(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationRuntimeInstanceRecord>(builder =>
        {
            builder.ToTable("ConversationRuntimeInstances");
            builder.HasKey(runtime => runtime.Id);
            builder.Property(runtime => runtime.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<ConversationRuntimeHostRecord>(builder =>
        {
            builder.ToTable("ConversationRuntimeHosts");
            builder.HasKey(host => host.Id);
            builder.HasIndex(host => new { host.ConversationRuntimeInstanceRecordId, host.Name })
                .IsUnique();
        });

        modelBuilder.Entity<ConversationRuntimeProviderRecord>(builder =>
        {
            builder.ToTable("ConversationRuntimeProviders");
            builder.HasKey(provider => provider.Id);
            builder.HasIndex(provider => new { provider.ConversationRuntimeInstanceRecordId, provider.Provider })
                .IsUnique();
        });
    }

    private static void ConfigureMicroagents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationMicroagentRecord>(builder =>
        {
            builder.ToTable("ConversationMicroagents");
            builder.HasKey(microagent => microagent.Id);
            builder.HasIndex(microagent => new { microagent.ConversationMetadataRecordId, microagent.Name })
                .IsUnique();
        });
    }

    private static void ConfigureFiles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationFileRecord>(builder =>
        {
            builder.ToTable("ConversationFiles");
            builder.HasKey(file => file.Id);
            builder.HasIndex(file => new { file.ConversationMetadataRecordId, file.Path })
                .IsUnique();
        });
    }

    private static void ConfigureGitDiffs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationGitDiffRecord>(builder =>
        {
            builder.ToTable("ConversationGitDiffs");
            builder.HasKey(diff => diff.Id);
            builder.HasIndex(diff => new { diff.ConversationMetadataRecordId, diff.Path })
                .IsUnique();
        });
    }

    private static void ConfigureFeedback(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationFeedbackRecord>(builder =>
        {
            builder.ToTable("ConversationFeedbackEntries");
            builder.HasKey(feedback => feedback.Id);
        });
    }

    private static void ConfigureRememberPrompts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationRememberPromptRecord>(builder =>
        {
            builder.ToTable("ConversationRememberPrompts");
            builder.HasKey(prompt => prompt.Id);
            builder.HasIndex(prompt => new { prompt.ConversationMetadataRecordId, prompt.EventId })
                .IsUnique();
        });
    }

    private static void ConfigureSandboxes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SandboxSpecRecord>(builder =>
        {
            builder.ToTable("SandboxSpecs");
            builder.HasKey(spec => spec.Id);
            builder.Property(spec => spec.Id)
                .HasMaxLength(100);
            builder.Property(spec => spec.CommandJson)
                .HasColumnName("Command");
            builder.Property(spec => spec.InitialEnvJson)
                .HasColumnName("InitialEnv")
                .HasDefaultValue("{}");
            builder.Property(spec => spec.WorkingDir)
                .HasMaxLength(256)
                .HasDefaultValue("/home/openhands/workspace");
            builder.Property(spec => spec.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<SandboxRecord>(builder =>
        {
            builder.ToTable("Sandboxes");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Id)
                .HasMaxLength(100);
            builder.Property(record => record.SandboxSpecId)
                .HasMaxLength(100);
            builder.Property(record => record.CreatedByUserId)
                .HasMaxLength(200);
            builder.Property(record => record.Status)
                .HasConversion<string>()
                .HasMaxLength(50);
            builder.Property(record => record.SessionApiKey)
                .HasMaxLength(200);
            builder.Property(record => record.ExposedUrlsJson)
                .HasColumnName("ExposedUrls")
                .HasDefaultValue("[]");
            builder.Property(record => record.RuntimeId)
                .HasMaxLength(200);
            builder.Property(record => record.RuntimeUrl)
                .HasMaxLength(512);
            builder.Property(record => record.WorkspacePath)
                .HasMaxLength(512);
            builder.Property(record => record.RuntimeHostsJson)
                .HasColumnName("RuntimeHosts")
                .HasDefaultValue("[]");
            builder.Property(record => record.RuntimeStateJson)
                .HasColumnName("RuntimeState")
                .HasDefaultValue("{}");
            builder.Property(record => record.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(record => record.LastUpdatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.HasOne<SandboxSpecRecord>()
                .WithMany()
                .HasForeignKey(record => record.SandboxSpecId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(record => record.SandboxSpecId);
        });
    }

    private static void ConfigureStartTasks(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationStartTaskRecord>(builder =>
        {
            builder.ToTable("ConversationStartTasks");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.CreatedByUserId)
                .HasMaxLength(200);
            builder.Property(record => record.Status)
                .HasConversion<string>()
                .HasMaxLength(64);
            builder.Property(record => record.Detail)
                .HasMaxLength(512);
            builder.Property(record => record.FailureDetail)
                .HasMaxLength(512);
            builder.Property(record => record.AppConversationId)
                .HasMaxLength(100);
            builder.Property(record => record.SandboxId)
                .HasMaxLength(100);
            builder.Property(record => record.AgentServerUrl)
                .HasMaxLength(512);
            builder.Property(record => record.SandboxSessionApiKey)
                .HasMaxLength(200);
            builder.Property(record => record.SandboxWorkspacePath)
                .HasMaxLength(512);
            builder.Property(record => record.SandboxVscodeUrl)
                .HasMaxLength(512);
            builder.Property(record => record.ConversationStatus)
                .HasMaxLength(100);
            builder.Property(record => record.RuntimeStatus)
                .HasMaxLength(100);
            builder.Property(record => record.BackendError)
                .HasMaxLength(512);
            builder.Property(record => record.RequestJson)
                .HasColumnName("Request")
                .HasDefaultValue("{}");
            builder.Property(record => record.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(record => record.UpdatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.HasIndex(record => record.CreatedAtUtc);
            builder.HasIndex(record => record.AppConversationId);
        });
    }

    private static void ConfigureEventCallbacks(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventCallbackRecord>(builder =>
        {
            builder.ToTable("event_callback");
            builder.HasKey(callback => callback.Id);
            builder.Property(callback => callback.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            builder.Property(callback => callback.ProcessorJson)
                .HasColumnName("processor")
                .HasColumnType("jsonb");
            builder.Property(callback => callback.EventKind)
                .HasColumnName("event_kind");
            builder.Property(callback => callback.ConversationId)
                .HasColumnName("conversation_id");
            builder.HasIndex(callback => callback.CreatedAtUtc)
                .HasDatabaseName("ix_event_callback_created_at");
            builder.HasMany(callback => callback.Results)
                .WithOne(result => result.EventCallback)
                .HasForeignKey(result => result.EventCallbackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventCallbackResultRecord>(builder =>
        {
            builder.ToTable("event_callback_result");
            builder.HasKey(result => result.Id);
            builder.Property(result => result.EventCallbackId)
                .HasColumnName("event_callback_id");
            builder.Property(result => result.EventId)
                .HasColumnName("event_id");
            builder.Property(result => result.ConversationId)
                .HasColumnName("conversation_id");
            builder.Property(result => result.Detail)
                .HasColumnName("detail");
            builder.Property(result => result.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");

            builder.Property(result => result.Status)
                .HasColumnName("status")
                .HasConversion(
                    status => status.ToString().ToUpperInvariant(),
                    value => ParseEventCallbackResultStatus(value));

            builder.HasIndex(result => result.ConversationId)
                .HasDatabaseName("ix_event_callback_result_conversation_id");
            builder.HasIndex(result => result.CreatedAtUtc)
                .HasDatabaseName("ix_event_callback_result_created_at");
            builder.HasIndex(result => result.EventCallbackId)
                .HasDatabaseName("ix_event_callback_result_event_callback_id");
            builder.HasIndex(result => result.EventId)
                .HasDatabaseName("ix_event_callback_result_event_id");
        });
    }
    private static EventCallbackResultStatus ParseEventCallbackResultStatus(string value)
    {
        if (Enum.TryParse<EventCallbackResultStatus>(value, ignoreCase: true, out EventCallbackResultStatus parsed))
        {
            return parsed;
        }

        return EventCallbackResultStatus.Error;
    }
}
