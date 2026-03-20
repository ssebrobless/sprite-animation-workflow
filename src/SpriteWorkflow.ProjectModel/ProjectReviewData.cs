using System.Text.Json.Serialization;

namespace SpriteWorkflow.ProjectModel;

public sealed class ProjectReviewData
{
    [JsonPropertyName("base_variant_reviews")]
    public List<BaseVariantReviewRecord> BaseVariantReviews { get; set; } = [];

    [JsonPropertyName("frame_reviews")]
    public List<FrameReviewRecord> FrameReviews { get; set; } = [];
}

public sealed class BaseVariantReviewRecord
{
    [JsonPropertyName("species")]
    public string Species { get; set; } = string.Empty;

    [JsonPropertyName("age")]
    public string Age { get; set; } = string.Empty;

    [JsonPropertyName("gender")]
    public string Gender { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "unreviewed";

    [JsonPropertyName("note")]
    public string Note { get; set; } = string.Empty;

    [JsonPropertyName("updated_utc")]
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public sealed class FrameReviewRecord
{
    [JsonPropertyName("species")]
    public string Species { get; set; } = string.Empty;

    [JsonPropertyName("age")]
    public string Age { get; set; } = string.Empty;

    [JsonPropertyName("gender")]
    public string Gender { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("family")]
    public string Family { get; set; } = string.Empty;

    [JsonPropertyName("sequence_id")]
    public string SequenceId { get; set; } = string.Empty;

    [JsonPropertyName("frame_index")]
    public int FrameIndex { get; set; }

    [JsonPropertyName("frame_id")]
    public string FrameId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "unreviewed";

    [JsonPropertyName("note")]
    public string Note { get; set; } = string.Empty;

    [JsonPropertyName("issue_tags")]
    public string[] IssueTags { get; set; } = [];

    [JsonPropertyName("updated_utc")]
    public DateTimeOffset? UpdatedUtc { get; set; }
}
