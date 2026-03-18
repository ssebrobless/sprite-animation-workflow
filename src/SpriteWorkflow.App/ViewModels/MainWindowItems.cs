using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpriteWorkflow.App.ViewModels;

public sealed record FamilyCoverageItemViewModel(string Family, int Complete, int Expected)
{
    public string Progress => $"{Complete}/{Expected}";
}

public sealed record SpeciesCoverageItemViewModel(
    string Species,
    int ExpectedBaseRows,
    string Locomotion,
    string Care,
    string Expression);

public sealed record WorkflowActionItemViewModel(string Name, string Command, string Arguments);

public sealed record RequestItemViewModel(
    string RequestId,
    string RequestType,
    string Status,
    string Title,
    string TargetScope,
    string Details,
    string MustPreserve,
    string MustAvoid,
    string SourceNote,
    DateTimeOffset? UpdatedUtc)
{
    public string StatusLabel => FormatLabel(Status);
    public string TypeLabel => FormatLabel(RequestType);
    public string UpdatedLabel => UpdatedUtc is null ? "Not saved yet." : $"Updated {UpdatedUtc.Value.ToLocalTime():MMM d h:mm tt}";

    private static string FormatLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var spaced = value.Replace('_', ' ');
        return string.Join(" ", spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}

public sealed partial class BaseVariantRowItemViewModel : ObservableObject
{
    public BaseVariantRowItemViewModel(
        string species,
        string age,
        string gender,
        int expectedColors,
        string overallStatus,
        IReadOnlyDictionary<string, int> completeColorsByFamily,
        string familySummary,
        string reviewStatus,
        string reviewNote,
        DateTimeOffset? reviewUpdatedUtc)
    {
        Species = species;
        Age = age;
        Gender = gender;
        ExpectedColors = expectedColors;
        OverallStatus = overallStatus;
        CompleteColorsByFamily = completeColorsByFamily;
        FamilySummary = familySummary;
        this.reviewStatus = reviewStatus;
        this.reviewNote = reviewNote;
        this.reviewUpdatedUtc = reviewUpdatedUtc;
    }

    public string Species { get; }
    public string Age { get; }
    public string Gender { get; }
    public int ExpectedColors { get; }
    public string OverallStatus { get; }
    public IReadOnlyDictionary<string, int> CompleteColorsByFamily { get; }
    public string FamilySummary { get; }

    [ObservableProperty] private string reviewStatus;
    [ObservableProperty] private string reviewNote;
    [ObservableProperty] private DateTimeOffset? reviewUpdatedUtc;

    public string DisplayName => $"{Species} - {Age} {Gender}";
    public string StatusLabel => char.ToUpperInvariant(OverallStatus[0]) + OverallStatus[1..];
    public string ReviewStatusLabel => FormatStatusLabel(ReviewStatus);
    public string ReviewNotePreview => string.IsNullOrWhiteSpace(ReviewNote) ? "No note yet." : ReviewNote.Length <= 88 ? ReviewNote : $"{ReviewNote[..85]}...";
    public string ReviewUpdatedLabel => ReviewUpdatedUtc is null ? "Not saved yet." : $"Last saved {ReviewUpdatedUtc.Value.ToLocalTime():MMM d h:mm tt}";
    public string ReviewSummary => $"{ReviewStatusLabel} | {ReviewNotePreview}";
    public bool HasPersistedReview => !ReviewStatus.Equals("unreviewed", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(ReviewNote);

    partial void OnReviewStatusChanged(string value)
    {
        OnPropertyChanged(nameof(ReviewStatusLabel));
        OnPropertyChanged(nameof(ReviewSummary));
    }

    partial void OnReviewNoteChanged(string value)
    {
        OnPropertyChanged(nameof(ReviewNotePreview));
        OnPropertyChanged(nameof(ReviewSummary));
    }

    partial void OnReviewUpdatedUtcChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(ReviewUpdatedLabel));

    private static string FormatStatusLabel(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Unreviewed";
        }

        var spaced = status.Replace('_', ' ');
        return string.Join(" ", spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}

public sealed record FamilyProgressItemViewModel(string Family, int CompleteColors, int ExpectedColors)
{
    public string Progress => $"{CompleteColors}/{ExpectedColors}";
}

public sealed record ViewerFrameItemViewModel(
    string FrameId,
    bool AuthoredExists,
    bool RuntimeExists,
    bool IsCurrent)
{
    public string Marker => IsCurrent ? "Current" : string.Empty;
    public string StateLabel => AuthoredExists ? "Authored present" : "Authored missing";
    public string RuntimeStateLabel => RuntimeExists ? "Runtime present" : "Runtime missing";
}
