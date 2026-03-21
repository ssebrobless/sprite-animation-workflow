using SpriteWorkflow.Infrastructure;

namespace SpriteWorkflow.Tests;

public sealed class ProjectConfigTests
{
    [Fact]
    public void WevitoSampleConfig_LoadsExpectedProjectId()
    {
        var root = FindRepositoryRoot();
        var configPath = Path.Combine(root, "sample-projects", "wevito.project.json");
        var service = new JsonProjectConfigService();

        var config = service.Load(configPath);

        Assert.Equal("wevito", config.ProjectId);
        Assert.Equal("Wevito", config.DisplayName);
        Assert.Equal(1, config.SchemaVersion);
        Assert.Contains("crow", config.VariantAxes.Species);
        Assert.Equal(".sprite-workflow/reviews.json", config.ReviewDataPath);
        Assert.Equal(".sprite-workflow/requests.json", config.RequestDataPath);
        Assert.Equal(".sprite-workflow/candidates.json", config.CandidateDataPath);
        Assert.Equal("gemini-browser", config.DefaultAiProviderId);
        Assert.Contains(config.AiProviders, provider => provider.ProviderId == "gemini-browser" && provider.ProviderKind == "browser_chat");
        Assert.Contains(config.AiProviders, provider => provider.ProviderId == "local-hidden-tools" && provider.SupportsAutomation);
        Assert.Contains(config.WorkflowActions, action => action.ActionId == "refresh_coverage");
        Assert.Contains(config.WorkflowActions, action => action.ActionId == "prepare_expression_handoffs" && action.ExecutionMode == "hidden_process");
        Assert.Contains(config.WorkflowActions, action => action.ActionId == "gemini_generation_external" && action.ExecutionMode == "manual_browser");
    }

    [Fact]
    public void BlankStarterSampleConfig_LoadsGenericProjectShape()
    {
        var root = FindRepositoryRoot();
        var configPath = Path.Combine(root, "sample-projects", "blank-starter.project.json");
        var service = new JsonProjectConfigService();

        var config = service.Load(configPath);

        Assert.Equal("blank-starter", config.ProjectId);
        Assert.Equal(1, config.SchemaVersion);
        Assert.Equal("manual-first", config.DefaultAiProviderId);
        Assert.Contains(config.AiProviders, provider => provider.ProviderId == "manual-first" && provider.ProviderKind == "manual_only");
        Assert.Contains(config.Families, family => family.Key == "locomotion");
    }

    [Fact]
    public void ImportedExternalSampleConfig_LoadsExternalProjectShape()
    {
        var root = FindRepositoryRoot();
        var configPath = Path.Combine(root, "sample-projects", "imported-external.project.json");
        var service = new JsonProjectConfigService();

        var config = service.Load(configPath);

        Assert.Equal("imported-external", config.ProjectId);
        Assert.Equal(1, config.SchemaVersion);
        Assert.Equal("Assets/Sprites/Runtime", config.RuntimeSpriteRoot);
        Assert.Equal("Art/Sprites/Authored", config.AuthoredSpriteRoot);
        Assert.Contains(config.AiProviders, provider => provider.ProviderId == "generic-browser-ai");
        Assert.Contains(config.Families, family => family.Key == "combat");
    }

    [Fact]
    public void WevitoSampleReviewStore_LoadsSeededRows()
    {
        var reviewPath = Path.Combine(
            @"C:\Users\fishe\Documents\projects\wevito",
            ".sprite-workflow",
            "reviews.json");
        var store = new JsonProjectReviewStore();

        var reviewData = store.Load(reviewPath);

        Assert.Equal(1, reviewData.SchemaVersion);
        Assert.Contains(
            reviewData.BaseVariantReviews,
            review => review.Species == "fox" && review.Age == "teen" && review.Gender == "male" && review.Status == "approved");
    }

    [Fact]
    public void WevitoSampleRequestStore_LoadsSeededRequests()
    {
        var requestPath = Path.Combine(
            @"C:\Users\fishe\Documents\projects\wevito",
            ".sprite-workflow",
            "requests.json");
        var store = new JsonProjectRequestStore();

        var requestData = store.Load(requestPath);

        Assert.Equal(1, requestData.SchemaVersion);
        Assert.Contains(
            requestData.Requests,
            request => request.RequestId == "repair-fox-teen-male-expression" && request.RequestType == "repair_existing");
    }

    [Fact]
    public void WevitoSampleCandidateStore_LoadsCandidateFile()
    {
        var candidatePath = Path.Combine(
            @"C:\Users\fishe\Documents\projects\wevito",
            ".sprite-workflow",
            "candidates.json");
        var store = new JsonProjectCandidateStore();

        var candidateData = store.Load(candidatePath);

        Assert.NotNull(candidateData);
        Assert.NotNull(candidateData.Candidates);
        Assert.Equal(1, candidateData.SchemaVersion);
    }

    private static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        var directory = new DirectoryInfo(current);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "SpriteWorkflow.sln");
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from test output.");
    }
}
