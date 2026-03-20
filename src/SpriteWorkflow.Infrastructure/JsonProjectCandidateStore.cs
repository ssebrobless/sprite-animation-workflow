using System.Text.Json;
using SpriteWorkflow.ProjectModel;

namespace SpriteWorkflow.Infrastructure;

public sealed class JsonProjectCandidateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public ProjectCandidateData Load(string path)
    {
        if (!File.Exists(path))
        {
            return new ProjectCandidateData();
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<ProjectCandidateData>(stream, SerializerOptions) ?? new ProjectCandidateData();
    }

    public void Save(string path, ProjectCandidateData data)
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
