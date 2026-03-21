using System.Text.Json.Serialization;

namespace SpriteWorkflow.ProjectModel;

public sealed class ProjectCandidateData
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("candidates")]
    public List<ProjectCandidateRecord> Candidates { get; set; } = [];
}

public sealed class ProjectCandidateRecord
{
    [JsonPropertyName("candidate_id")]
    public string CandidateId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("target_scope")]
    public string TargetScope { get; set; } = string.Empty;

    [JsonPropertyName("source_type")]
    public string SourceType { get; set; } = "authored_snapshot";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "staged";

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("candidate_image_path")]
    public string CandidateImagePath { get; set; } = string.Empty;

    [JsonPropertyName("reference_image_path")]
    public string ReferenceImagePath { get; set; } = string.Empty;

    [JsonPropertyName("target_frame_path")]
    public string TargetFramePath { get; set; } = string.Empty;

    [JsonPropertyName("import_backup_path")]
    public string ImportBackupPath { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string Note { get; set; } = string.Empty;

    [JsonPropertyName("updated_utc")]
    public DateTimeOffset? UpdatedUtc { get; set; }
}
