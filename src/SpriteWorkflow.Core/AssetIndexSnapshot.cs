namespace SpriteWorkflow.Core;

public sealed class AssetIndexSnapshot
{
    public int ExpectedVariantCount { get; init; }

    public int CompleteVariantCount { get; init; }

    public int IncompleteVariantCount { get; init; }

    public IReadOnlyList<FamilyCoverageSummary> FamilyCoverage { get; init; } = [];

    public IReadOnlyList<SpeciesCoverageSummary> SpeciesCoverage { get; init; } = [];

    public IReadOnlyList<BaseVariantCoverageSummary> BaseVariants { get; init; } = [];
}

public sealed class FamilyCoverageSummary
{
    public string Family { get; init; } = string.Empty;

    public int ExpectedVariantCount { get; init; }

    public int CompleteVariantCount { get; init; }

    public int IncompleteVariantCount { get; init; }
}

public sealed class SpeciesCoverageSummary
{
    public string Species { get; init; } = string.Empty;

    public int ExpectedBaseRows { get; init; }

    public IReadOnlyDictionary<string, int> CompleteBaseRowsByFamily { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}

public sealed class BaseVariantCoverageSummary
{
    public string Species { get; init; } = string.Empty;

    public string Age { get; init; } = string.Empty;

    public string Gender { get; init; } = string.Empty;

    public int ExpectedColors { get; init; }

    public IReadOnlyDictionary<string, int> CompleteColorsByFamily { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public string OverallStatus { get; init; } = "missing";
}
