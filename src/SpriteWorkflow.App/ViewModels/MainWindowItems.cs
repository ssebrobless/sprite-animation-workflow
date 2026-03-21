using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using SixLabors.ImageSharp.PixelFormats;

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

public sealed record PlanningChecklistItemViewModel(
    string Family,
    string SequenceSummary,
    int SequenceCount,
    int FramesPerBaseRow,
    int BaseRows,
    int ColorVariants)
{
    public string SequenceCountLabel => $"{SequenceCount} sequences";
    public string FramesPerBaseRowLabel => $"{FramesPerBaseRow} frames / base row";
    public string BaseRowsLabel => $"{BaseRows} base rows";
    public string ColorVariantsLabel => $"{ColorVariants} color variants";
}

public sealed record PlanningDiagnosticItemViewModel(
    string Title,
    string Summary);

public sealed record PlanningDiscoveryCategoryItemViewModel(
    string Category,
    int Count,
    string Summary)
{
    public string CountLabel => $"{Count}";
}

public sealed record ProjectReadinessItemViewModel(
    string Title,
    string Status,
    string Summary)
{
    public string StatusLabel => string.IsNullOrWhiteSpace(Status)
        ? "Unknown"
        : string.Join(" ", Status.Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}

public sealed record FrameHistoryItemViewModel(
    string Label,
    string FilePath,
    DateTimeOffset Timestamp)
{
    public string TimestampLabel => Timestamp.ToLocalTime().ToString("MMM d h:mm:ss tt");
    public string FileName => System.IO.Path.GetFileName(FilePath);
}

public sealed record PlanningTemplateItemViewModel(
    string TemplateId,
    string Name,
    string SpeciesText,
    string AgeText,
    string GenderText,
    string ColorText,
    string FamilyBlueprintText,
    DateTimeOffset? UpdatedUtc)
{
    public string UpdatedLabel => UpdatedUtc is null ? "Not saved yet." : $"Updated {UpdatedUtc.Value.ToLocalTime():MMM d h:mm tt}";
    public string AxisSummary => $"{SpeciesText}  |  {AgeText}  |  {GenderText}  |  {ColorText}";
    public string BlueprintPreview => FamilyBlueprintText.Length <= 120 ? FamilyBlueprintText : $"{FamilyBlueprintText[..117]}...";
}

public sealed record PlanningAdoptionEntryItemViewModel(
    string VariantSummary,
    string SequenceSummary,
    int ExpectedFrames,
    int ExistingFrames,
    int ExtraFrames)
{
    public string StatusLabel => ExistingFrames >= ExpectedFrames && ExtraFrames == 0
        ? "Complete"
        : ExistingFrames == 0 && ExtraFrames == 0
            ? "Missing"
            : "Partial";
    public string CoverageLabel => $"{ExistingFrames}/{ExpectedFrames} planned";
    public string ExtraLabel => ExtraFrames == 0 ? "No extra files" : $"{ExtraFrames} extra file(s)";
}

public sealed record ActivityLogItemViewModel(
    DateTimeOffset Timestamp,
    string Area,
    string Message)
{
    public string TimestampLabel => Timestamp.ToLocalTime().ToString("h:mm:ss tt");
}

public sealed record LiveOperationStepItemViewModel(
    string OperationId,
    string Label,
    string Status,
    string Summary)
{
    public string StatusLabel => string.IsNullOrWhiteSpace(Status)
        ? "Pending"
        : string.Join(" ", Status.Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}

public sealed record RequestHistoryItemViewModel(
    string EventType,
    string Message,
    DateTimeOffset? UpdatedUtc)
{
    public string EventTypeLabel => FormatLabel(EventType);
    public string UpdatedLabel => UpdatedUtc is null ? "No timestamp" : UpdatedUtc.Value.ToLocalTime().ToString("MMM d h:mm:ss tt");

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

public sealed partial class WorkflowActionItemViewModel : ObservableObject
{
    public WorkflowActionItemViewModel(
        string actionId,
        string name,
        string description,
        string executionMode,
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        ActionId = actionId;
        Name = name;
        Description = description;
        ExecutionMode = string.IsNullOrWhiteSpace(executionMode) ? "hidden_process" : executionMode;
        Command = command;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
        runStatus = CanRunHidden ? "Ready" : "External";
        lastOutputPreview = CanRunHidden
            ? "Run this action from the app to keep the terminal hidden."
            : "This step stays outside the app for now. Use the existing Gemini/browser window when you need generation.";
    }

    public string ActionId { get; }
    public string Name { get; }
    public string Description { get; }
    public string ExecutionMode { get; }
    public string Command { get; }
    public IReadOnlyList<string> Arguments { get; }
    public string WorkingDirectory { get; }
    public string ArgumentsDisplay => string.Join(" ", Arguments);
    public string ExecutionModeLabel => ExecutionMode.Equals("hidden_process", StringComparison.OrdinalIgnoreCase) ? "Hidden app action" : "External step";
    public bool CanRunHidden => ExecutionMode.Equals("hidden_process", StringComparison.OrdinalIgnoreCase);
    public bool CanStop => CanRunHidden;
    public bool IsExternalStep => !CanRunHidden;
    public string ActionHint => CanRunHidden
        ? "Runs as a hidden child process and stops automatically when the app closes."
        : "Keep this in the visible Gemini/browser workflow until the app has a cleaner direct integration.";

    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private string runStatus = "Ready";
    [ObservableProperty] private string lastOutputPreview;
    [ObservableProperty] private DateTimeOffset? lastRunUtc;

    public string StatusLabel => IsRunning ? "Running hidden" : RunStatus;
    public string LastRunLabel => LastRunUtc is null ? "Never run from the app." : $"Last run {LastRunUtc.Value.ToLocalTime():MMM d h:mm tt}";

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(StatusLabel));
    partial void OnRunStatusChanged(string value) => OnPropertyChanged(nameof(StatusLabel));
    partial void OnLastRunUtcChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(LastRunLabel));
}

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
    string HealthSummary,
    IReadOnlyList<RequestHistoryItemViewModel> History,
    DateTimeOffset? UpdatedUtc)
{
    public string StatusLabel => FormatLabel(Status);
    public string TypeLabel => FormatLabel(RequestType);
    public string UpdatedLabel => UpdatedUtc is null ? "Not saved yet." : $"Updated {UpdatedUtc.Value.ToLocalTime():MMM d h:mm tt}";
    public string LatestHistorySummary => History.Count == 0 ? "No activity yet." : $"{History[0].EventTypeLabel}: {History[0].Message}";

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

public sealed record AutomationTaskItemViewModel(
    string RequestId,
    string Title,
    string TargetScope,
    string Status,
    string RequestType,
    string PromptPreview,
    string LatestActivity,
    int LinkedCandidateCount,
    DateTimeOffset? UpdatedUtc)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? RequestId : Title;
    public string StatusLabel => FormatLabel(Status);
    public string TypeLabel => FormatLabel(RequestType);
    public string UpdatedLabel => UpdatedUtc is null ? "Not saved yet." : $"Updated {UpdatedUtc.Value.ToLocalTime():MMM d h:mm tt}";
    public string PromptPreviewSummary => string.IsNullOrWhiteSpace(PromptPreview)
        ? "No prompt preview available."
        : PromptPreview.Length <= 220
            ? PromptPreview
            : $"{PromptPreview[..217]}...";
    public string LatestActivitySummary => string.IsNullOrWhiteSpace(LatestActivity) ? "No automation activity recorded yet." : LatestActivity;
    public string LinkedCandidateSummary => LinkedCandidateCount == 0 ? "No linked candidates yet." : $"{LinkedCandidateCount} linked candidate(s)";

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

public sealed record AiProviderItemViewModel(
    string ProviderId,
    string DisplayName,
    string ProviderKind,
    string ExecutionMode,
    bool SupportsAutomation,
    string Notes,
    bool IsDefault)
{
    public string KindLabel => FormatLabel(ProviderKind);
    public string ExecutionModeLabel => FormatLabel(ExecutionMode);
    public string AutomationLabel => SupportsAutomation ? "Automation-ready" : "Manual handoff";
    public string DefaultLabel => IsDefault ? "Default provider" : "Optional provider";
    public string Summary => string.IsNullOrWhiteSpace(Notes) ? $"{KindLabel} | {AutomationLabel}" : Notes;

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

public sealed partial class CandidateItemViewModel : ObservableObject
{
    public CandidateItemViewModel(
        string candidateId,
        string title,
        string targetScope,
        string sourceType,
        string status,
        string requestId,
        string candidateImagePath,
        string referenceImagePath,
        string targetFramePath,
        string importBackupPath,
        string note,
        DateTimeOffset? updatedUtc)
    {
        CandidateId = candidateId;
        Title = title;
        TargetScope = targetScope;
        SourceType = sourceType;
        this.status = status;
        RequestId = requestId;
        CandidateImagePath = candidateImagePath;
        ReferenceImagePath = referenceImagePath;
        TargetFramePath = targetFramePath;
        ImportBackupPath = importBackupPath;
        this.note = note;
        this.updatedUtc = updatedUtc;
    }

    public string CandidateId { get; }
    public string Title { get; }
    public string TargetScope { get; }
    public string SourceType { get; }
    public string RequestId { get; }
    public string CandidateImagePath { get; }
    public string ReferenceImagePath { get; }
    public string TargetFramePath { get; }
    public string ImportBackupPath { get; private set; }

    [ObservableProperty] private string status;
    [ObservableProperty] private string note;
    [ObservableProperty] private DateTimeOffset? updatedUtc;

    public string StatusLabel => FormatLabel(Status);
    public string SourceTypeLabel => FormatLabel(SourceType);
    public string UpdatedLabel => UpdatedUtc is null ? "Not saved yet." : $"Updated {UpdatedUtc.Value.ToLocalTime():MMM d h:mm tt}";
    public string NotePreview => string.IsNullOrWhiteSpace(Note) ? "No candidate note yet." : Note.Length <= 96 ? Note : $"{Note[..93]}...";
    public string BackupSummary => string.IsNullOrWhiteSpace(ImportBackupPath) ? "No import backup yet." : $"Backup ready at {ImportBackupPath}";
    public bool HasImportBackup => !string.IsNullOrWhiteSpace(ImportBackupPath);

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(StatusLabel));
    partial void OnNoteChanged(string value) => OnPropertyChanged(nameof(NotePreview));
    partial void OnUpdatedUtcChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(UpdatedLabel));

    public void SetImportBackupPath(string path)
    {
        ImportBackupPath = path;
        OnPropertyChanged(nameof(ImportBackupPath));
        OnPropertyChanged(nameof(BackupSummary));
        OnPropertyChanged(nameof(HasImportBackup));
    }

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
    [ObservableProperty] private string frameReviewState = "unreviewed";
    [ObservableProperty] private int reviewedFrameCount;
    [ObservableProperty] private int approvedFrameCount;
    [ObservableProperty] private int flaggedFrameCount;
    [ObservableProperty] private int templateFrameCount;
    [ObservableProperty] private string frameIssueSummary = "No frame tags yet.";

    public string DisplayName => $"{Species} - {Age} {Gender}";
    public string StatusLabel => char.ToUpperInvariant(OverallStatus[0]) + OverallStatus[1..];
    public string ReviewStatusLabel => FormatStatusLabel(ReviewStatus);
    public string ReviewNotePreview => string.IsNullOrWhiteSpace(ReviewNote) ? "No note yet." : ReviewNote.Length <= 88 ? ReviewNote : $"{ReviewNote[..85]}...";
    public string ReviewUpdatedLabel => ReviewUpdatedUtc is null ? "Not saved yet." : $"Last saved {ReviewUpdatedUtc.Value.ToLocalTime():MMM d h:mm tt}";
    public string ReviewSummary => $"{ReviewStatusLabel} | {ReviewNotePreview}";
    public string FrameReviewStateLabel => FormatStatusLabel(FrameReviewState);
    public string FrameReviewSummary => ReviewedFrameCount == 0
        ? "No frame reviews yet."
        : $"{ApprovedFrameCount} approved | {FlaggedFrameCount} flagged | {TemplateFrameCount} template-only";
    public string QualitySummary => $"{ReviewStatusLabel} row review | {FrameReviewStateLabel} frame quality";
    public bool HasPersistedReview => !ReviewStatus.Equals("unreviewed", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(ReviewNote);

    public void UpdateFrameQuality(
        string state,
        int reviewedCount,
        int approvedCount,
        int flaggedCount,
        int templateCount,
        string issueSummary)
    {
        FrameReviewState = string.IsNullOrWhiteSpace(state) ? "unreviewed" : state;
        ReviewedFrameCount = Math.Max(0, reviewedCount);
        ApprovedFrameCount = Math.Max(0, approvedCount);
        FlaggedFrameCount = Math.Max(0, flaggedCount);
        TemplateFrameCount = Math.Max(0, templateCount);
        FrameIssueSummary = string.IsNullOrWhiteSpace(issueSummary) ? "No frame tags yet." : issueSummary;
    }

    partial void OnReviewStatusChanged(string value)
    {
        OnPropertyChanged(nameof(ReviewStatusLabel));
        OnPropertyChanged(nameof(ReviewSummary));
        OnPropertyChanged(nameof(QualitySummary));
    }

    partial void OnReviewNoteChanged(string value)
    {
        OnPropertyChanged(nameof(ReviewNotePreview));
        OnPropertyChanged(nameof(ReviewSummary));
    }

    partial void OnReviewUpdatedUtcChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(ReviewUpdatedLabel));

    partial void OnFrameReviewStateChanged(string value)
    {
        OnPropertyChanged(nameof(FrameReviewStateLabel));
        OnPropertyChanged(nameof(QualitySummary));
    }

    partial void OnReviewedFrameCountChanged(int value) => OnPropertyChanged(nameof(FrameReviewSummary));
    partial void OnApprovedFrameCountChanged(int value) => OnPropertyChanged(nameof(FrameReviewSummary));
    partial void OnFlaggedFrameCountChanged(int value) => OnPropertyChanged(nameof(FrameReviewSummary));
    partial void OnTemplateFrameCountChanged(int value) => OnPropertyChanged(nameof(FrameReviewSummary));

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

public sealed record FrameReviewQueueItemViewModel(
    string Species,
    string Age,
    string Gender,
    string Color,
    string Family,
    string SequenceId,
    int FrameIndex,
    string FrameId,
    string Status,
    string Note,
    IReadOnlyList<string> IssueTags,
    DateTimeOffset? UpdatedUtc)
{
    public string DisplayName => $"{Species} - {Age} {Gender} - {FrameId}";
    public string TargetSummary => $"{Color} | {Family}/{SequenceId}";
    public string StatusLabel => FormatLabel(Status);
    public string NotePreview => string.IsNullOrWhiteSpace(Note) ? "No frame note yet." : Note.Length <= 88 ? Note : $"{Note[..85]}...";
    public string IssueTagSummary => IssueTags.Count == 0 ? "No tags" : string.Join(", ", IssueTags);
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

public sealed class ViewerFrameItemViewModel : IDisposable
{
    public ViewerFrameItemViewModel(
        string frameId,
        int frameIndex,
        bool authoredExists,
        bool runtimeExists,
        bool isCurrent,
        bool isPlaybackFrame,
        string reviewStatus,
        string issueTagSummary,
        Bitmap? thumbnailBitmap)
    {
        FrameId = frameId;
        FrameIndex = frameIndex;
        AuthoredExists = authoredExists;
        RuntimeExists = runtimeExists;
        IsCurrent = isCurrent;
        IsPlaybackFrame = isPlaybackFrame;
        ReviewStatus = string.IsNullOrWhiteSpace(reviewStatus) ? "unreviewed" : reviewStatus;
        IssueTagSummary = issueTagSummary;
        ThumbnailBitmap = thumbnailBitmap;
    }

    public string FrameId { get; }
    public int FrameIndex { get; }
    public bool AuthoredExists { get; }
    public bool RuntimeExists { get; }
    public bool IsCurrent { get; }
    public bool IsPlaybackFrame { get; }
    public string ReviewStatus { get; }
    public string IssueTagSummary { get; }
    public Bitmap? ThumbnailBitmap { get; }
    public string Marker => IsCurrent && IsPlaybackFrame
        ? "Edit + Live"
        : IsCurrent
            ? "Edit"
            : IsPlaybackFrame
                ? "Live"
                : string.Empty;
    public string StateLabel => AuthoredExists ? "Authored present" : "Authored missing";
    public string RuntimeStateLabel => RuntimeExists ? "Runtime present" : "Runtime missing";
    public string ReviewStatusLabel => FormatLabel(ReviewStatus);
    public bool HasIssueTags => !string.IsNullOrWhiteSpace(IssueTagSummary);
    public bool HasThumbnailBitmap => ThumbnailBitmap is not null;
    public bool IsThumbnailMissing => ThumbnailBitmap is null;
    public string ThumbnailLabel => AuthoredExists ? "Authored" : RuntimeExists ? "Runtime" : "Missing";
    public IBrush FrameBorderBrush => IsCurrent
        ? new SolidColorBrush(Color.Parse("#F3C969"))
        : IsPlaybackFrame
            ? new SolidColorBrush(Color.Parse("#7ED7C1"))
            : new SolidColorBrush(Color.Parse("#3C4B5F"));
    public Thickness FrameBorderThickness => IsCurrent || IsPlaybackFrame ? new Thickness(2) : new Thickness(1);

    private static string FormatLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var spaced = value.Replace('_', ' ');
        return string.Join(" ", spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    public void Dispose()
    {
        // Keep viewer thumbnails alive for the session so Avalonia doesn't measure
        // an image source that was disposed mid-layout.
    }
}

public sealed partial class EditorPaletteColorItemViewModel : ObservableObject
{
    public EditorPaletteColorItemViewModel(string hexColor, IBrush brush, bool isSelected)
    {
        HexColor = hexColor;
        Brush = brush;
        this.isSelected = isSelected;
    }

    public string HexColor { get; }
    public IBrush Brush { get; }

    [ObservableProperty] private bool isSelected;

    public string SelectionLabel => IsSelected ? "Selected" : "Palette";

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(SelectionLabel));
}

public sealed record ColorPresetItemViewModel(
    string Label,
    string HexColor,
    IBrush Brush,
    string Summary);

public sealed record ProjectPaletteItemViewModel(
    string PaletteId,
    string Name,
    string ScopeKind,
    string ScopeKey,
    IReadOnlyList<string> Colors,
    DateTimeOffset? UpdatedUtc)
{
    public string UpdatedLabel => UpdatedUtc is null ? "Not saved yet." : $"Updated {UpdatedUtc.Value.ToLocalTime():MMM d h:mm tt}";
    public string ColorSummary => Colors.Count == 0 ? "No colors" : $"{Colors.Count} colors";
    public string PreviewSummary => Colors.Count == 0 ? "Palette is empty." : string.Join(", ", Colors.Take(6)) + (Colors.Count > 6 ? " ..." : string.Empty);
    public string ScopeLabel => ScopeKind.Equals("species", StringComparison.OrdinalIgnoreCase)
        ? $"{FormatLabel(ScopeKey)} palette"
        : "Project palette";

    private static string FormatLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Project";
        }

        var spaced = value.Replace('_', ' ').Replace('-', ' ');
        return string.Join(" ", spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}

public sealed record TrustedExportHistoryItemViewModel(
    string ExportId,
    string ExportDirectory,
    int ApprovedFrameCount,
    int ApprovedRowCount,
    int FlaggedFrameCount,
    DateTimeOffset? ExportedUtc)
{
    public string DisplayName => string.IsNullOrWhiteSpace(ExportId) ? "Trusted export" : ExportId;
    public string Summary => $"{ApprovedFrameCount} approved frame(s) | {ApprovedRowCount} approved row(s) | {FlaggedFrameCount} flagged frame(s)";
    public string UpdatedLabel => ExportedUtc is null ? "No timestamp" : $"Exported {ExportedUtc.Value.ToLocalTime():MMM d h:mm tt}";
}

public sealed record ValidationReportItemViewModel(
    string ReportId,
    string MarkdownPath,
    string JsonPath,
    string Summary,
    DateTimeOffset? GeneratedUtc)
{
    public string DisplayName => string.IsNullOrWhiteSpace(ReportId) ? "Validation report" : ReportId;
    public string UpdatedLabel => GeneratedUtc is null ? "No timestamp" : $"Generated {GeneratedUtc.Value.ToLocalTime():MMM d h:mm tt}";
    public bool HasMarkdownPath => !string.IsNullOrWhiteSpace(MarkdownPath);
    public bool HasJsonPath => !string.IsNullOrWhiteSpace(JsonPath);
}

public sealed record TrustedExportBlockerItemViewModel(
    string ScopeType,
    string Species,
    string Age,
    string Gender,
    string Color,
    string Family,
    string SequenceId,
    int? FrameIndex,
    string FrameId,
    string Reason)
{
    public string DisplayName => ScopeType.Equals("frame", StringComparison.OrdinalIgnoreCase)
        ? $"{Species} | {Age} | {Gender} | {FrameId}"
        : $"{Species} | {Age} | {Gender}";
    public string TargetSummary => ScopeType.Equals("frame", StringComparison.OrdinalIgnoreCase)
        ? $"{Color} | {Family}/{SequenceId}"
        : "Row review blocker";
    public string ScopeLabel => ScopeType.Equals("frame", StringComparison.OrdinalIgnoreCase) ? "Frame Blocker" : "Row Blocker";
}

public sealed record PlanningUnmappedEntryItemViewModel(
    string RelativePath,
    string Species,
    string Age,
    string Gender,
    string Color,
    string Family,
    string SequenceId,
    string Reason)
{
    public string DisplayName => $"{Species} | {Age} | {Gender} | {Color}";
    public string SequenceSummary => string.IsNullOrWhiteSpace(Family) ? "Unmapped asset" : $"{Family} / {SequenceId}";
}

public sealed partial class EditorLayerItemViewModel : ObservableObject
{
    public EditorLayerItemViewModel(int layerId, string name, bool isVisible, bool isSelected, int opacityPercent, bool isLocked)
    {
        LayerId = layerId;
        this.name = name;
        this.isVisible = isVisible;
        this.isSelected = isSelected;
        this.opacityPercent = opacityPercent;
        this.isLocked = isLocked;
    }

    public int LayerId { get; }

    [ObservableProperty] private string name;
    [ObservableProperty] private bool isVisible;
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private int opacityPercent;
    [ObservableProperty] private bool isLocked;
    [ObservableProperty] private int filledPixelCount;
    [ObservableProperty] private Bitmap? thumbnailBitmap;

    public string StatusLabel => $"{(IsVisible ? "Shown" : "Hidden")} | {(IsSelected ? "Active" : "Inactive")} | {(IsLocked ? "Locked" : "Editable")} | {OpacityPercent}%";
    public string RoleLabel => IsSelected
        ? "Active Layer"
        : IsLocked
            ? "Reference Layer"
            : "Paint Layer";
    public string PixelCoverageLabel => FilledPixelCount == 0 ? "Blank layer" : $"{FilledPixelCount} painted px";
    public bool HasThumbnail => ThumbnailBitmap is not null;
    public bool IsThumbnailMissing => ThumbnailBitmap is null;
    public string VisibilityActionLabel => IsVisible ? "Hide" : "Show";
    public string SelectionActionLabel => IsSelected ? "Active" : "Edit";
    public string LockActionLabel => IsLocked ? "Unlock" : "Lock";
    public string MoveUpActionLabel => "Up";
    public string MoveDownActionLabel => "Down";

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(StatusLabel));

    partial void OnIsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(RoleLabel));
        OnPropertyChanged(nameof(VisibilityActionLabel));
    }

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(RoleLabel));
        OnPropertyChanged(nameof(SelectionActionLabel));
    }

    partial void OnOpacityPercentChanged(int value) => OnPropertyChanged(nameof(StatusLabel));

    partial void OnIsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(RoleLabel));
        OnPropertyChanged(nameof(LockActionLabel));
    }

    partial void OnFilledPixelCountChanged(int value) => OnPropertyChanged(nameof(PixelCoverageLabel));
    partial void OnThumbnailBitmapChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasThumbnail));
        OnPropertyChanged(nameof(IsThumbnailMissing));
    }
}

public sealed partial class EditorPixelItemViewModel : ObservableObject
{
    public EditorPixelItemViewModel(int index, int x, int y, string hexColor, IBrush brush, bool isSelected)
    {
        Index = index;
        X = x;
        Y = y;
        this.hexColor = hexColor;
        this.brush = brush;
        this.isSelected = isSelected;
    }

    public int Index { get; }
    public int X { get; }
    public int Y { get; }

    [ObservableProperty] private string hexColor;
    [ObservableProperty] private IBrush brush;
    [ObservableProperty] private bool isSelected;

    public string Label => IsSelected ? $"{X},{Y} selected" : $"{X},{Y}";
    public IBrush BorderBrush => IsSelected ? new SolidColorBrush(Color.Parse("#F3C969")) : new SolidColorBrush(Color.Parse("#D8E3E1"));
    public Thickness BorderThickness => IsSelected ? new Thickness(1.5) : new Thickness(0.5);

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(BorderBrush));
        OnPropertyChanged(nameof(BorderThickness));
    }
}

public sealed record EditorHistoryState(int LayerId, Rgba32[] Pixels);
public sealed record EditorClipboardPixel(int OffsetX, int OffsetY, Rgba32 Color);
public sealed record EditorClipboardState(int Width, int Height, int OriginX, int OriginY, IReadOnlyList<EditorClipboardPixel> Pixels);
public sealed record SelectedPixelState(int X, int Y, Rgba32 Color);
