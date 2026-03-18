using System.Text.Json;
using SpriteWorkflow.ProjectModel;

namespace SpriteWorkflow.Infrastructure;

public sealed class JsonProjectRequestStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public ProjectRequestData Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ProjectRequestData();
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ProjectRequestData>(stream, SerializerOptions) ?? new ProjectRequestData();
    }

    public void Save(string path, ProjectRequestData data)
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
