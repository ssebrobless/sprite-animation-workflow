using System.Text.Json.Serialization;

namespace SpriteWorkflow.ProjectModel;

public sealed class ProjectConfig
{
    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("root_path")]
    public string RootPath { get; set; } = string.Empty;

    [JsonPropertyName("runtime_sprite_root")]
    public string RuntimeSpriteRoot { get; set; } = string.Empty;

    [JsonPropertyName("authored_sprite_root")]
    public string AuthoredSpriteRoot { get; set; } = string.Empty;

    [JsonPropertyName("incoming_handoff_root")]
    public string IncomingHandoffRoot { get; set; } = string.Empty;

    [JsonPropertyName("artifact_root")]
    public string ArtifactRoot { get; set; } = string.Empty;

    [JsonPropertyName("review_data_path")]
    public string ReviewDataPath { get; set; } = string.Empty;

    [JsonPropertyName("request_data_path")]
    public string RequestDataPath { get; set; } = string.Empty;

    [JsonPropertyName("variant_axes")]
    public VariantAxesConfig VariantAxes { get; set; } = new();

    [JsonPropertyName("families")]
    public Dictionary<string, AnimationSequenceConfig[]> Families { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("workflow_actions")]
    public WorkflowActionConfig[] WorkflowActions { get; set; } = [];
}

public sealed class VariantAxesConfig
{
    [JsonPropertyName("species")]
    public string[] Species { get; set; } = [];

    [JsonPropertyName("age")]
    public string[] Age { get; set; } = [];

    [JsonPropertyName("gender")]
    public string[] Gender { get; set; } = [];

    [JsonPropertyName("color")]
    public string[] Color { get; set; } = [];
}

public sealed class AnimationSequenceConfig
{
    [JsonPropertyName("sequence_id")]
    public string SequenceId { get; set; } = string.Empty;

    [JsonPropertyName("frame_count")]
    public int FrameCount { get; set; }
}

public sealed class WorkflowActionConfig
{
    [JsonPropertyName("action_id")]
    public string ActionId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string[] Arguments { get; set; } = [];

    [JsonPropertyName("working_directory")]
    public string WorkingDirectory { get; set; } = string.Empty;
}
