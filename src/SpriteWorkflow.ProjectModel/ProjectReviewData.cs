using System.Text.Json.Serialization;

namespace SpriteWorkflow.ProjectModel;

public sealed class ProjectReviewData
{
    [JsonPropertyName("base_variant_reviews")]
    public List<BaseVariantReviewRecord> BaseVariantReviews { get; set; } = [];
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
