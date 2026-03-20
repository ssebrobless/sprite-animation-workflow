using System.Text.Json.Serialization;

namespace SpriteWorkflow.ProjectModel;

public sealed class ProjectRequestData
{
    [JsonPropertyName("requests")]
    public List<ProjectRequestRecord> Requests { get; set; } = [];
}

public sealed class ProjectRequestRecord
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("request_type")]
    public string RequestType { get; set; } = "repair_existing";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "draft";

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("target_scope")]
    public string TargetScope { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;

    [JsonPropertyName("must_preserve")]
    public string MustPreserve { get; set; } = string.Empty;

    [JsonPropertyName("must_avoid")]
    public string MustAvoid { get; set; } = string.Empty;

    [JsonPropertyName("source_note")]
    public string SourceNote { get; set; } = string.Empty;

    [JsonPropertyName("history")]
    public List<ProjectRequestHistoryRecord> History { get; set; } = [];

    [JsonPropertyName("updated_utc")]
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public sealed class ProjectRequestHistoryRecord
{
    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("updated_utc")]
    public DateTimeOffset? UpdatedUtc { get; set; }
}
