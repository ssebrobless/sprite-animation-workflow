using SpriteWorkflow.Core;
using SpriteWorkflow.ProjectModel;

namespace SpriteWorkflow.Infrastructure;

public sealed class FileSystemAssetIndexService
{
    public AssetIndexSnapshot BuildSnapshot(ProjectConfig config)
    {
        var authoredRoot = ResolvePath(config.RootPath, config.AuthoredSpriteRoot);
        var species = config.VariantAxes.Species;
        var ages = config.VariantAxes.Age;
        var genders = config.VariantAxes.Gender;
        var colors = config.VariantAxes.Color;

        var expectedVariantCount = species.Length * ages.Length * genders.Length * colors.Length;
        var familyCompleteCounts = config.Families.Keys.ToDictionary(
            family => family,
            _ => 0,
            StringComparer.OrdinalIgnoreCase);
        var speciesCoverage = new List<SpeciesCoverageSummary>();
        var baseVariants = new List<BaseVariantCoverageSummary>();

        var completeVariants = 0;

        foreach (var speciesName in species)
        {
            var familyBaseRowCounts = config.Families.Keys.ToDictionary(
                family => family,
                _ => 0,
                StringComparer.OrdinalIgnoreCase);

            foreach (var age in ages)
            {
                foreach (var gender in genders)
                {
                    var rowFamilyCounts = config.Families.Keys.ToDictionary(
                        family => family,
                        _ => 0,
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var color in colors)
                    {
                        var variantDir = Path.Combine(authoredRoot, speciesName, age, gender, color);
                        var variantComplete = config.Families.All(family => SequenceFramesExist(variantDir, family.Value));
                        if (variantComplete)
                        {
                            completeVariants++;
                        }
                    }

                    foreach (var family in config.Families)
                    {
                        var familyCompleteForAllColors = true;
                        var completeColorsForFamily = 0;
                        foreach (var color in colors)
                        {
                            var variantDir = Path.Combine(authoredRoot, speciesName, age, gender, color);
                            var familyCompleteForColor = SequenceFramesExist(variantDir, family.Value);
                            if (familyCompleteForColor)
                            {
                                familyCompleteCounts[family.Key]++;
                                completeColorsForFamily++;
                            }
                            else
                            {
                                familyCompleteForAllColors = false;
                            }
                        }

                        rowFamilyCounts[family.Key] = completeColorsForFamily;

                        if (familyCompleteForAllColors)
                        {
                            familyBaseRowCounts[family.Key]++;
                        }
                    }

                    baseVariants.Add(
                        new BaseVariantCoverageSummary
                        {
                            Species = speciesName,
                            Age = age,
                            Gender = gender,
                            ExpectedColors = colors.Length,
                            CompleteColorsByFamily = new Dictionary<string, int>(rowFamilyCounts, StringComparer.OrdinalIgnoreCase),
                            OverallStatus = ComputeOverallStatus(rowFamilyCounts, colors.Length),
                        });

                }
            }

            speciesCoverage.Add(
                new SpeciesCoverageSummary
                {
                    Species = speciesName,
                    ExpectedBaseRows = ages.Length * genders.Length,
                    CompleteBaseRowsByFamily = new Dictionary<string, int>(familyBaseRowCounts, StringComparer.OrdinalIgnoreCase),
                });
        }

        var familyCoverage = config.Families.Keys
            .Select(
                family => new FamilyCoverageSummary
                {
                    Family = family,
                    ExpectedVariantCount = expectedVariantCount,
                    CompleteVariantCount = familyCompleteCounts[family],
                    IncompleteVariantCount = expectedVariantCount - familyCompleteCounts[family],
                })
            .OrderBy(summary => summary.Family)
            .ToList();

        return new AssetIndexSnapshot
        {
            ExpectedVariantCount = expectedVariantCount,
            CompleteVariantCount = completeVariants,
            IncompleteVariantCount = expectedVariantCount - completeVariants,
            FamilyCoverage = familyCoverage,
            SpeciesCoverage = speciesCoverage.OrderBy(summary => summary.Species).ToList(),
            BaseVariants = baseVariants
                .OrderBy(summary => summary.Species)
                .ThenBy(summary => summary.Age)
                .ThenBy(summary => summary.Gender)
                .ToList(),
        };
    }

    private static string ComputeOverallStatus(
        IReadOnlyDictionary<string, int> completeColorsByFamily,
        int expectedColors)
    {
        var values = completeColorsByFamily.Values.ToList();
        if (values.Count == 0 || values.All(count => count == 0))
        {
            return "missing";
        }

        if (values.All(count => count == expectedColors))
        {
            return "complete";
        }

        return "partial";
    }

    private static bool SequenceFramesExist(string variantDirectory, IEnumerable<AnimationSequenceConfig> sequences)
    {
        if (!Directory.Exists(variantDirectory))
        {
            return false;
        }

        foreach (var sequence in sequences)
        {
            for (var index = 0; index < sequence.FrameCount; index++)
            {
                var framePath = Path.Combine(variantDirectory, $"{sequence.SequenceId}_{index:00}.png");
                if (!File.Exists(framePath))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string ResolvePath(string rootPath, string relativeOrAbsolutePath)
    {
        return Path.IsPathRooted(relativeOrAbsolutePath)
            ? relativeOrAbsolutePath
            : Path.GetFullPath(Path.Combine(rootPath, relativeOrAbsolutePath));
    }
}
