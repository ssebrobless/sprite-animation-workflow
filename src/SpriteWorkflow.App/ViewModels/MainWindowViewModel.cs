using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpriteWorkflow.Core;
using SpriteWorkflow.ProjectModel;

namespace SpriteWorkflow.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private static readonly string[] ReviewStatuses = ["unreviewed", "approved", "needs_review", "to_be_repaired", "do_not_use"];
    private static readonly string[] RequestTypes = ["repair_existing", "polish_existing", "new_animation_family", "new_variant", "new_species"];
    private static readonly string[] RequestStatuses = ["draft", "queued", "ready_for_ai", "completed"];
    private readonly List<BaseVariantRowItemViewModel> _allBaseVariants = [];
    private readonly IReadOnlyList<string> _familyOrder = [];
    private readonly Dictionary<string, AnimationSequenceConfig[]> _familySequences = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _previewTimer = new();
    private readonly Action<ProjectReviewData>? _saveReviewData;
    private readonly Action<ProjectRequestData>? _saveRequestData;
    private readonly string _authoredSpriteRoot = string.Empty;
    private readonly string _runtimeSpriteRoot = string.Empty;
    private readonly string _reviewDataPath = string.Empty;
    private readonly string _requestDataPath = string.Empty;
    private readonly string _requestExportDirectory = string.Empty;
    private Bitmap? _currentFrameBitmap;
    private Bitmap? _runtimeFrameBitmap;
    private int _currentFrameIndex;
    private bool _isSynchronizingSelection;

    [ObservableProperty] private string selectedSpeciesFilter = "All species";
    [ObservableProperty] private string selectedFamilyFilter = "All families";
    [ObservableProperty] private string selectedCoverageStatusFilter = "All coverage statuses";
    [ObservableProperty] private string selectedReviewStatusFilter = "All review statuses";
    [ObservableProperty] private BaseVariantRowItemViewModel? selectedBaseVariant;
    [ObservableProperty] private BaseVariantRowItemViewModel? selectedRepairQueueItem;
    [ObservableProperty] private string selectedViewerColor = string.Empty;
    [ObservableProperty] private string selectedViewerFamily = string.Empty;
    [ObservableProperty] private string selectedViewerSequenceId = string.Empty;
    [ObservableProperty] private bool isPlaybackEnabled = true;
    [ObservableProperty] private string viewerStatusMessage = "Select a row to preview authored animation frames.";
    [ObservableProperty] private string viewerFrameSummary = "No frame loaded.";
    [ObservableProperty] private string viewerMissingSummary = string.Empty;
    [ObservableProperty] private string viewerFramePath = string.Empty;
    [ObservableProperty] private string runtimeViewerFrameSummary = "No runtime frame loaded.";
    [ObservableProperty] private string runtimeViewerFramePath = string.Empty;
    [ObservableProperty] private string reviewSaveMessage = "Review changes are saved per project.";
    [ObservableProperty] private RequestItemViewModel? selectedRequestItem;
    [ObservableProperty] private string draftRequestType = "repair_existing";
    [ObservableProperty] private string draftRequestStatus = "draft";
    [ObservableProperty] private string draftRequestTitle = string.Empty;
    [ObservableProperty] private string draftRequestTargetScope = string.Empty;
    [ObservableProperty] private string draftRequestDetails = string.Empty;
    [ObservableProperty] private string draftRequestMustPreserve = string.Empty;
    [ObservableProperty] private string draftRequestMustAvoid = string.Empty;
    [ObservableProperty] private string draftRequestSourceNote = string.Empty;
    [ObservableProperty] private string requestSaveMessage = "Requests are saved per project.";

    public MainWindowViewModel() : this(null, null, null, string.Empty, null, null, string.Empty, null, null, null) { }

    public MainWindowViewModel(
        ProjectConfig? config,
        AssetIndexSnapshot? snapshot,
        ProjectReviewData? reviewData,
        string? reviewDataPath,
        Action<ProjectReviewData>? saveReviewData,
        ProjectRequestData? requestData,
        string? requestDataPath,
        Action<ProjectRequestData>? saveRequestData,
        string? configPath,
        string? loadError)
    {
        AppTitle = "Sprite Workflow App";
        Subtitle = "Cross-platform sprite and animation workflow tool for generation, review, repair, and requests.";
        CurrentMilestone = "Milestone 1 scaffold: project loading, authored coverage summary, asset browser, compare preview, and row-level review notes.";
        NavigationItems = ["Dashboard", "Assets", "Review Queue", "Requests", "Workflow", "Settings"];
        RecentProjects = ["Wevito (sample profile)", "Add another project profile"];
        SpeciesFilterOptions = ["All species"];
        FamilyFilterOptions = ["All families"];
        CoverageStatusFilterOptions = ["All coverage statuses", "complete", "partial", "missing"];
        ReviewStatusFilterOptions = ["All review statuses"];
        ReviewStatusOptions = [];
        RequestTypeOptions = [];
        RequestStatusOptions = [];
        FilteredBaseVariants = [];
        RepairQueueItems = [];
        Requests = [];
        SelectedBaseVariantFamilyProgress = [];
        ViewerColorOptions = [];
        ViewerFamilyOptions = [];
        ViewerSequenceOptions = [];
        ViewerFrames = [];
        FamilyCoverage = [];
        SpeciesCoverage = [];
        WorkflowActions = [];

        foreach (var status in ReviewStatuses)
        {
            ReviewStatusFilterOptions.Add(status);
            ReviewStatusOptions.Add(status);
        }

        foreach (var type in RequestTypes)
        {
            RequestTypeOptions.Add(type);
        }

        foreach (var status in RequestStatuses)
        {
            RequestStatusOptions.Add(status);
        }

        _previewTimer.Interval = TimeSpan.FromMilliseconds(220);
        _previewTimer.Tick += OnPreviewTimerTick;

        if (config is null || snapshot is null || !string.IsNullOrWhiteSpace(loadError))
        {
            SelectedProjectName = "No project loaded";
            SelectedProjectSummary = "The shell is running, but the sample project profile failed to load.";
            SelectedProjectConfigPath = configPath ?? @"sample-projects\wevito.project.json";
            StatusMessage = loadError ?? "Waiting for project configuration.";
            return;
        }

        _saveReviewData = saveReviewData;
        _saveRequestData = saveRequestData;
        _reviewDataPath = reviewDataPath ?? string.Empty;
        _requestDataPath = requestDataPath ?? string.Empty;
        _requestExportDirectory = string.IsNullOrWhiteSpace(_requestDataPath)
            ? string.Empty
            : Path.Combine(Path.GetDirectoryName(_requestDataPath) ?? string.Empty, "exports");
        _familyOrder = config.Families.Keys.OrderBy(family => family).ToList();
        _familySequences = config.Families.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        _authoredSpriteRoot = ResolvePath(config.RootPath, config.AuthoredSpriteRoot);
        _runtimeSpriteRoot = ResolvePath(config.RootPath, config.RuntimeSpriteRoot);

        SelectedProjectName = config.DisplayName;
        SelectedProjectSummary = $"Root: {config.RootPath}";
        SelectedProjectConfigPath = configPath ?? @"sample-projects\wevito.project.json";
        StatusMessage = $"Loaded {config.DisplayName} with {snapshot.ExpectedVariantCount} expected color variants.";
        ExpectedVariantCount = snapshot.ExpectedVariantCount;
        CompleteVariantCount = snapshot.CompleteVariantCount;
        IncompleteVariantCount = snapshot.IncompleteVariantCount;

        FamilyCoverage = new ObservableCollection<FamilyCoverageItemViewModel>(snapshot.FamilyCoverage.Select(summary => new FamilyCoverageItemViewModel(summary.Family, summary.CompleteVariantCount, summary.ExpectedVariantCount)));
        SpeciesCoverage = new ObservableCollection<SpeciesCoverageItemViewModel>(snapshot.SpeciesCoverage.Select(summary => new SpeciesCoverageItemViewModel(summary.Species, summary.ExpectedBaseRows, FormatFamilyRow(summary, "locomotion"), FormatFamilyRow(summary, "care"), FormatFamilyRow(summary, "expression"))));
        WorkflowActions = new ObservableCollection<WorkflowActionItemViewModel>(config.WorkflowActions.Select(action => new WorkflowActionItemViewModel(action.DisplayName, action.Command, string.Join(" ", action.Arguments))));

        foreach (var species in snapshot.BaseVariants.Select(summary => summary.Species).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(species => species))
        {
            SpeciesFilterOptions.Add(species);
        }

        foreach (var family in _familyOrder)
        {
            FamilyFilterOptions.Add(family);
            ViewerFamilyOptions.Add(family);
        }

        foreach (var color in config.VariantAxes.Color)
        {
            ViewerColorOptions.Add(color);
        }

        var reviewLookup = (reviewData?.BaseVariantReviews ?? [])
            .ToDictionary(review => BuildVariantKey(review.Species, review.Age, review.Gender), review => review, StringComparer.OrdinalIgnoreCase);

        _allBaseVariants.AddRange(snapshot.BaseVariants.Select(summary =>
        {
            reviewLookup.TryGetValue(BuildVariantKey(summary.Species, summary.Age, summary.Gender), out var review);
            return new BaseVariantRowItemViewModel(
                summary.Species,
                summary.Age,
                summary.Gender,
                summary.ExpectedColors,
                summary.OverallStatus,
                summary.CompleteColorsByFamily.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                BuildFamilySummary(summary, _familyOrder),
                review?.Status ?? "unreviewed",
                review?.Note ?? string.Empty,
                review?.UpdatedUtc);
        }));

        foreach (var request in (requestData?.Requests ?? []).OrderByDescending(request => request.UpdatedUtc))
        {
            Requests.Add(new RequestItemViewModel(
                request.RequestId,
                request.RequestType,
                request.Status,
                request.Title,
                request.TargetScope,
                request.Details,
                request.MustPreserve,
                request.MustAvoid,
                request.SourceNote,
                request.UpdatedUtc));
        }

        SelectedViewerColor = ViewerColorOptions.Contains("blue") ? "blue" : ViewerColorOptions.FirstOrDefault() ?? string.Empty;
        SelectedViewerFamily = ViewerFamilyOptions.Contains("locomotion") ? "locomotion" : ViewerFamilyOptions.FirstOrDefault() ?? string.Empty;

        ApplyFilters();
        UpdateSequenceOptions(true);
        UpdateViewer();
        _previewTimer.Start();
    }

    public string AppTitle { get; }
    public string Subtitle { get; }
    public string CurrentMilestone { get; }
    public string SelectedProjectName { get; }
    public string SelectedProjectSummary { get; }
    public string SelectedProjectConfigPath { get; }
    public string StatusMessage { get; }
    public int ExpectedVariantCount { get; }
    public int CompleteVariantCount { get; }
    public int IncompleteVariantCount { get; }
    public ObservableCollection<string> NavigationItems { get; }
    public ObservableCollection<string> RecentProjects { get; }
    public ObservableCollection<FamilyCoverageItemViewModel> FamilyCoverage { get; private set; }
    public ObservableCollection<SpeciesCoverageItemViewModel> SpeciesCoverage { get; private set; }
    public ObservableCollection<WorkflowActionItemViewModel> WorkflowActions { get; private set; }
    public ObservableCollection<string> SpeciesFilterOptions { get; }
    public ObservableCollection<string> FamilyFilterOptions { get; }
    public ObservableCollection<string> CoverageStatusFilterOptions { get; }
    public ObservableCollection<string> ReviewStatusFilterOptions { get; }
    public ObservableCollection<string> ReviewStatusOptions { get; }
    public ObservableCollection<string> RequestTypeOptions { get; }
    public ObservableCollection<string> RequestStatusOptions { get; }
    public ObservableCollection<BaseVariantRowItemViewModel> FilteredBaseVariants { get; }
    public ObservableCollection<BaseVariantRowItemViewModel> RepairQueueItems { get; }
    public ObservableCollection<RequestItemViewModel> Requests { get; }
    public ObservableCollection<FamilyProgressItemViewModel> SelectedBaseVariantFamilyProgress { get; }
    public ObservableCollection<string> ViewerColorOptions { get; }
    public ObservableCollection<string> ViewerFamilyOptions { get; }
    public ObservableCollection<string> ViewerSequenceOptions { get; }
    public ObservableCollection<ViewerFrameItemViewModel> ViewerFrames { get; }
    public string AssetBrowserSummary => $"{FilteredBaseVariants.Count} of {_allBaseVariants.Count} base rows";
    public string RepairQueueSummary => $"{RepairQueueItems.Count} queued for repair";
    public string NeedsReviewSummary => $"{_allBaseVariants.Count(row => row.ReviewStatus.Equals("needs_review", StringComparison.OrdinalIgnoreCase))} marked needs review";
    public string RequestSummary => $"{Requests.Count} saved requests";
    public string SelectedBaseVariantTitle => SelectedBaseVariant is null ? "No row selected" : $"{ToTitleCase(SelectedBaseVariant.Species)} - {ToTitleCase(SelectedBaseVariant.Age)} {ToTitleCase(SelectedBaseVariant.Gender)}";
    public string SelectedBaseVariantStatus => SelectedBaseVariant is null ? "Choose a row to inspect its family coverage." : $"{ToTitleCase(SelectedBaseVariant.OverallStatus)} coverage across {SelectedBaseVariant.ExpectedColors} color variants";
    public string SelectedBaseVariantSummary => SelectedBaseVariant?.FamilySummary ?? "Family progress will appear here once a row is selected.";
    public string SelectedBaseVariantReviewSummary => SelectedBaseVariant?.ReviewSummary ?? "Use the review status and notes to mark what still needs work.";
    public string ReviewDataPathDisplay => string.IsNullOrWhiteSpace(_reviewDataPath) ? "No review store configured." : $"Review file: {_reviewDataPath}";
    public string RequestDataPathDisplay => string.IsNullOrWhiteSpace(_requestDataPath) ? "No request store configured." : $"Request file: {_requestDataPath}";
    public string RequestExportPathDisplay => string.IsNullOrWhiteSpace(_requestExportDirectory) ? "No export folder configured." : $"Request exports: {_requestExportDirectory}";
    public string DraftRequestPreview => BuildRequestPreview();

    public Bitmap? CurrentFrameBitmap
    {
        get => _currentFrameBitmap;
        private set => SetBitmap(ref _currentFrameBitmap, value);
    }

    public Bitmap? RuntimeFrameBitmap
    {
        get => _runtimeFrameBitmap;
        private set => SetBitmap(ref _runtimeFrameBitmap, value);
    }

    [RelayCommand]
    private void StepPreviousFrame() => AdvanceFrame(-1);

    [RelayCommand]
    private void StepNextFrame() => AdvanceFrame(1);

    [RelayCommand]
    private void RestartPreview()
    {
        _currentFrameIndex = 0;
        UpdateViewer();
    }

    [RelayCommand]
    private void SaveReview()
    {
        if (SelectedBaseVariant is null || _saveReviewData is null)
        {
            ReviewSaveMessage = "No selected row to save.";
            return;
        }

        try
        {
            SelectedBaseVariant.ReviewUpdatedUtc = DateTimeOffset.UtcNow;
            _saveReviewData(BuildReviewData());
            ReviewSaveMessage = $"Saved review for {SelectedBaseVariant.DisplayName} at {DateTime.Now:h:mm tt}.";
            OnPropertyChanged(nameof(SelectedBaseVariantReviewSummary));
        }
        catch (Exception ex)
        {
            ReviewSaveMessage = $"Unable to save review: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearSelectedReview()
    {
        if (SelectedBaseVariant is null)
        {
            return;
        }

        SelectedBaseVariant.ReviewStatus = "unreviewed";
        SelectedBaseVariant.ReviewNote = string.Empty;
        SelectedBaseVariant.ReviewUpdatedUtc = null;
        ReviewSaveMessage = "Unsaved review changes.";
        OnPropertyChanged(nameof(SelectedBaseVariantReviewSummary));
    }

    [RelayCommand]
    private void MarkSelectedApproved() => SetSelectedReviewStatus("approved");

    [RelayCommand]
    private void MarkSelectedNeedsReview() => SetSelectedReviewStatus("needs_review");

    [RelayCommand]
    private void MarkSelectedToBeRepaired() => SetSelectedReviewStatus("to_be_repaired");

    [RelayCommand]
    private void CreateRequestFromSelected()
    {
        var target = SelectedBaseVariant;
        if (target is null)
        {
            RequestSaveMessage = "Select a row first to create a targeted request.";
            return;
        }

        DraftRequestType = target.ReviewStatus.Equals("to_be_repaired", StringComparison.OrdinalIgnoreCase)
            ? "repair_existing"
            : "polish_existing";
        DraftRequestStatus = "draft";
        DraftRequestTitle = $"{ToTitleCase(target.Species)} {ToTitleCase(target.Age)} {ToTitleCase(target.Gender)} {DraftRequestType.Replace('_', ' ')}";
        DraftRequestTargetScope = $"{target.Species} | {target.Age} | {target.Gender}";
        DraftRequestDetails = target.ReviewNote;
        DraftRequestMustPreserve = $"Preserve established {target.Species} silhouette, anatomy, and trusted family style.";
        DraftRequestMustAvoid = "Do not drift into anthropomorphic posing or off-model proportions.";
        DraftRequestSourceNote = target.ReviewNote;
        RequestSaveMessage = "Draft prefilled from the selected row.";
        OnPropertyChanged(nameof(DraftRequestPreview));
    }

    [RelayCommand]
    private void CreateRequestFromRepairQueue()
    {
        if (SelectedRepairQueueItem is null)
        {
            RequestSaveMessage = "Select a repair-queue row first.";
            return;
        }

        _isSynchronizingSelection = true;
        SelectedBaseVariant = SelectedRepairQueueItem;
        _isSynchronizingSelection = false;
        CreateRequestFromSelected();
    }

    [RelayCommand]
    private void SaveRequest()
    {
        if (_saveRequestData is null)
        {
            RequestSaveMessage = "No request store configured.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DraftRequestTitle) || string.IsNullOrWhiteSpace(DraftRequestTargetScope))
        {
            RequestSaveMessage = "Add a title and target scope before saving the request.";
            return;
        }

        var updatedUtc = DateTimeOffset.UtcNow;
        var selectedId = SelectedRequestItem?.RequestId;
        var request = new RequestItemViewModel(
            selectedId ?? BuildRequestId(DraftRequestType, DraftRequestTargetScope),
            DraftRequestType,
            DraftRequestStatus,
            DraftRequestTitle.Trim(),
            DraftRequestTargetScope.Trim(),
            DraftRequestDetails.Trim(),
            DraftRequestMustPreserve.Trim(),
            DraftRequestMustAvoid.Trim(),
            DraftRequestSourceNote.Trim(),
            updatedUtc);

        var existing = Requests.FirstOrDefault(item => item.RequestId.Equals(request.RequestId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            var index = Requests.IndexOf(existing);
            Requests[index] = request;
        }
        else
        {
            Requests.Insert(0, request);
        }

        PersistRequests();
        SelectedRequestItem = request;
        RequestSaveMessage = $"Saved request '{request.Title}'.";
        OnPropertyChanged(nameof(RequestSummary));
        OnPropertyChanged(nameof(DraftRequestPreview));
    }

    [RelayCommand]
    private void ClearRequestDraft()
    {
        SelectedRequestItem = null;
        DraftRequestType = RequestTypeOptions.FirstOrDefault() ?? "repair_existing";
        DraftRequestStatus = RequestStatusOptions.FirstOrDefault() ?? "draft";
        DraftRequestTitle = string.Empty;
        DraftRequestTargetScope = string.Empty;
        DraftRequestDetails = string.Empty;
        DraftRequestMustPreserve = string.Empty;
        DraftRequestMustAvoid = string.Empty;
        DraftRequestSourceNote = string.Empty;
        RequestSaveMessage = "Cleared request draft.";
        OnPropertyChanged(nameof(DraftRequestPreview));
    }

    [RelayCommand]
    private void ExportDraftRequest()
    {
        if (string.IsNullOrWhiteSpace(DraftRequestTitle) || string.IsNullOrWhiteSpace(DraftRequestTargetScope))
        {
            RequestSaveMessage = "Add a title and target scope before exporting the request.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_requestExportDirectory))
        {
            RequestSaveMessage = "No export folder configured for this project.";
            return;
        }

        try
        {
            Directory.CreateDirectory(_requestExportDirectory);
            var requestId = SelectedRequestItem?.RequestId ?? BuildRequestId(DraftRequestType, DraftRequestTargetScope);
            var exportPath = Path.Combine(_requestExportDirectory, $"{requestId}.txt");
            File.WriteAllText(exportPath, BuildRequestPreview());
            RequestSaveMessage = $"Exported request handoff to {exportPath}.";
        }
        catch (Exception ex)
        {
            RequestSaveMessage = $"Unable to export request: {ex.Message}";
        }
    }

    partial void OnSelectedSpeciesFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedFamilyFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedCoverageStatusFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedReviewStatusFilterChanged(string value) => ApplyFilters();

    partial void OnSelectedViewerColorChanged(string value)
    {
        _currentFrameIndex = 0;
        UpdateViewer();
    }

    partial void OnSelectedViewerFamilyChanged(string value)
    {
        UpdateSequenceOptions(true);
        UpdateViewer();
    }

    partial void OnSelectedViewerSequenceIdChanged(string value)
    {
        _currentFrameIndex = 0;
        UpdateViewer();
    }

    partial void OnIsPlaybackEnabledChanged(bool value)
    {
        if (value)
        {
            _previewTimer.Start();
        }
        else
        {
            _previewTimer.Stop();
        }
    }

    partial void OnDraftRequestTypeChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestStatusChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestTitleChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestTargetScopeChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestDetailsChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestMustPreserveChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestMustAvoidChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestSourceNoteChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));

    partial void OnSelectedBaseVariantChanged(BaseVariantRowItemViewModel? oldValue, BaseVariantRowItemViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= SelectedBaseVariantOnPropertyChanged;
        }

        SelectedBaseVariantFamilyProgress.Clear();

        if (newValue is not null)
        {
            foreach (var family in _familyOrder)
            {
                newValue.CompleteColorsByFamily.TryGetValue(family, out var count);
                SelectedBaseVariantFamilyProgress.Add(new FamilyProgressItemViewModel(family, count, newValue.ExpectedColors));
            }

            newValue.PropertyChanged += SelectedBaseVariantOnPropertyChanged;
        }

        OnPropertyChanged(nameof(SelectedBaseVariantTitle));
        OnPropertyChanged(nameof(SelectedBaseVariantStatus));
        OnPropertyChanged(nameof(SelectedBaseVariantSummary));
        OnPropertyChanged(nameof(SelectedBaseVariantReviewSummary));

        if (!_isSynchronizingSelection)
        {
            _isSynchronizingSelection = true;
            SelectedRepairQueueItem = newValue?.ReviewStatus.Equals("to_be_repaired", StringComparison.OrdinalIgnoreCase) == true
                ? newValue
                : null;
            _isSynchronizingSelection = false;
        }

        UpdateViewer();
    }

    partial void OnSelectedRepairQueueItemChanged(BaseVariantRowItemViewModel? value)
    {
        if (_isSynchronizingSelection || value is null)
        {
            return;
        }

        _isSynchronizingSelection = true;
        SelectedBaseVariant = value;
        _isSynchronizingSelection = false;
    }

    partial void OnSelectedRequestItemChanged(RequestItemViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        DraftRequestType = value.RequestType;
        DraftRequestStatus = value.Status;
        DraftRequestTitle = value.Title;
        DraftRequestTargetScope = value.TargetScope;
        DraftRequestDetails = value.Details;
        DraftRequestMustPreserve = value.MustPreserve;
        DraftRequestMustAvoid = value.MustAvoid;
        DraftRequestSourceNote = value.SourceNote;
        RequestSaveMessage = $"Loaded request '{value.Title}' into the draft editor.";
        OnPropertyChanged(nameof(DraftRequestPreview));
    }

    private void SelectedBaseVariantOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender != SelectedBaseVariant)
        {
            return;
        }

        if (e.PropertyName is nameof(BaseVariantRowItemViewModel.ReviewStatus) or nameof(BaseVariantRowItemViewModel.ReviewNote))
        {
            ReviewSaveMessage = "Unsaved review changes.";
            OnPropertyChanged(nameof(SelectedBaseVariantReviewSummary));
            ApplyFilters();
            RefreshRepairQueue();
        }
    }

    private void SetBitmap(ref Bitmap? target, Bitmap? value)
    {
        if (ReferenceEquals(target, value))
        {
            return;
        }

        var previous = target;
        if (SetProperty(ref target, value))
        {
            previous?.Dispose();
        }
    }

    private static string FormatFamilyRow(SpeciesCoverageSummary summary, string family)
    {
        summary.CompleteBaseRowsByFamily.TryGetValue(family, out var count);
        return $"{count}/{summary.ExpectedBaseRows}";
    }

    private static string BuildFamilySummary(BaseVariantCoverageSummary summary, IReadOnlyList<string> familyOrder)
    {
        return string.Join("  |  ", familyOrder.Select(family =>
        {
            summary.CompleteColorsByFamily.TryGetValue(family, out var count);
            return $"{family} {count}/{summary.ExpectedColors}";
        }));
    }

    private static string BuildVariantKey(string species, string age, string gender) => $"{species}|{age}|{gender}";

    private static string BuildRequestId(string requestType, string targetScope)
    {
        var slug = targetScope
            .ToLowerInvariant()
            .Replace('|', '-')
            .Replace(' ', '-')
            .Replace("--", "-");
        return $"{requestType}-{slug}".Trim('-');
    }

    private static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return string.Join(" ", value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static string ResolvePath(string rootPath, string relativeOrAbsolutePath)
    {
        return Path.IsPathRooted(relativeOrAbsolutePath)
            ? relativeOrAbsolutePath
            : Path.GetFullPath(Path.Combine(rootPath, relativeOrAbsolutePath));
    }

    private string BuildRequestPreview()
    {
        var lines = new List<string>
        {
            $"type: {DraftRequestType}",
            $"status: {DraftRequestStatus}",
            $"target: {DraftRequestTargetScope}",
            $"title: {DraftRequestTitle}",
        };

        if (!string.IsNullOrWhiteSpace(DraftRequestDetails))
        {
            lines.Add($"details: {DraftRequestDetails}");
        }

        if (!string.IsNullOrWhiteSpace(DraftRequestMustPreserve))
        {
            lines.Add($"must_preserve: {DraftRequestMustPreserve}");
        }

        if (!string.IsNullOrWhiteSpace(DraftRequestMustAvoid))
        {
            lines.Add($"must_avoid: {DraftRequestMustAvoid}");
        }

        if (!string.IsNullOrWhiteSpace(DraftRequestSourceNote))
        {
            lines.Add($"source_note: {DraftRequestSourceNote}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void PersistRequests()
    {
        if (_saveRequestData is null)
        {
            return;
        }

        _saveRequestData(
            new ProjectRequestData
            {
                Requests = Requests
                    .Select(
                        request => new ProjectRequestRecord
                        {
                            RequestId = request.RequestId,
                            RequestType = request.RequestType,
                            Status = request.Status,
                            Title = request.Title,
                            TargetScope = request.TargetScope,
                            Details = request.Details,
                            MustPreserve = request.MustPreserve,
                            MustAvoid = request.MustAvoid,
                            SourceNote = request.SourceNote,
                            UpdatedUtc = request.UpdatedUtc,
                        })
                    .ToList(),
            });
    }

    private ProjectReviewData BuildReviewData()
    {
        return new ProjectReviewData
        {
            BaseVariantReviews = _allBaseVariants
                .Where(row => row.HasPersistedReview)
                .Select(row => new BaseVariantReviewRecord
                {
                    Species = row.Species,
                    Age = row.Age,
                    Gender = row.Gender,
                    Status = row.ReviewStatus,
                    Note = row.ReviewNote.Trim(),
                    UpdatedUtc = row.ReviewUpdatedUtc,
                })
                .OrderBy(record => record.Species)
                .ThenBy(record => record.Age)
                .ThenBy(record => record.Gender)
                .ToList(),
        };
    }

    private void OnPreviewTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaybackEnabled)
        {
            return;
        }

        AdvanceFrame(1);
    }

    private void ApplyFilters()
    {
        var filtered = _allBaseVariants
            .Where(row => SelectedSpeciesFilter == "All species" || row.Species.Equals(SelectedSpeciesFilter, StringComparison.OrdinalIgnoreCase))
            .Where(row => SelectedCoverageStatusFilter == "All coverage statuses" || row.OverallStatus.Equals(SelectedCoverageStatusFilter, StringComparison.OrdinalIgnoreCase))
            .Where(row => SelectedReviewStatusFilter == "All review statuses" || row.ReviewStatus.Equals(SelectedReviewStatusFilter, StringComparison.OrdinalIgnoreCase))
            .Where(row =>
            {
                if (SelectedFamilyFilter == "All families")
                {
                    return true;
                }

                row.CompleteColorsByFamily.TryGetValue(SelectedFamilyFilter, out var count);
                return count > 0;
            })
            .ToList();

        FilteredBaseVariants.Clear();
        foreach (var row in filtered)
        {
            FilteredBaseVariants.Add(row);
        }

        if (SelectedBaseVariant is not null && filtered.Contains(SelectedBaseVariant))
        {
            OnPropertyChanged(nameof(AssetBrowserSummary));
            return;
        }

        SelectedBaseVariant = FilteredBaseVariants.FirstOrDefault();
        OnPropertyChanged(nameof(AssetBrowserSummary));
        RefreshRepairQueue();
    }

    private void RefreshRepairQueue()
    {
        var queueItems = _allBaseVariants
            .Where(row => row.ReviewStatus.Equals("to_be_repaired", StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.Species)
            .ThenBy(row => row.Age)
            .ThenBy(row => row.Gender)
            .ToList();

        RepairQueueItems.Clear();
        foreach (var row in queueItems)
        {
            RepairQueueItems.Add(row);
        }

        if (SelectedRepairQueueItem is not null && !queueItems.Contains(SelectedRepairQueueItem))
        {
            _isSynchronizingSelection = true;
            SelectedRepairQueueItem = null;
            _isSynchronizingSelection = false;
        }

        OnPropertyChanged(nameof(RepairQueueSummary));
        OnPropertyChanged(nameof(NeedsReviewSummary));
    }

    private void SetSelectedReviewStatus(string status)
    {
        if (SelectedBaseVariant is null)
        {
            return;
        }

        SelectedBaseVariant.ReviewStatus = status;
    }

    private void UpdateSequenceOptions(bool resetFrameIndex)
    {
        ViewerSequenceOptions.Clear();

        if (string.IsNullOrWhiteSpace(SelectedViewerFamily) || !_familySequences.TryGetValue(SelectedViewerFamily, out var sequences))
        {
            SelectedViewerSequenceId = string.Empty;
            return;
        }

        foreach (var sequence in sequences)
        {
            ViewerSequenceOptions.Add(sequence.SequenceId);
        }

        if (!ViewerSequenceOptions.Contains(SelectedViewerSequenceId))
        {
            SelectedViewerSequenceId = ViewerSequenceOptions.FirstOrDefault() ?? string.Empty;
        }

        if (resetFrameIndex)
        {
            _currentFrameIndex = 0;
        }
    }

    private void AdvanceFrame(int delta)
    {
        var sequence = GetSelectedSequence();
        if (sequence is null || sequence.FrameCount <= 0)
        {
            return;
        }

        _currentFrameIndex = (_currentFrameIndex + delta) % sequence.FrameCount;
        if (_currentFrameIndex < 0)
        {
            _currentFrameIndex += sequence.FrameCount;
        }

        UpdateViewer();
    }

    private AnimationSequenceConfig? GetSelectedSequence()
    {
        if (string.IsNullOrWhiteSpace(SelectedViewerFamily) ||
            string.IsNullOrWhiteSpace(SelectedViewerSequenceId) ||
            !_familySequences.TryGetValue(SelectedViewerFamily, out var sequences))
        {
            return null;
        }

        return sequences.FirstOrDefault(sequence => sequence.SequenceId.Equals(SelectedViewerSequenceId, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateViewer()
    {
        ViewerFrames.Clear();

        if (SelectedBaseVariant is null)
        {
            CurrentFrameBitmap = null;
            RuntimeFrameBitmap = null;
            ViewerStatusMessage = "Select a row to preview authored animation frames.";
            ViewerFrameSummary = "No frame loaded.";
            ViewerMissingSummary = string.Empty;
            ViewerFramePath = string.Empty;
            RuntimeViewerFrameSummary = "No runtime frame loaded.";
            RuntimeViewerFramePath = string.Empty;
            return;
        }

        var sequence = GetSelectedSequence();
        if (sequence is null)
        {
            CurrentFrameBitmap = null;
            RuntimeFrameBitmap = null;
            ViewerStatusMessage = "Choose a family and sequence to inspect this row.";
            ViewerFrameSummary = "No frame loaded.";
            ViewerMissingSummary = string.Empty;
            ViewerFramePath = string.Empty;
            RuntimeViewerFrameSummary = "No runtime frame loaded.";
            RuntimeViewerFramePath = string.Empty;
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedViewerColor))
        {
            CurrentFrameBitmap = null;
            RuntimeFrameBitmap = null;
            ViewerStatusMessage = "Choose a color variant to preview.";
            ViewerFrameSummary = "No frame loaded.";
            ViewerMissingSummary = string.Empty;
            ViewerFramePath = string.Empty;
            RuntimeViewerFrameSummary = "No runtime frame loaded.";
            RuntimeViewerFramePath = string.Empty;
            return;
        }

        var authoredDir = Path.Combine(_authoredSpriteRoot, SelectedBaseVariant.Species, SelectedBaseVariant.Age, SelectedBaseVariant.Gender, SelectedViewerColor);
        var runtimeDir = Path.Combine(_runtimeSpriteRoot, SelectedBaseVariant.Species, SelectedBaseVariant.Age, SelectedBaseVariant.Gender, SelectedViewerColor);
        var currentSlot = ((_currentFrameIndex % sequence.FrameCount) + sequence.FrameCount) % sequence.FrameCount;
        var authoredCount = 0;
        var missingNames = new List<string>();
        string? selectedAuthoredPath = null;
        var selectedFrameId = $"{sequence.SequenceId}_{currentSlot:00}";
        var currentFrameExists = false;

        for (var index = 0; index < sequence.FrameCount; index++)
        {
            var frameId = $"{sequence.SequenceId}_{index:00}";
            var authoredPath = Path.Combine(authoredDir, $"{frameId}.png");
            var runtimePath = Path.Combine(runtimeDir, $"{frameId}.png");
            var authoredExists = File.Exists(authoredPath);
            var runtimeExists = File.Exists(runtimePath);

            if (authoredExists)
            {
                authoredCount++;
            }
            else
            {
                missingNames.Add(frameId);
            }

            if (index == currentSlot)
            {
                selectedAuthoredPath = authoredPath;
                currentFrameExists = authoredExists;
            }

            ViewerFrames.Add(new ViewerFrameItemViewModel(frameId, authoredExists, runtimeExists, index == currentSlot));
        }

        ViewerStatusMessage = $"{ToTitleCase(SelectedBaseVariant.Species)} - {SelectedViewerColor} - {SelectedViewerFamily}/{sequence.SequenceId}";
        ViewerFrameSummary = currentFrameExists ? $"Showing authored {selectedFrameId} ({currentSlot + 1}/{sequence.FrameCount})" : $"Authored frame {selectedFrameId} is missing ({currentSlot + 1}/{sequence.FrameCount})";
        ViewerMissingSummary = missingNames.Count == 0 ? $"All {sequence.FrameCount} authored frames are present." : $"Present {authoredCount}/{sequence.FrameCount}. Missing: {string.Join(", ", missingNames.Take(4))}{(missingNames.Count > 4 ? ", ..." : string.Empty)}";
        ViewerFramePath = selectedAuthoredPath ?? string.Empty;

        var selectedRuntimePath = Path.Combine(runtimeDir, $"{selectedFrameId}.png");
        RuntimeViewerFramePath = selectedRuntimePath;
        RuntimeViewerFrameSummary = File.Exists(selectedRuntimePath) ? $"Runtime {selectedFrameId} is available." : $"Runtime {selectedFrameId} is missing.";

        if (!currentFrameExists || string.IsNullOrWhiteSpace(selectedAuthoredPath))
        {
            CurrentFrameBitmap = null;
        }
        else
        {
            try
            {
                CurrentFrameBitmap = new Bitmap(selectedAuthoredPath);
            }
            catch
            {
                CurrentFrameBitmap = null;
                ViewerFrameSummary = $"Unable to load authored {selectedFrameId}.";
            }
        }

        if (!File.Exists(selectedRuntimePath))
        {
            RuntimeFrameBitmap = null;
            return;
        }

        try
        {
            RuntimeFrameBitmap = new Bitmap(selectedRuntimePath);
        }
        catch
        {
            RuntimeFrameBitmap = null;
            RuntimeViewerFrameSummary = $"Unable to load runtime {selectedFrameId}.";
        }
    }
}
