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
        Assert.Contains("crow", config.VariantAxes.Species);
        Assert.Equal(".sprite-workflow/reviews.json", config.ReviewDataPath);
        Assert.Equal(".sprite-workflow/requests.json", config.RequestDataPath);
        Assert.Contains(config.WorkflowActions, action => action.ActionId == "refresh_coverage");
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

        Assert.Contains(
            reviewData.BaseVariantReviews,
            review => review.Species == "fox" && review.Age == "teen" && review.Gender == "male" && review.Status == "to_be_repaired");
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

        Assert.Contains(
            requestData.Requests,
            request => request.RequestId == "repair-fox-teen-male-expression" && request.RequestType == "repair_existing");
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
