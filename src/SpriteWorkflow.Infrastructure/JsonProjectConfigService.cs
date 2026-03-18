using System.Text.Json;
using SpriteWorkflow.ProjectModel;

namespace SpriteWorkflow.Infrastructure;

public sealed class JsonProjectConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public ProjectConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<ProjectConfig>(json, JsonOptions);
        if (config is null)
        {
            throw new InvalidOperationException($"Failed to deserialize project config at '{path}'.");
        }

        return config;
    }

    public void Save(string path, ProjectConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
    }
}
