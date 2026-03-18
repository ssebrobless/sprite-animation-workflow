using System.Text.Json;
using SpriteWorkflow.ProjectModel;

namespace SpriteWorkflow.Infrastructure;

public sealed class JsonProjectReviewStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public ProjectReviewData Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ProjectReviewData();
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ProjectReviewData>(stream, SerializerOptions) ?? new ProjectReviewData();
    }

    public void Save(string path, ProjectReviewData data)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, data, SerializerOptions);
    }
}
