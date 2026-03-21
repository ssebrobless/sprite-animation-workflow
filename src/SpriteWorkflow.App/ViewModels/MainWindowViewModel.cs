using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SpriteWorkflow.App.Services;
using SpriteWorkflow.Core;
using SpriteWorkflow.Infrastructure;
using SpriteWorkflow.ProjectModel;

namespace SpriteWorkflow.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private static readonly string[] ReviewStatuses = ["unreviewed", "approved", "needs_review", "to_be_repaired", "do_not_use"];
    private static readonly string[] FrameReviewStatuses = ["unreviewed", "approved", "needs_review", "to_be_repaired", "template_only", "do_not_use"];
    private static readonly string[] FrameIssueTags =
    [
        "off_model",
        "stiff_motion",
        "broken_outline",
        "wrong_silhouette",
        "placeholder",
        "anthropomorphic_drift"
    ];
    private static readonly string[] RequestTypes = ["repair_existing", "polish_existing", "new_animation_family", "new_variant", "new_species"];
    private static readonly string[] RequestStatuses = ["draft", "queued", "ready_for_ai", "running", "paused", "completed"];
    private static readonly string[] CandidateStatuses = ["staged", "approved", "rejected", "imported"];
    private readonly List<BaseVariantRowItemViewModel> _allBaseVariants = [];
    private readonly IReadOnlyList<string> _familyOrder = [];
    private readonly Dictionary<string, AnimationSequenceConfig[]> _familySequences = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FrameReviewRecord> _frameReviewLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _previewTimer = new();
    private readonly DispatcherTimer _compareBlinkTimer = new();
    private readonly DispatcherTimer _editorBlinkTimer = new();
    private readonly DispatcherTimer _liveOperationTimer = new();
    private readonly Action<ProjectReviewData>? _saveReviewData;
    private readonly Action<ProjectRequestData>? _saveRequestData;
    private readonly Action<ProjectCandidateData>? _saveCandidateData;
    private readonly IWorkflowProcessRunner? _workflowRunner;
    private readonly string _authoredSpriteRoot = string.Empty;
    private readonly string _runtimeSpriteRoot = string.Empty;
    private readonly string _incomingHandoffRoot = string.Empty;
    private readonly string _reviewDataPath = string.Empty;
    private readonly string _requestDataPath = string.Empty;
    private readonly string _requestExportDirectory = string.Empty;
    private readonly string _candidateDataPath = string.Empty;
    private readonly string _candidateAssetDirectory = string.Empty;
    private readonly string _editorCaptureDirectory = string.Empty;
    private readonly string _frameHistoryDirectory = string.Empty;
    private readonly string _trustedExportDirectory = string.Empty;
    private readonly string _trustedExportHistoryPath = string.Empty;
    private readonly string _projectValidationReportDirectory = string.Empty;
    private readonly string _projectKitDirectory = string.Empty;
    private readonly string _validationSandboxDirectory = string.Empty;
    private readonly string _planningTemplateStorePath = string.Empty;
    private readonly string _projectPaletteStorePath = string.Empty;
    private readonly string _workspaceStatePath = string.Empty;
    private readonly string _projectConfigPath = string.Empty;
    private readonly string _seedPlanningSpeciesText = string.Empty;
    private readonly string _seedPlanningAgeText = string.Empty;
    private readonly string _seedPlanningGenderText = string.Empty;
    private readonly string _seedPlanningColorText = string.Empty;
    private readonly string _seedPlanningFamilyBlueprintText = string.Empty;
    private readonly Stack<EditorHistoryState> _undoHistory = new();
    private readonly Stack<EditorHistoryState> _redoHistory = new();
    private readonly HashSet<int> _selectedEditorIndices = [];
    private readonly List<(int X, int Y)> _lassoSelectionPoints = [];
    private Bitmap? _currentFrameBitmap;
    private Bitmap? _onionSkinBitmap;
    private Bitmap? _runtimeFrameBitmap;
    private Bitmap? _previousFrameReferenceBitmap;
    private Bitmap? _nextFrameReferenceBitmap;
    private Bitmap? _playbackFrameBitmap;
    private Bitmap? _runtimePlaybackFrameBitmap;
    private Bitmap? _selectedCandidateBitmap;
    private Bitmap? _selectedCandidateReferenceBitmap;
    private Bitmap? _editorPreviewBitmap;
    private Bitmap? _editorBaselineBitmap;
    private Bitmap? _editorDiffBitmap;
    private readonly Dictionary<int, Rgba32[]> _editorLayerPixels = [];
    private Rgba32[] _editorPixels = [];
    private Rgba32[] _editorBaselinePixels = [];
    private Rgba32[]? _shapePreviewBasePixels;
    private Rgba32[]? _movePreviewBasePixels;
    private List<SelectedPixelState>? _movePreviewPixels;
    private EditorClipboardState? _editorClipboard;
    private string _loadedEditorFramePath = string.Empty;
    private string _editorSaveFramePath = string.Empty;
    private int _editorWidth;
    private int _editorHeight;
    private int? _selectionAnchorIndex;
    private int? _shapeAnchorIndex;
    private int? _moveAnchorIndex;
    private bool _strokeSnapshotCaptured;
    private bool _isSynchronizingEditorColor;
    private bool _isSynchronizingEditorHsv;
    private bool _isLoadingFrameReview;
    private bool _isSynchronizingFrameQueueSelection;
    private bool _showRuntimeBlinkFrame;
    private bool _showEditorBaselineBlinkFrame;
    private int _currentFrameIndex;
    private int _nextEditorLayerId = 1;
    private int _activeEditorLayerId;
    private bool _isSynchronizingSelection;
    private bool _isRestoringWorkspaceState;
    private int _playbackFrameIndex;
    private readonly HashSet<string> _processedLiveOperationIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<LiveOperationRecord> _pendingLiveOperations = new();

    [ObservableProperty] private string selectedSpeciesFilter = "All species";
    [ObservableProperty] private string selectedFamilyFilter = "All families";
    [ObservableProperty] private string selectedCoverageStatusFilter = "All coverage statuses";
    [ObservableProperty] private string selectedReviewStatusFilter = "All review statuses";
    [ObservableProperty] private BaseVariantRowItemViewModel? selectedBaseVariant;
    [ObservableProperty] private BaseVariantRowItemViewModel? selectedRepairQueueItem;
    [ObservableProperty] private string selectedViewerColor = string.Empty;
    [ObservableProperty] private string selectedViewerFamily = string.Empty;
    [ObservableProperty] private string selectedViewerSequenceId = string.Empty;
    [ObservableProperty] private int selectedWorkspaceTabIndex = 1;
    [ObservableProperty] private int selectedStudioTabIndex = 0;
    [ObservableProperty] private bool isPlaybackEnabled = false;
    [ObservableProperty] private int previewPlaybackMs = 220;
    [ObservableProperty] private bool isOnionSkinEnabled = true;
    [ObservableProperty] private int onionSkinOpacity = 35;
    [ObservableProperty] private bool isBlinkCompareEnabled;
    [ObservableProperty] private string viewerStatusMessage = "Select a row to preview authored animation frames.";
    [ObservableProperty] private string viewerFrameSummary = "No frame loaded.";
    [ObservableProperty] private string viewerMissingSummary = string.Empty;
    [ObservableProperty] private string viewerFramePath = string.Empty;
    [ObservableProperty] private string runtimeViewerFrameSummary = "No runtime frame loaded.";
    [ObservableProperty] private string runtimeViewerFramePath = string.Empty;
    [ObservableProperty] private string auditProgressSummary = "No row selected.";
    [ObservableProperty] private string editorStatusMessage = "Load a frame to edit it in-app.";
    [ObservableProperty] private string selectedEditorTool = "brush";
    [ObservableProperty] private string selectedEditorColorHex = "#FFFFFFFF";
    [ObservableProperty] private string editorLoadedFramePath = string.Empty;
    [ObservableProperty] private int selectedEditorBrushSize = 1;
    [ObservableProperty] private int selectedEditorZoom = 16;
    [ObservableProperty] private string controlMode = "assisted_ai";
    [ObservableProperty] private bool editorMirrorHorizontal;
    [ObservableProperty] private bool editorMirrorVertical;
    [ObservableProperty] private bool editorFillShapes;
    [ObservableProperty] private int editorRed = 255;
    [ObservableProperty] private int editorGreen = 255;
    [ObservableProperty] private int editorBlue = 255;
    [ObservableProperty] private int editorAlpha = 255;
    [ObservableProperty] private int editorHue;
    [ObservableProperty] private int editorSaturation = 100;
    [ObservableProperty] private int editorValue = 100;
    [ObservableProperty] private bool isEditorDirty;
    [ObservableProperty] private bool autoCaptureOnStroke = true;
    [ObservableProperty] private bool autoCaptureOnSave = true;
    [ObservableProperty] private string lastEditorCapturePath = "No capture yet.";
    [ObservableProperty] private string reviewSaveMessage = "Review changes are saved per project.";
    [ObservableProperty] private string selectedFrameReviewStatus = "unreviewed";
    [ObservableProperty] private string selectedFrameReviewNote = string.Empty;
    [ObservableProperty] private string selectedFrameIssueTagsText = string.Empty;
    [ObservableProperty] private DateTimeOffset? selectedFrameReviewUpdatedUtc;
    [ObservableProperty] private string frameReviewSaveMessage = "Frame review changes are saved per project.";
    [ObservableProperty] private string trustedExportMessage = "Export approved frames and trusted rows into a visible implementation bundle when you are ready.";
    [ObservableProperty] private string frameHistoryMessage = "Frame history appears here after you start saving edits.";
    [ObservableProperty] private RequestItemViewModel? selectedRequestItem;
    [ObservableProperty] private AutomationTaskItemViewModel? selectedAutomationTaskItem;
    [ObservableProperty] private CandidateItemViewModel? selectedCandidateItem;
    [ObservableProperty] private FrameReviewQueueItemViewModel? selectedFrameReviewQueueItem;
    [ObservableProperty] private string candidateSaveMessage = "Candidates are staged per project.";
    [ObservableProperty] private string draftRequestType = "repair_existing";
    [ObservableProperty] private string draftRequestStatus = "draft";
    [ObservableProperty] private string draftRequestTitle = string.Empty;
    [ObservableProperty] private string draftRequestTargetScope = string.Empty;
    [ObservableProperty] private string draftRequestDetails = string.Empty;
    [ObservableProperty] private string draftRequestMustPreserve = string.Empty;
    [ObservableProperty] private string draftRequestMustAvoid = string.Empty;
    [ObservableProperty] private string draftRequestSourceNote = string.Empty;
    [ObservableProperty] private string requestSaveMessage = "Requests are saved per project.";
    [ObservableProperty] private string planningSpeciesText = string.Empty;
    [ObservableProperty] private string planningAgeText = string.Empty;
    [ObservableProperty] private string planningGenderText = string.Empty;
    [ObservableProperty] private string planningColorText = string.Empty;
    [ObservableProperty] private string planningFamilyBlueprintText = string.Empty;
    [ObservableProperty] private string planningProjectId = string.Empty;
    [ObservableProperty] private string planningDisplayName = string.Empty;
    [ObservableProperty] private string planningRootPath = string.Empty;
    [ObservableProperty] private string planningExportPath = string.Empty;
    [ObservableProperty] private string planningExportMessage = "Export a starter profile when the project blueprint is ready.";
    [ObservableProperty] private string planningWorkspaceMessage = "Create the starter workspace folders and seed files here when you are ready to begin drawing or importing sprites.";
    [ObservableProperty] private string planningSkeletonMessage = "Create a starter asset skeleton to materialize the authored folder tree and visible checklist manifest for a new sprite project.";
    [ObservableProperty] private string planningFrameGenerationMessage = "Generate blank planned PNG frames when you want the project blueprint to become immediately editable art files.";
    [ObservableProperty] private PlanningTemplateItemViewModel? selectedPlanningTemplateItem;
    [ObservableProperty] private string planningTemplateMessage = "Save a blueprint once and reuse it across future sprite projects.";
    [ObservableProperty] private string planningDiscoveryAdoptionMessage = "Adopt the discovered authored assets when you want the plan to mirror what is already on disk.";
    [ObservableProperty] private ProjectPaletteItemViewModel? selectedProjectPaletteItem;
    [ObservableProperty] private string projectPaletteMessage = "Project palettes let you reuse species- or project-specific color sets across manual and AI-assisted editing.";
    [ObservableProperty] private TrustedExportHistoryItemViewModel? selectedTrustedExportHistoryItem;
    [ObservableProperty] private ValidationReportItemViewModel? selectedValidationReportItem;
    [ObservableProperty] private bool isEditorBlinkCompareEnabled;
    [ObservableProperty] private string projectValidationMessage = "Run validation to see how portable and healthy this linked sprite project is.";
    [ObservableProperty] private TrustedExportBlockerItemViewModel? selectedTrustedExportBlockerItem;
    [ObservableProperty] private PlanningUnmappedEntryItemViewModel? selectedPlanningUnmappedEntryItem;
    [ObservableProperty] private string projectKitMessage = "Export a reusable project kit when you want to move this workflow to another machine or teammate.";
    [ObservableProperty] private string validationSandboxMessage = "Create a blank validation sandbox to prove this app works beyond the linked project.";
    [ObservableProperty] private string liveOperationStatusMessage = "No visible in-app operation has run yet.";
    [ObservableProperty] private string liveOperationProgressSummary = "No visible steps queued.";

    public MainWindowViewModel() : this(null, null, null, string.Empty, null, null, string.Empty, null, null, string.Empty, null, null, null, null) { }

    public MainWindowViewModel(
        ProjectConfig? config,
        AssetIndexSnapshot? snapshot,
        ProjectReviewData? reviewData,
        string? reviewDataPath,
        Action<ProjectReviewData>? saveReviewData,
        ProjectRequestData? requestData,
        string? requestDataPath,
        Action<ProjectRequestData>? saveRequestData,
        ProjectCandidateData? candidateData,
        string? candidateDataPath,
        Action<ProjectCandidateData>? saveCandidateData,
        IWorkflowProcessRunner? workflowRunner,
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
        FrameReviewStatusOptions = [];
        FrameIssueTagOptions = [];
        RequestTypeOptions = [];
        RequestStatusOptions = [];
        CandidateStatusOptions = [];
        ControlModeOptions = ["assisted_ai", "manual"];
        FilteredBaseVariants = [];
        RepairQueueItems = [];
        FrameReviewQueueItems = [];
        Requests = [];
        AutomationQueueItems = [];
        Candidates = [];
        SelectedBaseVariantFamilyProgress = [];
        ActivityLog = [];
        LiveOperationSteps = [];
        EditorToolOptions = [];
        EditorBrushSizeOptions = [];
        EditorZoomOptions = [];
        _liveOperationTimer.Interval = TimeSpan.FromMilliseconds(700);
        _liveOperationTimer.Tick += (_, _) => ProcessNextLiveOperation();
        EditorPalette = [];
        EditorHuePresets = [];
        EditorTonePresets = [];
        SavedEditorPalette = [];
        EditorLayers = [];
        EditorPixels = [];
        FrameHistoryItems = [];
        ViewerColorOptions = [];
        ViewerFamilyOptions = [];
        ViewerSequenceOptions = [];
        ViewerFrames = [];
        FamilyCoverage = [];
        SpeciesCoverage = [];
        PlanningChecklist = [];
        PlanningDiagnostics = [];
        PlanningTemplates = [];
        ProjectPalettes = [];
        AiProviders = [];
        TrustedExportHistoryItems = [];
        ValidationReports = [];
        PlanningAdoptionEntries = [];
        PlanningDiscoveryCategories = [];
        TrustedExportBlockers = [];
        PlanningUnmappedEntries = [];
        ProjectReadinessItems = [];
        WorkflowActions = [];

        foreach (var status in ReviewStatuses)
        {
            ReviewStatusFilterOptions.Add(status);
            ReviewStatusOptions.Add(status);
        }

        foreach (var status in FrameReviewStatuses)
        {
            FrameReviewStatusOptions.Add(status);
        }

        foreach (var tag in FrameIssueTags)
        {
            FrameIssueTagOptions.Add(tag);
        }

        foreach (var type in RequestTypes)
        {
            RequestTypeOptions.Add(type);
        }

        foreach (var status in RequestStatuses)
        {
            RequestStatusOptions.Add(status);
        }

        foreach (var status in CandidateStatuses)
        {
            CandidateStatusOptions.Add(status);
        }

        foreach (var tool in new[] { "brush", "dropper", "erase", "fill", "select", "lasso", "move", "line", "rectangle", "ellipse" })
        {
            EditorToolOptions.Add(tool);
        }

        foreach (var brushSize in new[] { 1, 2, 3, 4, 5 })
        {
            EditorBrushSizeOptions.Add(brushSize);
        }

        foreach (var zoom in new[] { 8, 12, 16, 20, 24, 32 })
        {
            EditorZoomOptions.Add(zoom);
        }

        _previewTimer.Interval = TimeSpan.FromMilliseconds(PreviewPlaybackMs);
        _previewTimer.Tick += OnPreviewTimerTick;
        _compareBlinkTimer.Interval = TimeSpan.FromMilliseconds(350);
        _compareBlinkTimer.Tick += OnCompareBlinkTimerTick;
        _editorBlinkTimer.Interval = TimeSpan.FromMilliseconds(300);
        _editorBlinkTimer.Tick += OnEditorBlinkTimerTick;
        RebuildEditorQuickColorPresets();

        if (config is null || snapshot is null || !string.IsNullOrWhiteSpace(loadError))
        {
            SelectedProjectName = "No project loaded";
            SelectedProjectSummary = "The shell is running, but the sample project profile failed to load.";
            SelectedProjectConfigPath = configPath ?? @"sample-projects\wevito.project.json";
            StatusMessage = loadError ?? "Waiting for project configuration.";
            PlanningProjectId = "new-sprite-project";
            PlanningDisplayName = "New Sprite Project";
            PlanningRootPath = @"C:\path\to\project";
            PlanningExportPath = Path.Combine(AppContext.BaseDirectory, "artifacts", "exported-project-profiles", "new-sprite-project.project.json");
            PlanningSpeciesText = "hero, enemy, prop";
            PlanningAgeText = "adult";
            PlanningGenderText = "default";
            PlanningColorText = "base";
            PlanningFamilyBlueprintText = "locomotion: idle x4, walk x6\nexpression: happy x4, sad x2";
            RebuildPlanningChecklist();
            return;
        }

        _saveReviewData = saveReviewData;
        _saveRequestData = saveRequestData;
        _saveCandidateData = saveCandidateData;
        _workflowRunner = workflowRunner;
        _projectConfigPath = configPath ?? string.Empty;
        _reviewDataPath = reviewDataPath ?? string.Empty;
        _requestDataPath = requestDataPath ?? string.Empty;
        _requestExportDirectory = string.IsNullOrWhiteSpace(_requestDataPath)
            ? string.Empty
            : Path.Combine(Path.GetDirectoryName(_requestDataPath) ?? string.Empty, "exports");
        _candidateDataPath = candidateDataPath ?? string.Empty;
        _candidateAssetDirectory = string.IsNullOrWhiteSpace(_candidateDataPath)
            ? Path.Combine(config.RootPath, ".sprite-workflow", "candidate-staging")
            : Path.Combine(Path.GetDirectoryName(_candidateDataPath) ?? string.Empty, "candidate-staging");
        _editorCaptureDirectory = !string.IsNullOrWhiteSpace(_reviewDataPath)
            ? Path.Combine(Path.GetDirectoryName(_reviewDataPath) ?? string.Empty, "editor-captures")
            : Path.Combine(config.RootPath, ".sprite-workflow", "editor-captures");
        _frameHistoryDirectory = !string.IsNullOrWhiteSpace(_reviewDataPath)
            ? Path.Combine(Path.GetDirectoryName(_reviewDataPath) ?? string.Empty, "frame-history")
            : Path.Combine(config.RootPath, ".sprite-workflow", "frame-history");
        _trustedExportDirectory = !string.IsNullOrWhiteSpace(_reviewDataPath)
            ? Path.Combine(Path.GetDirectoryName(_reviewDataPath) ?? string.Empty, "trusted-exports")
            : Path.Combine(config.RootPath, ".sprite-workflow", "trusted-exports");
        _trustedExportHistoryPath = !string.IsNullOrWhiteSpace(_reviewDataPath)
            ? Path.Combine(Path.GetDirectoryName(_reviewDataPath) ?? string.Empty, "trusted-export-history.json")
            : Path.Combine(config.RootPath, ".sprite-workflow", "trusted-export-history.json");
        _projectValidationReportDirectory = !string.IsNullOrWhiteSpace(_reviewDataPath)
            ? Path.Combine(Path.GetDirectoryName(_reviewDataPath) ?? string.Empty, "validation-reports")
            : Path.Combine(config.RootPath, ".sprite-workflow", "validation-reports");
        _projectKitDirectory = !string.IsNullOrWhiteSpace(_reviewDataPath)
            ? Path.Combine(Path.GetDirectoryName(_reviewDataPath) ?? string.Empty, "project-kits")
            : Path.Combine(config.RootPath, ".sprite-workflow", "project-kits");
        _validationSandboxDirectory = !string.IsNullOrWhiteSpace(_reviewDataPath)
            ? Path.Combine(Path.GetDirectoryName(_reviewDataPath) ?? string.Empty, "validation-sandboxes")
            : Path.Combine(config.RootPath, ".sprite-workflow", "validation-sandboxes");
        _planningTemplateStorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpriteWorkflowApp",
            "planning-templates.json");
        _projectPaletteStorePath = !string.IsNullOrWhiteSpace(_reviewDataPath)
            ? Path.Combine(Path.GetDirectoryName(_reviewDataPath) ?? string.Empty, "project-palettes.json")
            : Path.Combine(config.RootPath, ".sprite-workflow", "project-palettes.json");
        _workspaceStatePath = !string.IsNullOrWhiteSpace(_reviewDataPath)
            ? Path.Combine(Path.GetDirectoryName(_reviewDataPath) ?? string.Empty, "workspace-state.json")
            : Path.Combine(config.RootPath, ".sprite-workflow", "workspace-state.json");
        _familyOrder = config.Families.Keys.OrderBy(family => family).ToList();
        _familySequences = config.Families.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        _authoredSpriteRoot = ResolvePath(config.RootPath, config.AuthoredSpriteRoot);
        _runtimeSpriteRoot = ResolvePath(config.RootPath, config.RuntimeSpriteRoot);
        _incomingHandoffRoot = ResolvePath(config.RootPath, config.IncomingHandoffRoot);
        _seedPlanningSpeciesText = string.Join(", ", config.VariantAxes.Species);
        _seedPlanningAgeText = string.Join(", ", config.VariantAxes.Age);
        _seedPlanningGenderText = string.Join(", ", config.VariantAxes.Gender);
        _seedPlanningColorText = string.Join(", ", config.VariantAxes.Color);
        _seedPlanningFamilyBlueprintText = BuildFamilyBlueprintText(config.Families);
        PlanningSpeciesText = _seedPlanningSpeciesText;
        PlanningAgeText = _seedPlanningAgeText;
        PlanningGenderText = _seedPlanningGenderText;
        PlanningColorText = _seedPlanningColorText;
        PlanningFamilyBlueprintText = _seedPlanningFamilyBlueprintText;
        RebuildPlanningChecklist();

        SelectedProjectName = config.DisplayName;
        SelectedProjectSummary = $"Root: {config.RootPath}";
        SelectedProjectConfigPath = configPath ?? @"sample-projects\wevito.project.json";
        PlanningProjectId = config.ProjectId;
        PlanningDisplayName = config.DisplayName;
        PlanningRootPath = config.RootPath;
        PlanningExportPath = string.IsNullOrWhiteSpace(_projectConfigPath)
            ? Path.Combine(config.RootPath, $"{config.ProjectId}.project.json")
            : _projectConfigPath;
        StatusMessage = $"Loaded {config.DisplayName} with {snapshot.ExpectedVariantCount} expected color variants.";
        ExpectedVariantCount = snapshot.ExpectedVariantCount;
        CompleteVariantCount = snapshot.CompleteVariantCount;
        IncompleteVariantCount = snapshot.IncompleteVariantCount;

        FamilyCoverage = new ObservableCollection<FamilyCoverageItemViewModel>(snapshot.FamilyCoverage.Select(summary => new FamilyCoverageItemViewModel(summary.Family, summary.CompleteVariantCount, summary.ExpectedVariantCount)));
        SpeciesCoverage = new ObservableCollection<SpeciesCoverageItemViewModel>(snapshot.SpeciesCoverage.Select(summary => new SpeciesCoverageItemViewModel(summary.Species, summary.ExpectedBaseRows, FormatFamilyRow(summary, "locomotion"), FormatFamilyRow(summary, "care"), FormatFamilyRow(summary, "expression"))));
        WorkflowActions = new ObservableCollection<WorkflowActionItemViewModel>(config.WorkflowActions.Select(action => new WorkflowActionItemViewModel(
            action.ActionId,
            action.DisplayName,
            action.Description,
            action.ExecutionMode,
            action.Command,
            action.Arguments,
            ResolvePath(config.RootPath, action.WorkingDirectory))));
        AiProviders = new ObservableCollection<AiProviderItemViewModel>(config.AiProviders.Select(provider => new AiProviderItemViewModel(
            provider.ProviderId,
            provider.DisplayName,
            provider.ProviderKind,
            provider.ExecutionMode,
            provider.SupportsAutomation,
            provider.Notes,
            provider.ProviderId.Equals(config.DefaultAiProviderId, StringComparison.OrdinalIgnoreCase))));

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

        _allBaseVariants.AddRange(snapshot.BaseVariants.Select(summary =>
        {
            return new BaseVariantRowItemViewModel(
                summary.Species,
                summary.Age,
                summary.Gender,
                summary.ExpectedColors,
                summary.OverallStatus,
                summary.CompleteColorsByFamily.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                BuildFamilySummary(summary, _familyOrder),
                "unreviewed",
                string.Empty,
                null);
        }));

        ApplyReviewData(reviewData);
        ApplyRequestData(requestData);
        ApplyCandidateData(candidateData);

        AddActivity("project", $"Loaded {config.DisplayName}.");

        SelectedViewerColor = ViewerColorOptions.Contains("blue") ? "blue" : ViewerColorOptions.FirstOrDefault() ?? string.Empty;
        SelectedViewerFamily = ViewerFamilyOptions.Contains("locomotion") ? "locomotion" : ViewerFamilyOptions.FirstOrDefault() ?? string.Empty;

        ApplyFilters();
        RefreshFrameReviewQueue();
        RefreshTrustedExportBlockers();
        RefreshVariantFrameQuality();
        RefreshPlanningDiagnostics();
        LoadPlanningTemplates();
        LoadProjectPalettes();
        LoadTrustedExportHistory();
        LoadValidationReports();
        UpdateSequenceOptions(true);
        UpdateViewer();
        TryRestoreWorkspaceState();
        if (IsPlaybackEnabled)
        {
            _previewTimer.Start();
        }

        NotifyCurrentTaskChanged();
    }

    public void ReloadExternalReviewData(ProjectReviewData? reviewData)
    {
        var selectedVariantKey = SelectedBaseVariant is null
            ? string.Empty
            : BuildVariantKey(SelectedBaseVariant.Species, SelectedBaseVariant.Age, SelectedBaseVariant.Gender);
        var selectedFrameQueueKey = SelectedFrameReviewQueueItem is null
            ? string.Empty
            : BuildFrameReviewKey(
                SelectedFrameReviewQueueItem.Species,
                SelectedFrameReviewQueueItem.Age,
                SelectedFrameReviewQueueItem.Gender,
                SelectedFrameReviewQueueItem.Color,
                SelectedFrameReviewQueueItem.Family,
                SelectedFrameReviewQueueItem.SequenceId,
                SelectedFrameReviewQueueItem.FrameIndex);

        ApplyReviewData(reviewData);

        if (!string.IsNullOrWhiteSpace(selectedVariantKey))
        {
            var matchingVariant = _allBaseVariants.FirstOrDefault(row =>
                BuildVariantKey(row.Species, row.Age, row.Gender).Equals(selectedVariantKey, StringComparison.OrdinalIgnoreCase));
            if (matchingVariant is not null && !ReferenceEquals(SelectedBaseVariant, matchingVariant))
            {
                SelectedBaseVariant = matchingVariant;
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedFrameQueueKey))
        {
            var matchingFrame = FrameReviewQueueItems.FirstOrDefault(item =>
                BuildFrameReviewKey(item.Species, item.Age, item.Gender, item.Color, item.Family, item.SequenceId, item.FrameIndex)
                    .Equals(selectedFrameQueueKey, StringComparison.OrdinalIgnoreCase));
            if (matchingFrame is not null && !ReferenceEquals(SelectedFrameReviewQueueItem, matchingFrame))
            {
                SelectedFrameReviewQueueItem = matchingFrame;
            }
        }

        LoadCurrentFrameReview();
        RefreshPlanningDiagnostics();
        RefreshProjectReadiness();
        NotifyCurrentTaskChanged();
        FocusCurrentTask(false, true);
        AddActivity("project", "Reloaded review data from disk.");
    }

    public void ReloadExternalRequestData(ProjectRequestData? requestData)
    {
        var selectedRequestId = SelectedRequestItem?.RequestId ?? SelectedAutomationTaskItem?.RequestId ?? string.Empty;
        ApplyRequestData(requestData);

        if (!string.IsNullOrWhiteSpace(selectedRequestId))
        {
            var matchingRequest = Requests.FirstOrDefault(request => request.RequestId.Equals(selectedRequestId, StringComparison.OrdinalIgnoreCase));
            if (matchingRequest is not null && !Equals(SelectedRequestItem, matchingRequest))
            {
                SelectedRequestItem = matchingRequest;
            }
        }

        RefreshPlanningDiagnostics();
        RefreshProjectReadiness();
        NotifyCurrentTaskChanged();
        FocusCurrentTask(false, true);
        AddActivity("project", "Reloaded request data from disk.");
    }

    public void ReloadExternalCandidateData(ProjectCandidateData? candidateData)
    {
        var selectedCandidateId = SelectedCandidateItem?.CandidateId ?? string.Empty;
        ApplyCandidateData(candidateData);

        if (!string.IsNullOrWhiteSpace(selectedCandidateId))
        {
            var matchingCandidate = Candidates.FirstOrDefault(candidate => candidate.CandidateId.Equals(selectedCandidateId, StringComparison.OrdinalIgnoreCase));
            if (matchingCandidate is not null && !ReferenceEquals(SelectedCandidateItem, matchingCandidate))
            {
                SelectedCandidateItem = matchingCandidate;
            }
        }

        RefreshPlanningDiagnostics();
        RefreshProjectReadiness();
        NotifyCurrentTaskChanged();
        FocusCurrentTask(false, true);
        AddActivity("project", "Reloaded candidate data from disk.");
    }

    public void ApplyLiveOperationLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            LiveOperationRecord? operation;
            try
            {
                operation = JsonSerializer.Deserialize<LiveOperationRecord>(line, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
            }
            catch (Exception ex)
            {
                AddActivity("session", $"Skipped invalid live operation: {ex.Message}");
                continue;
            }

            if (operation is null)
            {
                continue;
            }

            NormalizeLiveOperation(operation);
            if (string.IsNullOrWhiteSpace(operation.Op))
            {
                AddActivity("session", "Skipped live operation because no action/op was provided.");
                continue;
            }

            var operationId = string.IsNullOrWhiteSpace(operation.Id)
                ? $"{operation.Op}|{operation.RequestId}|{operation.Species}|{operation.Age}|{operation.Gender}|{operation.Color}|{operation.Family}|{operation.SequenceId}|{operation.FrameIndex}|{operation.X}|{operation.Y}|{operation.ColorHex}|{operation.Tool}"
                : operation.Id;
            if (!_processedLiveOperationIds.Add(operationId))
            {
                continue;
            }

            LiveOperationSteps.Add(new LiveOperationStepItemViewModel(
                operationId,
                BuildLiveOperationLabel(operation),
                "queued",
                BuildLiveOperationSummary(operation)));
            _pendingLiveOperations.Enqueue(operation);
        }

        if (_pendingLiveOperations.Count > 0 && !_liveOperationTimer.IsEnabled)
        {
            LiveOperationStatusMessage = $"Queued {_pendingLiveOperations.Count} visible in-app step(s).";
            LiveOperationProgressSummary = $"{_pendingLiveOperations.Count} step(s) queued.";
            ProcessNextLiveOperation();
        }
    }

    private void ProcessNextLiveOperation()
    {
        if (_pendingLiveOperations.Count == 0)
        {
            _liveOperationTimer.Stop();
            return;
        }

        var operation = _pendingLiveOperations.Dequeue();
        UpdateLiveOperationStep(operation, "running", BuildLiveOperationSummary(operation));
        LiveOperationProgressSummary = $"{LiveOperationSteps.Count(step => step.Status.Equals("done", StringComparison.OrdinalIgnoreCase))} done / {LiveOperationSteps.Count} total";
        try
        {
            ExecuteLiveOperation(operation);
            UpdateLiveOperationStep(operation, "done", LiveOperationStatusMessage);
        }
        catch (Exception ex)
        {
            LiveOperationStatusMessage = $"Visible op '{operation.Op}' failed: {ex.Message}";
            AddActivity("session", $"Visible op '{operation.Op}' failed: {ex.Message}");
            UpdateLiveOperationStep(operation, "failed", ex.Message);
        }

        if (_pendingLiveOperations.Count == 0)
        {
            _liveOperationTimer.Stop();
            LiveOperationProgressSummary = $"{LiveOperationSteps.Count(step => step.Status.Equals("done", StringComparison.OrdinalIgnoreCase))} done / {LiveOperationSteps.Count} total";
            return;
        }

        _liveOperationTimer.Stop();
        _liveOperationTimer.Start();
    }

    private string BuildLiveOperationLabel(LiveOperationRecord operation)
    {
        var op = string.IsNullOrWhiteSpace(operation.Op) ? "step" : operation.Op;
        return string.Join(" ", op.Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private string BuildLiveOperationSummary(LiveOperationRecord operation)
    {
        return operation.Op switch
        {
            "focus_frame" => $"{operation.Species} / {operation.Age} / {operation.Gender} / {operation.Family} / {operation.SequenceId} / {operation.FrameId}",
            "paint_pixel" or "erase_pixel" => $"{operation.X},{operation.Y}",
            "set_color" => operation.ColorHex,
            "set_tool" => operation.Tool,
            _ => string.IsNullOrWhiteSpace(operation.FrameId) ? operation.Op : operation.FrameId
        };
    }

    private void UpdateLiveOperationStep(LiveOperationRecord operation, string status, string summary)
    {
        var operationId = string.IsNullOrWhiteSpace(operation.Id)
            ? $"{operation.Op}|{operation.RequestId}|{operation.Species}|{operation.Age}|{operation.Gender}|{operation.Color}|{operation.Family}|{operation.SequenceId}|{operation.FrameIndex}|{operation.X}|{operation.Y}|{operation.ColorHex}|{operation.Tool}"
            : operation.Id;
        var index = LiveOperationSteps.ToList().FindIndex(item => item.OperationId.Equals(operationId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        LiveOperationSteps[index] = new LiveOperationStepItemViewModel(
            operationId,
            BuildLiveOperationLabel(operation),
            status,
            summary);
    }

    private static void NormalizeLiveOperation(LiveOperationRecord operation)
    {
        if (!string.IsNullOrWhiteSpace(operation.FrameId))
        {
            var frameValue = operation.FrameId.Trim();
            var underscoreIndex = frameValue.LastIndexOf('_');
            if (underscoreIndex > 0 && underscoreIndex < frameValue.Length - 1)
            {
                if (string.IsNullOrWhiteSpace(operation.SequenceId))
                {
                    operation.SequenceId = frameValue[..underscoreIndex];
                }

                if (operation.FrameIndex is null &&
                    int.TryParse(frameValue[(underscoreIndex + 1)..], out var parsedFrameIndex))
                {
                    operation.FrameIndex = parsedFrameIndex;
                }
            }
        }
    }

    [RelayCommand]
    private void FocusCurrentTask()
    {
        FocusCurrentTask(true, false);
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
    public ObservableCollection<string> FrameReviewStatusOptions { get; }
    public ObservableCollection<string> FrameIssueTagOptions { get; }
    public ObservableCollection<string> RequestTypeOptions { get; }
    public ObservableCollection<string> RequestStatusOptions { get; }
    public ObservableCollection<string> CandidateStatusOptions { get; }
    public ObservableCollection<string> ControlModeOptions { get; }
    public ObservableCollection<BaseVariantRowItemViewModel> FilteredBaseVariants { get; }
    public ObservableCollection<BaseVariantRowItemViewModel> RepairQueueItems { get; }
    public ObservableCollection<FrameReviewQueueItemViewModel> FrameReviewQueueItems { get; }
    public ObservableCollection<RequestItemViewModel> Requests { get; }
    public ObservableCollection<AutomationTaskItemViewModel> AutomationQueueItems { get; }
    public ObservableCollection<CandidateItemViewModel> Candidates { get; }
    public ObservableCollection<FamilyProgressItemViewModel> SelectedBaseVariantFamilyProgress { get; }
    public ObservableCollection<ActivityLogItemViewModel> ActivityLog { get; }
    public ObservableCollection<LiveOperationStepItemViewModel> LiveOperationSteps { get; }
    public ObservableCollection<string> EditorToolOptions { get; }
    public ObservableCollection<int> EditorBrushSizeOptions { get; }
    public ObservableCollection<int> EditorZoomOptions { get; }
    public ObservableCollection<EditorPaletteColorItemViewModel> EditorPalette { get; }
    public ObservableCollection<ColorPresetItemViewModel> EditorHuePresets { get; }
    public ObservableCollection<ColorPresetItemViewModel> EditorTonePresets { get; }
    public ObservableCollection<EditorPaletteColorItemViewModel> SavedEditorPalette { get; }
    public ObservableCollection<EditorLayerItemViewModel> EditorLayers { get; }
    public ObservableCollection<EditorPixelItemViewModel> EditorPixels { get; }
    public ObservableCollection<FrameHistoryItemViewModel> FrameHistoryItems { get; }
    public ObservableCollection<string> ViewerColorOptions { get; }
    public ObservableCollection<string> ViewerFamilyOptions { get; }
    public ObservableCollection<string> ViewerSequenceOptions { get; }
    public ObservableCollection<ViewerFrameItemViewModel> ViewerFrames { get; }
    public ObservableCollection<PlanningChecklistItemViewModel> PlanningChecklist { get; }
    public ObservableCollection<PlanningDiagnosticItemViewModel> PlanningDiagnostics { get; }
    public ObservableCollection<PlanningTemplateItemViewModel> PlanningTemplates { get; }
    public ObservableCollection<ProjectPaletteItemViewModel> ProjectPalettes { get; }
    public ObservableCollection<AiProviderItemViewModel> AiProviders { get; private set; }
    public ObservableCollection<TrustedExportHistoryItemViewModel> TrustedExportHistoryItems { get; }
    public ObservableCollection<ValidationReportItemViewModel> ValidationReports { get; }
    public ObservableCollection<PlanningAdoptionEntryItemViewModel> PlanningAdoptionEntries { get; }
    public ObservableCollection<PlanningDiscoveryCategoryItemViewModel> PlanningDiscoveryCategories { get; }
    public ObservableCollection<TrustedExportBlockerItemViewModel> TrustedExportBlockers { get; }
    public ObservableCollection<PlanningUnmappedEntryItemViewModel> PlanningUnmappedEntries { get; }
    public ObservableCollection<ProjectReadinessItemViewModel> ProjectReadinessItems { get; }
    public string AssetBrowserSummary => $"{FilteredBaseVariants.Count} of {_allBaseVariants.Count} base rows";
    public string RepairQueueSummary => $"{RepairQueueItems.Count} queued for repair";
    public string FrameReviewQueueSummary => $"{FrameReviewQueueItems.Count} flagged frame reviews";
    public string NeedsReviewSummary => $"{_allBaseVariants.Count(row => row.ReviewStatus.Equals("needs_review", StringComparison.OrdinalIgnoreCase))} marked needs review";
    public string RequestSummary => $"{Requests.Count} saved requests";
    public string SelectedRequestHistorySummary => SelectedRequestItem is null
        ? "Select a saved request to see its activity history."
        : SelectedRequestItem.LatestHistorySummary;
    public string SelectedRequestHealthSummary => SelectedRequestItem?.HealthSummary ?? "Select a saved request to see whether it still matches the current review state.";
    public string AutomationQueueSummary => $"{AutomationQueueItems.Count} automation tasks queued or in progress";
    public string SelectedAutomationTaskSummary => SelectedAutomationTaskItem is null
        ? "Select a queued request to show what the AI loop should work on next."
        : $"{SelectedAutomationTaskItem.StatusLabel} | {SelectedAutomationTaskItem.TypeLabel} | {SelectedAutomationTaskItem.TargetScope}";
    public string SelectedAutomationTaskPromptSummary => SelectedAutomationTaskItem?.PromptPreviewSummary ?? "Select a queued request to inspect the exact AI prompt preview.";
    public string SelectedAutomationTaskActivitySummary => SelectedAutomationTaskItem?.LatestActivitySummary ?? "No queued request selected yet.";
    public string SelectedAutomationTaskCandidateSummary => SelectedAutomationTaskItem?.LinkedCandidateSummary ?? "No queued request selected yet.";
    public string SelectedAutomationTaskHistorySummary => SelectedAutomationTaskItem is null
        ? "Select a queued request to inspect the full AI attempt trail."
        : $"{SelectedAutomationTaskHistoryItems.Count} recorded event(s) for the selected AI task.";
    public IReadOnlyList<RequestHistoryItemViewModel> SelectedAutomationTaskHistoryItems => SelectedRequestItem?.History ?? [];
    public IReadOnlyList<CandidateItemViewModel> SelectedAutomationTaskCandidates => SelectedAutomationTaskItem is null
        ? []
        : Candidates.Where(candidate => candidate.RequestId.Equals(SelectedAutomationTaskItem.RequestId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.UpdatedUtc)
            .ToList();
    public string SelectedAutomationTaskLinkedCandidatesSummary => SelectedAutomationTaskItem is null
        ? "Select a queued request to inspect its linked candidates."
        : SelectedAutomationTaskCandidates.Count == 0
            ? "No staged candidates are linked to the selected AI task yet."
            : $"{SelectedAutomationTaskCandidates.Count} linked candidate(s) are available for compare, import, or paint.";
    public string CandidateSummary => $"{Candidates.Count} staged candidates";
    public string ActivitySummary => ActivityLog.Count == 0 ? "No activity yet." : $"{ActivityLog.Count} recent actions";
    public string ControlModeSummary => ControlMode.Equals("manual", StringComparison.OrdinalIgnoreCase)
        ? "Manual control: the user can take over freely, while the app keeps the current row, frame, and editor state intact."
        : "Assisted AI control: the app stays ready for guided review, capture, and scripted support without hiding the work.";
    public string ControlModeHint => ControlMode.Equals("manual", StringComparison.OrdinalIgnoreCase)
        ? "Switch back to Assisted AI any time. The app keeps the current row, frame, workspace, and draft state."
        : "Switch to Manual any time without losing your place. Unsaved editor work is checkpointed automatically.";
    public string AutomationQueueHint => ControlMode.Equals("manual", StringComparison.OrdinalIgnoreCase)
        ? "Manual mode pauses the visible AI queue so the user can step in without losing the selected row, frame, or editor draft."
        : "Assisted AI mode keeps queued requests visible and ready for controlled automation or guided review.";
    public string StudioAiHandoffSummary => SelectedAutomationTaskItem is not null
        ? $"AI task focus: {SelectedAutomationTaskItem.DisplayName}  |  {SelectedAutomationTaskItem.StatusLabel}  |  {SelectedAutomationTaskItem.TargetScope}"
        : SelectedRequestItem is not null
            ? $"Request focus: {SelectedRequestItem.Title}  |  {SelectedRequestItem.StatusLabel}  |  {SelectedRequestItem.TargetScope}"
            : "No AI task or saved request is currently focused. You can still review, draw, and stage candidates manually.";
    public string StudioCandidateHandoffSummary => SelectedCandidateItem is null
        ? "No candidate is selected. Stage the authored frame, runtime frame, or live Paint canvas when you want a visible compare/import handoff."
        : $"Candidate focus: {SelectedCandidateItem.Title}  |  {SelectedCandidateItem.StatusLabel}  |  {SelectedCandidateItem.TargetScope}";
    public string SelectedBaseVariantTitle => SelectedBaseVariant is null ? "No row selected" : $"{ToTitleCase(SelectedBaseVariant.Species)} - {ToTitleCase(SelectedBaseVariant.Age)} {ToTitleCase(SelectedBaseVariant.Gender)}";
    public string SelectedBaseVariantStatus => SelectedBaseVariant is null ? "Choose a row to inspect its family coverage." : $"{ToTitleCase(SelectedBaseVariant.OverallStatus)} coverage across {SelectedBaseVariant.ExpectedColors} color variants";
    public string SelectedBaseVariantSummary => SelectedBaseVariant?.FamilySummary ?? "Family progress will appear here once a row is selected.";
    public string SelectedBaseVariantReviewSummary => SelectedBaseVariant?.ReviewSummary ?? "Use the review status and notes to mark what still needs work.";
    public string SelectedBaseVariantFrameQualitySummary => SelectedBaseVariant?.FrameReviewSummary ?? "Frame-level quality will appear here once review data exists.";
    public string SelectedBaseVariantFrameIssueSummary => SelectedBaseVariant?.FrameIssueSummary ?? "Issue tags from frame reviews will appear here.";
    public string CurrentFrameReviewTargetSummary => TryGetCurrentFrameReviewContext(out var frameId)
        ? $"Frame review target: {frameId} in {SelectedViewerFamily}/{SelectedViewerSequenceId} ({SelectedViewerColor})."
        : "Choose a row, family, sequence, color, and frame to review the current slot.";
    public string CurrentFrameReviewSummary => $"{FormatStatusLabel(SelectedFrameReviewStatus)} | {BuildFrameIssueTagsSummary()} | {BuildFrameReviewNotePreview()}";
    public string CurrentFrameReviewUpdatedLabel => SelectedFrameReviewUpdatedUtc is null
        ? "Frame review has not been saved yet."
        : $"Frame review saved {SelectedFrameReviewUpdatedUtc.Value.ToLocalTime():MMM d h:mm tt}";
    public string CurrentWorkspaceLabel => SelectedWorkspaceTabIndex switch
    {
        0 => "Automation",
        1 => "Review",
        2 => "Queue",
        3 => "Requests",
        4 => SelectedStudioTabIndex == 0 ? "Studio / Animate" : "Studio / Paint",
        5 => "Planning",
        _ => "Workspace"
    };
    public bool HasCurrentTask => !string.IsNullOrWhiteSpace(CurrentTaskTitle);
    public string CurrentTaskTitle => BuildCurrentTaskDescriptor().Title;
    public string CurrentTaskSummary => BuildCurrentTaskDescriptor().Summary;
    public string CurrentTaskTargetSummary => BuildCurrentTaskDescriptor().TargetSummary;
    public string CurrentTaskWorkspaceHint => BuildCurrentTaskDescriptor().WorkspaceHint;
    public string LiveWorkMonitorSummary => SelectedBaseVariant is null
        ? "No active row is surfaced yet."
        : $"{CurrentWorkspaceLabel}  |  {SelectedBaseVariant.DisplayName}  |  {SelectedViewerFamily}/{SelectedViewerSequenceId}  |  Edit {ViewerSelectionSummary}";
    public string LiveWorkVisualHint => IsEditorFrameLoaded
        ? "The pinned monitor mirrors the live editor canvas, authored target, and playback frame so the work stays visible even when the main Studio surface is farther down."
        : "Once Paint loads a frame, the pinned monitor will mirror the editor canvas here so visible edits are easier to follow.";
    public string LoopGuideSummary => SelectedBaseVariant is null
        ? "Start in Review, pick a sprite row, then use Studio to preview, paint, save, and replay the animation without leaving the app."
        : $"Workspace: {CurrentWorkspaceLabel}  |  Row: {SelectedBaseVariant.DisplayName}  |  Preview: {SelectedViewerFamily}/{SelectedViewerSequenceId}  |  Frame: {ViewerSelectionSummary}";
    public string ReviewStepSummary => SelectedBaseVariant is null
        ? "Choose a base row to audit."
        : $"Selected {SelectedBaseVariant.DisplayName}.";
    public string AnimateStepSummary => SelectedBaseVariant is null
        ? "Open Studio after selecting a row."
        : $"{ToTitleCase(SelectedViewerFamily)} / {SelectedViewerSequenceId} / {ViewerSelectionSummary}";
    public string PaintStepSummary => !IsEditorFrameLoaded
        ? "Load the current frame or a runtime template into Paint."
        : $"{Path.GetFileName(string.IsNullOrWhiteSpace(_editorSaveFramePath) ? _loadedEditorFramePath : _editorSaveFramePath)} loaded  |  {(IsEditorDirty ? "Unsaved changes" : "Saved to disk")}";
    public string SaveReplayStepSummary => !IsEditorFrameLoaded
        ? "Save + Replay activates after a frame is loaded in Paint."
        : IsEditorDirty
            ? "Save the edited frame and jump back to Animate to judge it in motion."
            : "The current frame is saved. Return to Animate to replay it.";
    public string PaintEditTargetTitle => !IsEditorFrameLoaded
        ? "No Paint Target Loaded"
        : $"{SelectedBaseVariant?.DisplayName ?? "Selected row"}  |  {ViewerSelectionSummary}";
    public string PaintEditTargetSummary => !IsEditorFrameLoaded
        ? "Load the current authored frame, a runtime template, or a blank frame to begin painting."
        : $"Paint target: {Path.GetFileName(string.IsNullOrWhiteSpace(_editorSaveFramePath) ? _loadedEditorFramePath : _editorSaveFramePath)}  |  Source view: {SelectedViewerFamily}/{SelectedViewerSequenceId}/{SelectedViewerColor}";
    public string PaintEditStateSummary => !IsEditorFrameLoaded
        ? "Nothing loaded in Paint yet."
        : IsEditorDirty
            ? "Unsaved edits are on the canvas now. Save + Replay when you want to judge this frame in motion."
            : "This paint target is currently saved. Keep editing, or jump back to Animate to replay it.";
    public string PaintNextStepSummary => !IsEditorFrameLoaded
        ? "Best next step: choose a frame in Animate, then click Edit Current In Paint."
        : IsEditorDirty
            ? "Best next step: Save + Replay to test the current frame in motion."
            : "Best next step: keep painting, or return to Animate to compare motion and references.";
    public string PaintMonitorSummary => !IsEditorFrameLoaded
        ? "Keep Paint open beside the selected references so the whole edit stays visible in the app."
        : $"Live canvas target: {Path.GetFileName(string.IsNullOrWhiteSpace(_editorSaveFramePath) ? _loadedEditorFramePath : _editorSaveFramePath)}  |  Selected frame: {ViewerSelectionSummary}";
    public string EditorCaptureSummary => $"{(AutoCaptureOnStroke ? "Stroke capture on" : "Stroke capture off")}  |  {(AutoCaptureOnSave ? "Save capture on" : "Save capture off")}";
    public string EditorCapturePathSummary => string.IsNullOrWhiteSpace(_editorCaptureDirectory)
        ? "No editor capture folder configured."
        : $"Capture folder: {_editorCaptureDirectory}";
    public string LastEditorCaptureSummary => $"Last capture: {LastEditorCapturePath}";
    public string CoreLoopSummary => SelectedBaseVariant is null
        ? "Review: choose a row first."
        : $"{SelectedBaseVariant.DisplayName} -> {SelectedViewerFamily}/{SelectedViewerSequenceId} -> {ViewerSelectionSummary}";
    public string SelectedEditorColorSummary => $"Tool: {ToTitleCase(SelectedEditorTool)}  |  Brush {SelectedEditorBrushSize}px  |  Color: {SelectedEditorColorHex}";
    public string EditorHsvSummary => $"HSV: {EditorHue} deg / {EditorSaturation}% / {EditorValue}%";
    public string EditorQuickColorSummary => $"Hue ring: {EditorHuePresets.Count} quick hues  |  Tone ramp: {EditorTonePresets.Count} current-hue variations";
    public string EditorHistorySummary => $"Undo {_undoHistory.Count}  |  Redo {_redoHistory.Count}";
    public string EditorLayerSummary => EditorLayers.Count == 0
        ? "No layers loaded."
        : $"{EditorLayers.Count} layers  |  Active: {EditorLayers.FirstOrDefault(layer => layer.LayerId == _activeEditorLayerId)?.Name ?? "None"}";
    public string ActiveEditorLayerSummary => GetActiveLayerItem() is { } layer
        ? $"{layer.Name}  |  {layer.RoleLabel}  |  {(layer.IsLocked ? "Locked" : "Editable")}  |  {layer.OpacityPercent}% opacity"
        : "No active layer selected.";
    public string EditorLayerWorkflowHint => EditorLayers.Count == 0
        ? "Load a frame to start layering."
        : _selectedEditorIndices.Count > 0
            ? "Protect the base, lift the current selection into a new overlay, then paint and merge only after the frame looks right in motion."
            : "Keep template and base layers locked, paint on overlays, and flatten only after the frame is approved.";
    public string EditorSelectionSummary => _selectedEditorIndices.Count == 0
        ? (_editorClipboard is null ? "No active selection." : $"No active selection. Clipboard: {_editorClipboard.Width}x{_editorClipboard.Height}")
        : $"Selection: {_selectedEditorIndices.Count} px";
    public string EditorSelectionBoundsSummary => TryGetSelectionBounds(out var minX, out var minY, out var maxX, out var maxY)
        ? $"Bounds: {minX},{minY} -> {maxX},{maxY}  |  {(maxX - minX) + 1} x {(maxY - minY) + 1}"
        : "Bounds: none";
    public string EditorSelectionTransformHint => _selectedEditorIndices.Count == 0
        ? "Select pixels to move, rotate, scale, flip, or lift them into their own layer."
        : SelectedEditorTool.Equals("move", StringComparison.OrdinalIgnoreCase)
            ? "Drag the selection directly on the canvas, or use the transform buttons for precise reshaping."
            : "Use Move for direct dragging, or Rotate / Scale / Flip for controlled transforms.";
    public string EditorMirrorSummary => $"Mirror: {(EditorMirrorHorizontal ? "H" : "-")}/{(EditorMirrorVertical ? "V" : "-")}";
    public string EditorShapeModeSummary => EditorFillShapes ? "Shapes: filled" : "Shapes: outline";
    public string SavedPaletteSummary => SavedEditorPalette.Count == 0 ? "No saved swatches yet." : $"{SavedEditorPalette.Count} saved swatches";
    public string ProjectPaletteSummary => ProjectPalettes.Count == 0 ? "No project palettes yet." : $"{ProjectPalettes.Count} reusable project palette(s)";
    public string AiProviderSummary => AiProviders.Count == 0 ? "No AI providers configured." : $"{AiProviders.Count} provider adapter(s) configured";
    public string DefaultAiProviderSummary => AiProviders.FirstOrDefault(provider => provider.IsDefault) is { } provider
        ? $"{provider.DisplayName} is the default provider. {provider.AutomationLabel} via {provider.ExecutionModeLabel}."
        : "No default AI provider is configured yet.";
    public string ProjectPaletteSaveHint => SelectedBaseVariant is null
        ? "Save swatches as a project palette, or select a row first to also save a species palette."
        : $"Save colors either as a project palette or as a {ToTitleCase(SelectedBaseVariant.Species)} palette you can reuse across that species.";
    public string SelectedProjectPaletteSummary => SelectedProjectPaletteItem is null
        ? "Select a saved project palette to reuse it here."
        : $"{SelectedProjectPaletteItem.ScopeLabel} | {SelectedProjectPaletteItem.ColorSummary} | {SelectedProjectPaletteItem.UpdatedLabel}";
    public string TrustedExportHistorySummary => TrustedExportHistoryItems.Count == 0 ? "No trusted exports yet." : $"{TrustedExportHistoryItems.Count} trusted export bundle(s)";
    public string ValidationReportSummary => ValidationReports.Count == 0 ? "No validation reports yet." : $"{ValidationReports.Count} validation report(s)";
    public string SelectedValidationReportSummary => SelectedValidationReportItem is null
        ? "Run validation to create a reusable project-health report."
        : $"{SelectedValidationReportItem.DisplayName} | {SelectedValidationReportItem.Summary}";
    public string TrustedExportBlockerListSummary => TrustedExportBlockers.Count == 0 ? "No trusted export blockers listed." : $"{TrustedExportBlockers.Count} trusted export blocker(s)";
    public string PlanningUnmappedEntrySummary => PlanningUnmappedEntries.Count == 0 ? "No unmapped discovered assets." : $"{PlanningUnmappedEntries.Count} unmapped discovered asset(s)";
    public string EditorToolHint => SelectedEditorTool switch
    {
        "brush" => "Brush: click or drag across pixels to paint. Use the brush size to block in shapes quickly.",
        "erase" => "Erase: click or drag across pixels to clear them with the current brush size.",
        "dropper" => "Dropper: click any pixel to copy its color.",
        "fill" => "Fill: click a pixel to flood fill its connected region.",
        "select" => "Select: click and drag to marquee pixels. Use move, copy, paste, or delete to reshape parts of the sprite.",
        "lasso" => "Lasso: drag a freeform outline around pixels to grab irregular shapes without boxing the whole area.",
        "move" => "Move: drag the current selection directly on the canvas. Use rotate and scale to reshape the selected sprite chunk.",
        "line" => "Line: click and drag to draw a line between two points using the current brush size.",
        "rectangle" => EditorFillShapes
            ? "Rectangle: click and drag to draw a filled rectangle using the current brush size."
            : "Rectangle: click and drag to draw an outline rectangle with the current brush size.",
        "ellipse" => EditorFillShapes
            ? "Ellipse: click and drag to draw a filled ellipse using the current brush size."
            : "Ellipse: click and drag to draw an ellipse outline with the current brush size.",
        _ => "Choose a tool to start editing."
    };
    public string EditorCanvasSummary => _editorWidth > 0 && _editorHeight > 0 ? $"{_editorWidth} x {_editorHeight} pixels" : "No frame loaded.";
    public string EditorTemplateSummary => string.IsNullOrWhiteSpace(_editorSaveFramePath)
        ? "No save target selected."
        : $"Editing target: {_editorSaveFramePath}";
    public double EditorCanvasHeight => _editorHeight <= 0 ? 0 : _editorHeight * SelectedEditorZoom;
    public bool HasEditorSelectionOverlay => _selectedEditorIndices.Count > 0 && _editorWidth > 0 && _editorHeight > 0;
    public double EditorSelectionOverlayLeft => TryGetSelectionBounds(out var minX, out _, out _, out _)
        ? minX * EditorPixelSize
        : 0;
    public double EditorSelectionOverlayTop => TryGetSelectionBounds(out _, out var minY, out _, out _)
        ? minY * EditorPixelSize
        : 0;
    public double EditorSelectionOverlayWidth => TryGetSelectionBounds(out var minX, out _, out var maxX, out _)
        ? Math.Max(EditorPixelSize, ((maxX - minX) + 1) * EditorPixelSize)
        : 0;
    public double EditorSelectionOverlayHeight => TryGetSelectionBounds(out _, out var minY, out _, out var maxY)
        ? Math.Max(EditorPixelSize, ((maxY - minY) + 1) * EditorPixelSize)
        : 0;
    public double EditorSelectionHandleWestLeft => EditorSelectionOverlayLeft - 8;
    public double EditorSelectionHandleEastLeft => EditorSelectionOverlayLeft + EditorSelectionOverlayWidth - 8;
    public double EditorSelectionHandleNorthTop => EditorSelectionOverlayTop - 8;
    public double EditorSelectionHandleSouthTop => EditorSelectionOverlayTop + EditorSelectionOverlayHeight - 8;
    public double EditorSelectionHandleCenterX => EditorSelectionOverlayLeft + (EditorSelectionOverlayWidth / 2) - 8;
    public double EditorSelectionHandleCenterY => EditorSelectionOverlayTop + (EditorSelectionOverlayHeight / 2) - 8;
    public double EditorSelectionToolbarLeft => Math.Max(0, EditorSelectionOverlayLeft + (EditorSelectionOverlayWidth / 2) - 150);
    public double EditorSelectionToolbarTop => Math.Max(0, EditorSelectionOverlayTop - 44);
    public string EditorComparisonSummary => !IsEditorFrameLoaded
        ? "Load a frame to compare the original, edited, and diff views."
        : $"{ChangedEditorPixelCount} changed pixels  |  Baseline: {Path.GetFileName(_loadedEditorFramePath)}";
    public string EditorBlinkCompareSummary => !IsEditorFrameLoaded || !HasEditorBaselineBitmap
        ? "Load a frame to blink between the baseline and current edit."
        : IsEditorBlinkCompareEnabled
            ? (_showEditorBaselineBlinkFrame ? "Blink compare: original baseline" : "Blink compare: current edit")
            : "Blink compare is off.";
    public bool IsEditorFrameLoaded => _editorWidth > 0 && _editorHeight > 0 && EditorPixels.Count > 0;
    public bool IsEditorFrameMissing => !IsEditorFrameLoaded;
    public bool HasEditorBaselineBitmap => EditorBaselineBitmap is not null;
    public bool HasEditorDiffBitmap => EditorDiffBitmap is not null;
    public bool IsEditorBaselineBitmapMissing => !HasEditorBaselineBitmap;
    public bool IsEditorDiffBitmapMissing => !HasEditorDiffBitmap;
    public Bitmap? EditorBlinkCompareBitmap => _showEditorBaselineBlinkFrame && HasEditorBaselineBitmap ? EditorBaselineBitmap : EditorPreviewBitmap;
    public int ChangedEditorPixelCount => CalculateChangedEditorPixelCount();
    public string EditorEmptyStateSummary => "Pick a row in Review, choose a family/sequence/color in Animate, then load the current frame or runtime template into Paint. The canvas stays disabled until a real frame is ready.";
    public double EditorCanvasWidth => _editorWidth <= 0 ? 0 : _editorWidth * SelectedEditorZoom;
    public double EditorPixelSize => SelectedEditorZoom;
    public string ViewerSelectionSummary => string.IsNullOrWhiteSpace(SelectedViewerSequenceId) ? "Choose a sequence." : $"{SelectedViewerSequenceId}_{_currentFrameIndex:00}";
    public string PlaybackSelectionSummary => string.IsNullOrWhiteSpace(SelectedViewerSequenceId) ? "Choose a sequence." : $"{SelectedViewerSequenceId}_{_playbackFrameIndex:00}";
    public string PlaybackSpeedSummary => $"{PreviewPlaybackMs} ms / frame";
    public string PlaybackModeSummary => IsPlaybackEnabled
        ? $"Playing  |  {ViewerSelectionSummary}  |  {PreviewPlaybackMs} ms / frame"
        : $"Paused on {ViewerSelectionSummary}  |  Step frames or press Play";
    public string ViewerEditHint => IsPlaybackEnabled
        ? "Playback is running. Press Pause On Frame or click Show on any thumbnail to freeze the frame you want to edit."
        : "The preview is paused on the current frame. Use Edit Current In Paint to work on exactly what you see here.";
    public string CurrentFrameActionSummary => IsCurrentFrameBitmapMissing
        ? "There is no authored frame in this slot yet. Load the runtime template if you want a starting point."
        : IsPlaybackEnabled
            ? "The authored frame is still cycling. Pause on this frame before editing if you want a stable target."
            : "This authored frame is frozen and ready to paint.";
    public string RuntimeTemplateActionSummary => IsRuntimeFrameBitmapMissing
        ? "No runtime template is available for this slot."
        : "Use the runtime frame as a base template, then save back into the authored slot.";
    public string BlinkCompareSummary => !HasCurrentFrameBitmap || !HasRuntimeFrameBitmap
        ? "Blink compare needs both authored and runtime frames."
        : IsBlinkCompareEnabled
            ? (_showRuntimeBlinkFrame ? "Blink compare: runtime reference" : "Blink compare: authored frame")
            : "Blink compare is off.";
    public string LivePlaybackSummary => HasPlaybackFrameBitmap
        ? (IsPlaybackEnabled
            ? $"Live playback is currently showing {PlaybackSelectionSummary}."
            : $"Live playback is parked on {PlaybackSelectionSummary}.")
        : "No authored playback frame is available for the current sequence.";
    public string LiveDiffSummary => !IsEditorFrameLoaded
        ? "Diff preview appears after Paint loads a frame."
        : ChangedEditorPixelCount == 0
            ? "No painted differences from the loaded baseline yet."
            : $"{ChangedEditorPixelCount} changed pixel(s) from the loaded baseline.";
    public string LivePlaybackActionSummary => IsPlaybackEnabled
        ? "The live preview is moving independently from the edit target."
        : "Press Play to watch motion, or use the live frame as your new edit target.";
    public string RuntimePlaybackSummary => HasRuntimePlaybackFrameBitmap
        ? $"Runtime live reference is available for {PlaybackSelectionSummary}."
        : "No runtime live reference is available for the current playback slot.";
    public string PreviousFrameReferenceSummary => HasPreviousFrameReferenceBitmap
        ? "Previous authored frame is ready as a spacing reference."
        : "No previous authored frame is available for this slot.";
    public string NextFrameReferenceSummary => HasNextFrameReferenceBitmap
        ? "Next authored frame is ready as a spacing reference."
        : "No next authored frame is available for this slot.";
    public double OnionSkinOpacityFraction => Math.Clamp(OnionSkinOpacity / 100.0, 0.0, 1.0);
    public string OnionSkinSummary => IsOnionSkinEnabled ? $"Onion skin on ({OnionSkinOpacity}%)" : "Onion skin off";
    public IBrush EditorSelectedColorBrush => TryParseHexColor(SelectedEditorColorHex, out var color)
        ? CreateBrush(color)
        : CreateBrush(new Rgba32(255, 255, 255, 255));
    public string ReviewDataPathDisplay => string.IsNullOrWhiteSpace(_reviewDataPath) ? "No review store configured." : $"Review file: {_reviewDataPath}";
    public string RequestDataPathDisplay => string.IsNullOrWhiteSpace(_requestDataPath) ? "No request store configured." : $"Request file: {_requestDataPath}";
    public string RequestExportPathDisplay => string.IsNullOrWhiteSpace(_requestExportDirectory) ? "No export folder configured." : $"Request exports: {_requestExportDirectory}";
    public string CandidateDataPathDisplay => string.IsNullOrWhiteSpace(_candidateDataPath) ? "No candidate store configured." : $"Candidate file: {_candidateDataPath}";
    public string CandidateAssetDirectoryDisplay => string.IsNullOrWhiteSpace(_candidateAssetDirectory) ? "No candidate staging folder configured." : $"Candidate staging: {_candidateAssetDirectory}";
    public string FrameHistoryDirectoryDisplay => string.IsNullOrWhiteSpace(_frameHistoryDirectory) ? "No frame history folder configured." : $"Frame history: {_frameHistoryDirectory}";
    public string TrustedExportDirectoryDisplay => string.IsNullOrWhiteSpace(_trustedExportDirectory) ? "No trusted export folder configured." : $"Trusted exports: {_trustedExportDirectory}";
    public string SelectedCandidateSummary => SelectedCandidateItem is null
        ? "Stage a frame or select a saved candidate to compare it before approval."
        : $"{SelectedCandidateItem.StatusLabel} | {SelectedCandidateItem.NotePreview}";
    public string SelectedCandidateTargetSummary => SelectedCandidateItem is null
        ? "No candidate selected."
        : $"{SelectedCandidateItem.TargetScope} | source: {SelectedCandidateItem.SourceTypeLabel}";
    public string DraftRequestPreview => BuildRequestPreview();
    public string AutomationModeSummary => "Local Python/PowerShell workflow steps can run hidden from the app. Gemini generation stays as an external browser step for now, so the app does not pretend it can hide or fully own that part yet.";
    public string AutomationSafetySummary => "Use Stop All Automation before stepping in manually. Hidden app-run loops will be stopped immediately, while external browser-based AI steps still need manual handoff.";
    public string PlanningProjectModeSummary => ExpectedVariantCount > 0
        ? "Project-linked mode: the app discovered an existing sprite contract and seeded this planner from it. You can keep the discovered structure or reshape it here before the next generation/editing wave."
        : "Blank-project mode: define the sprite roster, families, and sequence targets here before you generate or draw anything.";
    public string PlanningDiscoverySummary => $"Authored: {FormatPathDiscovery(_authoredSpriteRoot)}  |  Runtime: {FormatPathDiscovery(_runtimeSpriteRoot)}  |  Handoffs: {FormatPathDiscovery(_incomingHandoffRoot)}";
    public string PlanningAdoptionSummary
    {
        get
        {
            var discoveredSpecies = _allBaseVariants.Select(row => row.Species).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var discoveredAges = _allBaseVariants.Select(row => row.Age).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var discoveredGenders = _allBaseVariants.Select(row => row.Gender).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var discoveredColors = ViewerColorOptions.Count;
            return $"Discovered axes: {discoveredSpecies} species  |  {discoveredAges} ages  |  {discoveredGenders} genders  |  {discoveredColors} colors";
        }
    }
    public string PlanningGapSummary
    {
        get
        {
            var discoveredBaseRows = _allBaseVariants.Count;
            var missingBaseRows = Math.Max(0, PlanningBaseRowCount - discoveredBaseRows);
            var extraBaseRows = Math.Max(0, discoveredBaseRows - PlanningBaseRowCount);
            var incompleteRows = _allBaseVariants.Count(row => !row.OverallStatus.Equals("complete", StringComparison.OrdinalIgnoreCase));
            return $"Base rows: planned {PlanningBaseRowCount}, discovered {discoveredBaseRows}, missing {missingBaseRows}, extra {extraBaseRows}  |  Incomplete discovered rows: {incompleteRows}";
        }
    }
    public string PlanningAxisSummary => $"{ParsePlanningList(PlanningSpeciesText).Count} species  |  {ParsePlanningList(PlanningAgeText).Count} ages  |  {ParsePlanningList(PlanningGenderText).Count} genders  |  {ParsePlanningList(PlanningColorText).Count} colors";
    public string PlanningChecklistSummary => $"{PlanningBaseRowCount} base rows  |  {PlanningVariantTargetCount} color variants  |  {PlanningSequenceTargetCount} sequences  |  {PlanningBaseFrameTargetCount} base-row frames";
    public string PlanningProjectExportSummary => $"Project: {PlanningProjectId}  |  Root: {PlanningRootPath}";
    public string PlanningAnimationSummary => PlanningChecklist.Count == 0
        ? "Add family blueprint lines like `locomotion: idle x4, walk x6` to start a checklist."
        : string.Join("  |  ", PlanningChecklist.Select(item => $"{item.Family}: {item.SequenceCount} sequences / {item.FramesPerBaseRow} frames"));
    public string PlanningTemplateSummary => $"{PlanningTemplates.Count} saved blueprint templates";
    public string PlanningAdoptionEntrySummary => $"{PlanningAdoptionEntries.Count} planned sequence targets analyzed";
    public string PlanningDiscoveryCategorySummary => PlanningDiscoveryCategories.Count == 0
        ? "No discovery categories yet."
        : string.Join("  |  ", PlanningDiscoveryCategories.Select(item => $"{item.Category}: {item.Count}"));
    public string PlanningStarterHint => PlanningChecklist.Count == 0
        ? "Start from scratch by listing species, axes, and family lines, or load the discovered project blueprint as a starting point."
        : "Use this blueprint to drive manual art, AI requests, or automated repair/generation loops without leaving the app.";
    public string TrustedExportSummary
    {
        get
        {
            var snapshot = BuildTrustedExportSnapshot();
            return $"Approved frames: {snapshot.ApprovedFrameCount}  |  Template-only: {snapshot.TemplateOnlyFrameCount}  |  Flagged: {snapshot.FlaggedFrameCount}  |  Approved rows: {snapshot.ApprovedRowCount}";
        }
    }
    public string TrustedExportHint
    {
        get
        {
            var snapshot = BuildTrustedExportSnapshot();
            return snapshot.ApprovedFrameCount == 0
                ? "Mark frames or rows approved before exporting a trusted implementation bundle."
                : "Export writes a visible manifest and mirrored approved PNGs so implementation can follow the trusted set instead of the whole raw project.";
        }
    }
    public string TrustedExportBlockerSummary => BuildTrustedExportBlockerSummary();
    public string FrameHistorySummary => $"{FrameHistoryItems.Count} saved history snapshots";
    public string ProjectReadinessSummary => ProjectReadinessItems.Count == 0
        ? "Project readiness has not been evaluated yet."
        : $"{ProjectReadinessItems.Count(item => item.Status.Equals("ready", StringComparison.OrdinalIgnoreCase))} ready  |  {ProjectReadinessItems.Count(item => item.Status.Equals("needs_attention", StringComparison.OrdinalIgnoreCase))} need attention";
    public int PlanningBaseRowCount => ParsePlanningList(PlanningSpeciesText).Count * Math.Max(1, ParsePlanningList(PlanningAgeText).Count) * Math.Max(1, ParsePlanningList(PlanningGenderText).Count);
    public int PlanningVariantTargetCount => PlanningBaseRowCount * Math.Max(1, ParsePlanningList(PlanningColorText).Count);
    public int PlanningSequenceTargetCount => PlanningChecklist.Sum(item => item.SequenceCount);
    public int PlanningBaseFrameTargetCount => PlanningBaseRowCount * PlanningChecklist.Sum(item => item.FramesPerBaseRow);
    public bool HasCurrentFrameBitmap => CurrentFrameBitmap is not null;
    public bool IsCurrentFrameBitmapMissing => !HasCurrentFrameBitmap;
    public bool HasRuntimeFrameBitmap => RuntimeFrameBitmap is not null;
    public bool IsRuntimeFrameBitmapMissing => !HasRuntimeFrameBitmap;
    public bool HasPreviousFrameReferenceBitmap => PreviousFrameReferenceBitmap is not null;
    public bool IsPreviousFrameReferenceBitmapMissing => !HasPreviousFrameReferenceBitmap;
    public bool HasNextFrameReferenceBitmap => NextFrameReferenceBitmap is not null;
    public bool IsNextFrameReferenceBitmapMissing => !HasNextFrameReferenceBitmap;
    public bool HasOnionSkinBitmap => OnionSkinBitmap is not null;
    public bool HasPlaybackFrameBitmap => PlaybackFrameBitmap is not null;
    public bool IsPlaybackFrameBitmapMissing => !HasPlaybackFrameBitmap;
    public bool HasRuntimePlaybackFrameBitmap => RuntimePlaybackFrameBitmap is not null;
    public bool IsRuntimePlaybackFrameBitmapMissing => !HasRuntimePlaybackFrameBitmap;
    public bool HasSelectedCandidateBitmap => SelectedCandidateBitmap is not null;
    public bool HasSelectedCandidateReferenceBitmap => SelectedCandidateReferenceBitmap is not null;
    public bool IsSelectedCandidateBitmapMissing => !HasSelectedCandidateBitmap;
    public bool IsSelectedCandidateReferenceBitmapMissing => !HasSelectedCandidateReferenceBitmap;

    public Bitmap? CurrentFrameBitmap
    {
        get => _currentFrameBitmap;
        private set => SetBitmap(ref _currentFrameBitmap, value);
    }

    public Bitmap? OnionSkinBitmap
    {
        get => _onionSkinBitmap;
        private set => SetBitmap(ref _onionSkinBitmap, value);
    }

    public Bitmap? RuntimeFrameBitmap
    {
        get => _runtimeFrameBitmap;
        private set => SetBitmap(ref _runtimeFrameBitmap, value);
    }

    public Bitmap? PreviousFrameReferenceBitmap
    {
        get => _previousFrameReferenceBitmap;
        private set => SetBitmap(ref _previousFrameReferenceBitmap, value);
    }

    public Bitmap? EditorPreviewBitmap
    {
        get => _editorPreviewBitmap;
        private set => SetBitmap(ref _editorPreviewBitmap, value);
    }

    public Bitmap? NextFrameReferenceBitmap
    {
        get => _nextFrameReferenceBitmap;
        private set => SetBitmap(ref _nextFrameReferenceBitmap, value);
    }

    public Bitmap? BlinkCompareBitmap => _showRuntimeBlinkFrame && HasRuntimeFrameBitmap ? RuntimeFrameBitmap : CurrentFrameBitmap;

    public Bitmap? PlaybackFrameBitmap
    {
        get => _playbackFrameBitmap;
        private set => SetBitmap(ref _playbackFrameBitmap, value);
    }

    public Bitmap? RuntimePlaybackFrameBitmap
    {
        get => _runtimePlaybackFrameBitmap;
        private set => SetBitmap(ref _runtimePlaybackFrameBitmap, value);
    }

    public Bitmap? EditorBaselineBitmap
    {
        get => _editorBaselineBitmap;
        private set => SetBitmap(ref _editorBaselineBitmap, value);
    }

    public Bitmap? EditorDiffBitmap
    {
        get => _editorDiffBitmap;
        private set => SetBitmap(ref _editorDiffBitmap, value);
    }

    public Bitmap? SelectedCandidateBitmap
    {
        get => _selectedCandidateBitmap;
        private set => SetBitmap(ref _selectedCandidateBitmap, value);
    }

    public Bitmap? SelectedCandidateReferenceBitmap
    {
        get => _selectedCandidateReferenceBitmap;
        private set => SetBitmap(ref _selectedCandidateReferenceBitmap, value);
    }

    [RelayCommand]
    private void StepPreviousFrame()
    {
        IsPlaybackEnabled = false;
        AdvanceFrame(-1);
    }

    [RelayCommand]
    private void StepNextFrame()
    {
        IsPlaybackEnabled = false;
        AdvanceFrame(1);
    }

    [RelayCommand]
    private void EditPreviousFrame()
    {
        IsPlaybackEnabled = false;
        AdvanceFrame(-1);
        LoadCurrentFrameIntoEditor();
    }

    [RelayCommand]
    private void EditNextFrame()
    {
        IsPlaybackEnabled = false;
        AdvanceFrame(1);
        LoadCurrentFrameIntoEditor();
    }

    [RelayCommand]
    private void RestartPreview()
    {
        IsPlaybackEnabled = false;
        _currentFrameIndex = 0;
        _playbackFrameIndex = 0;
        UpdateViewer();
    }

    [RelayCommand]
    private void PlayPreview()
    {
        _playbackFrameIndex = _currentFrameIndex;
        UpdatePlaybackPreview();
        IsPlaybackEnabled = true;
        EditorStatusMessage = "Playback started.";
    }

    [RelayCommand]
    private void PausePreview()
    {
        IsPlaybackEnabled = false;
        EditorStatusMessage = $"Playback paused on {ViewerSelectionSummary}.";
    }

    [RelayCommand]
    private void ToggleBlinkCompare()
    {
        IsBlinkCompareEnabled = !IsBlinkCompareEnabled;
        if (!IsBlinkCompareEnabled)
        {
            _showRuntimeBlinkFrame = false;
            OnPropertyChanged(nameof(BlinkCompareBitmap));
            OnPropertyChanged(nameof(BlinkCompareSummary));
        }

        AddActivity("viewer", IsBlinkCompareEnabled ? "Enabled blink compare." : "Disabled blink compare.");
    }

    [RelayCommand]
    private void UsePlaybackFrameAsEditTarget()
    {
        var sequence = GetSelectedSequence();
        if (sequence is null || sequence.FrameCount <= 0)
        {
            return;
        }

        IsPlaybackEnabled = false;
        _currentFrameIndex = ((_playbackFrameIndex % sequence.FrameCount) + sequence.FrameCount) % sequence.FrameCount;
        UpdateViewer();
        NotifyLoopStateChanged();
        AddActivity("viewer", $"Moved live playback into the edit target: {ViewerSelectionSummary}.");
    }

    [RelayCommand]
    private void SelectPreviousVariant() => SelectVariantByOffset(-1);

    [RelayCommand]
    private void SelectNextVariant() => SelectVariantByOffset(1);

    [RelayCommand]
    private void SelectNextNeedsReviewVariant()
    {
        if (FilteredBaseVariants.Count == 0)
        {
            return;
        }

        var startIndex = SelectedBaseVariant is null ? -1 : FilteredBaseVariants.IndexOf(SelectedBaseVariant);
        for (var offset = 1; offset <= FilteredBaseVariants.Count; offset++)
        {
            var index = (startIndex + offset + FilteredBaseVariants.Count) % FilteredBaseVariants.Count;
            var candidate = FilteredBaseVariants[index];
            if (!candidate.ReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase))
            {
                SelectedBaseVariant = candidate;
                return;
            }
        }
    }

    [RelayCommand]
    private void SelectViewerFrame(int frameIndex)
    {
        var sequence = GetSelectedSequence();
        if (sequence is null || frameIndex < 0 || frameIndex >= sequence.FrameCount)
        {
            return;
        }

        IsPlaybackEnabled = false;
        _currentFrameIndex = frameIndex;
        _playbackFrameIndex = frameIndex;
        UpdateViewer();
        NotifyLoopStateChanged();
        AddActivity("viewer", $"Selected frame {sequence.SequenceId}_{frameIndex:00}.");
    }

    [RelayCommand]
    private void OpenReviewWorkspace()
    {
        SurfaceReviewWorkspace(true);
    }

    [RelayCommand]
    private void OpenPlanningWorkspace()
    {
        SurfacePlanningWorkspace(true);
    }

    [RelayCommand]
    private void ResetPlanningBlueprintToProject()
    {
        PlanningSpeciesText = _seedPlanningSpeciesText;
        PlanningAgeText = _seedPlanningAgeText;
        PlanningGenderText = _seedPlanningGenderText;
        PlanningColorText = _seedPlanningColorText;
        PlanningFamilyBlueprintText = _seedPlanningFamilyBlueprintText;
        RebuildPlanningChecklist();
        AddActivity("planning", "Reset the planning blueprint from the discovered project contract.");
    }

    [RelayCommand]
    private void AdoptDiscoveredAssetsIntoPlanning()
    {
        try
        {
            var discovered = DiscoverPlanningBlueprintFromAuthoredAssets();
            if (discovered.Species.Count == 0)
            {
                PlanningDiscoveryAdoptionMessage = "No discovered authored assets were available to adopt.";
                return;
            }

            PlanningSpeciesText = string.Join(", ", discovered.Species);
            PlanningAgeText = string.Join(", ", discovered.Ages);
            PlanningGenderText = string.Join(", ", discovered.Genders);
            PlanningColorText = string.Join(", ", discovered.Colors);
            PlanningFamilyBlueprintText = discovered.FamilyBlueprintText;
            RebuildPlanningChecklist();
            RefreshPlanningDiagnostics();
            PlanningDiscoveryAdoptionMessage = "Adopted the discovered authored asset structure into the planning blueprint.";
            AddActivity("planning", PlanningDiscoveryAdoptionMessage);
        }
        catch (Exception ex)
        {
            PlanningDiscoveryAdoptionMessage = $"Unable to adopt discovered assets: {ex.Message}";
            AddActivity("planning", $"Discovery adoption failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ClearPlanningBlueprint()
    {
        PlanningSpeciesText = string.Empty;
        PlanningAgeText = string.Empty;
        PlanningGenderText = string.Empty;
        PlanningColorText = string.Empty;
        PlanningFamilyBlueprintText = string.Empty;
        RebuildPlanningChecklist();
        AddActivity("planning", "Cleared the planning blueprint for a from-scratch sprite project.");
    }

    [RelayCommand]
    private void ExportPlanningProjectProfile()
    {
        if (!TryBuildPlanningProjectConfig(out var config, out var exportPath, out var validationMessage))
        {
            PlanningExportMessage = validationMessage;
            return;
        }

        try
        {
            var exportDirectory = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrWhiteSpace(exportDirectory))
            {
                Directory.CreateDirectory(exportDirectory);
            }

            var configService = new JsonProjectConfigService();
            configService.Save(exportPath, config);
            PlanningExportPath = exportPath;
            PlanningExportMessage = $"Exported starter project profile to {exportPath}.";
            AddActivity("planning", $"Exported starter project profile to {exportPath}.");
        }
        catch (Exception ex)
        {
            PlanningExportMessage = $"Unable to export starter profile: {ex.Message}";
            AddActivity("planning", $"Project profile export failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CreatePlanningStarterWorkspace()
    {
        if (!TryBuildPlanningProjectConfig(out var config, out var exportPath, out var validationMessage))
        {
            PlanningWorkspaceMessage = validationMessage;
            return;
        }

        try
        {
            Directory.CreateDirectory(config.RootPath);
            Directory.CreateDirectory(ResolvePlanningWorkspacePath(config.RootPath, config.RuntimeSpriteRoot));
            Directory.CreateDirectory(ResolvePlanningWorkspacePath(config.RootPath, config.AuthoredSpriteRoot));
            Directory.CreateDirectory(ResolvePlanningWorkspacePath(config.RootPath, config.IncomingHandoffRoot));
            Directory.CreateDirectory(ResolvePlanningWorkspacePath(config.RootPath, config.ArtifactRoot));

            var reviewStore = new JsonProjectReviewStore();
            var requestStore = new JsonProjectRequestStore();
            var candidateStore = new JsonProjectCandidateStore();
            reviewStore.Save(ResolvePlanningWorkspacePath(config.RootPath, config.ReviewDataPath), new ProjectReviewData());
            requestStore.Save(ResolvePlanningWorkspacePath(config.RootPath, config.RequestDataPath), new ProjectRequestData());
            candidateStore.Save(ResolvePlanningWorkspacePath(config.RootPath, config.CandidateDataPath), new ProjectCandidateData());

            var configService = new JsonProjectConfigService();
            configService.Save(exportPath, config);
            PlanningExportPath = exportPath;
            PlanningWorkspaceMessage =
                $"Created starter workspace at {config.RootPath} with sprite roots, workflow data files, and a starter profile.";
            PlanningExportMessage = $"Starter profile is ready at {exportPath}.";
            AddActivity("planning", $"Created starter project workspace at {config.RootPath}.");
        }
        catch (Exception ex)
        {
            PlanningWorkspaceMessage = $"Unable to create the starter workspace: {ex.Message}";
            AddActivity("planning", $"Starter workspace creation failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CreatePlanningAssetSkeleton()
    {
        if (!TryBuildPlanningProjectConfig(out var config, out var exportPath, out var validationMessage))
        {
            PlanningSkeletonMessage = validationMessage;
            return;
        }

        try
        {
            Directory.CreateDirectory(config.RootPath);
            Directory.CreateDirectory(ResolvePlanningWorkspacePath(config.RootPath, config.AuthoredSpriteRoot));
            Directory.CreateDirectory(ResolvePlanningWorkspacePath(config.RootPath, config.ArtifactRoot));
            Directory.CreateDirectory(Path.Combine(config.RootPath, ".sprite-workflow"));

            var authoredRoot = ResolvePlanningWorkspacePath(config.RootPath, config.AuthoredSpriteRoot);
            var checklistEntries = BuildPlanningAssetChecklistEntries(config, authoredRoot);

            var checklistDirectory = Path.Combine(config.RootPath, ".sprite-workflow");
            var checklistJsonPath = Path.Combine(checklistDirectory, "planning-checklist.json");
            var checklistMarkdownPath = Path.Combine(checklistDirectory, "planning-checklist.md");
            File.WriteAllText(
                checklistJsonPath,
                JsonSerializer.Serialize(
                    new
                    {
                        project_id = config.ProjectId,
                        display_name = config.DisplayName,
                        authored_root = authoredRoot,
                        generated_utc = DateTimeOffset.UtcNow,
                        entries = checklistEntries,
                    },
                    new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(checklistMarkdownPath, BuildPlanningChecklistMarkdown(config, checklistEntries));

            var configService = new JsonProjectConfigService();
            configService.Save(exportPath, config);
            PlanningExportPath = exportPath;
            PlanningSkeletonMessage =
                $"Created starter asset skeleton with {checklistEntries.Count} sequence entries. Checklist saved to {checklistJsonPath}.";
            PlanningExportMessage = $"Starter profile is ready at {exportPath}.";
            AddActivity("planning", $"Created starter asset skeleton at {authoredRoot}.");
        }
        catch (Exception ex)
        {
            PlanningSkeletonMessage = $"Unable to create the starter asset skeleton: {ex.Message}";
            AddActivity("planning", $"Starter asset skeleton creation failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SavePlanningTemplate()
    {
        try
        {
            var directory = Path.GetDirectoryName(_planningTemplateStorePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var templates = LoadPlanningTemplateRecords();
            var templateId = SlugifyPlanningValue(string.IsNullOrWhiteSpace(PlanningProjectId) ? PlanningDisplayName : PlanningProjectId);
            if (string.IsNullOrWhiteSpace(templateId))
            {
                templateId = $"template-{DateTime.Now:yyyyMMddHHmmss}";
            }

            var template = new PlanningTemplateRecord(
                templateId,
                string.IsNullOrWhiteSpace(PlanningDisplayName) ? templateId : PlanningDisplayName.Trim(),
                PlanningSpeciesText.Trim(),
                PlanningAgeText.Trim(),
                PlanningGenderText.Trim(),
                PlanningColorText.Trim(),
                PlanningFamilyBlueprintText.Trim(),
                DateTimeOffset.UtcNow);

            templates.RemoveAll(item => item.TemplateId.Equals(templateId, StringComparison.OrdinalIgnoreCase));
            templates.Insert(0, template);
            SavePlanningTemplateRecords(templates);
            LoadPlanningTemplates();
            SelectedPlanningTemplateItem = PlanningTemplates.FirstOrDefault(item => item.TemplateId.Equals(templateId, StringComparison.OrdinalIgnoreCase));
            PlanningTemplateMessage = $"Saved planning template '{template.Name}'.";
            AddActivity("planning", PlanningTemplateMessage);
        }
        catch (Exception ex)
        {
            PlanningTemplateMessage = $"Unable to save planning template: {ex.Message}";
            AddActivity("planning", $"Planning template save failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void LoadSelectedPlanningTemplate()
    {
        if (SelectedPlanningTemplateItem is null)
        {
            PlanningTemplateMessage = "Select a planning template first.";
            return;
        }

        PlanningSpeciesText = SelectedPlanningTemplateItem.SpeciesText;
        PlanningAgeText = SelectedPlanningTemplateItem.AgeText;
        PlanningGenderText = SelectedPlanningTemplateItem.GenderText;
        PlanningColorText = SelectedPlanningTemplateItem.ColorText;
        PlanningFamilyBlueprintText = SelectedPlanningTemplateItem.FamilyBlueprintText;
        if (!string.IsNullOrWhiteSpace(SelectedPlanningTemplateItem.Name))
        {
            PlanningDisplayName = SelectedPlanningTemplateItem.Name;
        }

        RebuildPlanningChecklist();
        NotifyPlanningStateChanged();
        PlanningTemplateMessage = $"Loaded planning template '{SelectedPlanningTemplateItem.Name}'.";
        AddActivity("planning", PlanningTemplateMessage);
    }

    [RelayCommand]
    private void DeleteSelectedPlanningTemplate()
    {
        if (SelectedPlanningTemplateItem is null)
        {
            PlanningTemplateMessage = "Select a planning template first.";
            return;
        }

        try
        {
            var templates = LoadPlanningTemplateRecords();
            templates.RemoveAll(item => item.TemplateId.Equals(SelectedPlanningTemplateItem.TemplateId, StringComparison.OrdinalIgnoreCase));
            SavePlanningTemplateRecords(templates);
            var deletedName = SelectedPlanningTemplateItem.Name;
            LoadPlanningTemplates();
            PlanningTemplateMessage = $"Deleted planning template '{deletedName}'.";
            AddActivity("planning", PlanningTemplateMessage);
        }
        catch (Exception ex)
        {
            PlanningTemplateMessage = $"Unable to delete planning template: {ex.Message}";
            AddActivity("planning", $"Planning template delete failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void GeneratePlanningBlankFrames()
    {
        if (!TryBuildPlanningProjectConfig(out var config, out _, out var validationMessage))
        {
            PlanningFrameGenerationMessage = validationMessage;
            return;
        }

        try
        {
            var authoredRoot = ResolvePlanningWorkspacePath(config.RootPath, config.AuthoredSpriteRoot);
            var checklistEntries = BuildPlanningAssetChecklistEntries(config, authoredRoot);
            var createdCount = 0;
            const int defaultCanvasWidth = 64;
            const int defaultCanvasHeight = 64;

            foreach (var entry in checklistEntries)
            {
                Directory.CreateDirectory(entry.VariantDirectory);
                foreach (var frameFile in entry.FrameFiles)
                {
                    var outputPath = Path.Combine(entry.VariantDirectory, frameFile);
                    if (File.Exists(outputPath))
                    {
                        continue;
                    }

                    WriteBlankFrame(outputPath, defaultCanvasWidth, defaultCanvasHeight);
                    createdCount++;
                }
            }

            PlanningFrameGenerationMessage = createdCount == 0
                ? "All planned frame files already existed."
                : $"Generated {createdCount} blank planned PNG frame(s) at {authoredRoot}.";
            AddActivity("planning", createdCount == 0
                ? "Blank planned frame generation skipped because all files already existed."
                : $"Generated {createdCount} blank planned frame(s) from the planning checklist.");
        }
        catch (Exception ex)
        {
            PlanningFrameGenerationMessage = $"Unable to generate planned blank frames: {ex.Message}";
            AddActivity("planning", $"Planned blank frame generation failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ExportTrustedSet()
    {
        if (string.IsNullOrWhiteSpace(_trustedExportDirectory))
        {
            TrustedExportMessage = "No trusted export folder is configured for this project.";
            return;
        }

        try
        {
            var snapshot = BuildTrustedExportSnapshot();
            Directory.CreateDirectory(_trustedExportDirectory);

            var exportDirectory = Path.Combine(_trustedExportDirectory, $"trusted-set-{DateTime.Now:yyyyMMdd-HHmmss}");
            var framesDirectory = Path.Combine(exportDirectory, "frames");
            Directory.CreateDirectory(exportDirectory);
            Directory.CreateDirectory(framesDirectory);

            var manifestEntries = new List<object>();
            foreach (var entry in snapshot.ApprovedEntries)
            {
                var relativePath = GetTrustedExportRelativePath(entry.FramePath);
                var targetPath = Path.Combine(framesDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? framesDirectory);
                File.Copy(entry.FramePath, targetPath, true);

                manifestEntries.Add(
                    new
                    {
                        entry.Species,
                        entry.Age,
                        entry.Gender,
                        entry.Color,
                        entry.Family,
                        entry.SequenceId,
                        entry.FrameIndex,
                        entry.FrameId,
                        source_frame_path = entry.FramePath,
                        exported_frame_path = targetPath,
                        relative_frame_path = relativePath,
                        status = entry.Status,
                        source = entry.Source,
                    });
            }

            var manifestPath = Path.Combine(exportDirectory, "trusted-set.json");
            var markdownPath = Path.Combine(exportDirectory, "trusted-set.md");
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(
                    new
                    {
                        project = SelectedProjectName,
                        generated_utc = DateTimeOffset.UtcNow,
                        approved_frames = snapshot.ApprovedFrameCount,
                        template_only_frames = snapshot.TemplateOnlyFrameCount,
                        flagged_frames = snapshot.FlaggedFrameCount,
                        approved_rows = snapshot.ApprovedRowCount,
                        entries = manifestEntries,
                    },
                    new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(markdownPath, BuildTrustedExportMarkdown(snapshot, manifestEntries.Count, framesDirectory));
            AppendTrustedExportHistory(new TrustedExportHistoryItemViewModel(
                Path.GetFileName(exportDirectory),
                exportDirectory,
                snapshot.ApprovedFrameCount,
                snapshot.ApprovedRowCount,
                snapshot.FlaggedFrameCount,
                DateTimeOffset.UtcNow));

            TrustedExportMessage = manifestEntries.Count == 0
                ? $"Trusted manifest exported to {manifestPath}, but no approved frame PNGs were available to mirror."
                : $"Trusted export created at {exportDirectory} with {manifestEntries.Count} approved frame PNG(s).";
            AddActivity("review", TrustedExportMessage);
        }
        catch (Exception ex)
        {
            TrustedExportMessage = $"Unable to export trusted set: {ex.Message}";
            AddActivity("review", $"Trusted export failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenSelectedTrustedExport()
    {
        if (SelectedTrustedExportHistoryItem is null || string.IsNullOrWhiteSpace(SelectedTrustedExportHistoryItem.ExportDirectory))
        {
            TrustedExportMessage = "Select a trusted export bundle first.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{SelectedTrustedExportHistoryItem.ExportDirectory}\"",
                UseShellExecute = true,
            });
            TrustedExportMessage = $"Opened {SelectedTrustedExportHistoryItem.ExportDirectory}.";
        }
        catch (Exception ex)
        {
            TrustedExportMessage = $"Unable to open trusted export: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenTrustedExportFolder()
    {
        if (string.IsNullOrWhiteSpace(_trustedExportDirectory))
        {
            TrustedExportMessage = "No trusted export folder is configured for this project.";
            return;
        }

        try
        {
            Directory.CreateDirectory(_trustedExportDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _trustedExportDirectory,
                UseShellExecute = true,
            });
            TrustedExportMessage = $"Opened trusted export folder at {_trustedExportDirectory}.";
            AddActivity("review", TrustedExportMessage);
        }
        catch (Exception ex)
        {
            TrustedExportMessage = $"Unable to open trusted export folder: {ex.Message}";
            AddActivity("review", $"Could not open trusted export folder: {ex.Message}");
        }
    }

    [RelayCommand]
    private void LoadCurrentFrameIntoEditor()
    {
        IsPlaybackEnabled = false;
        LoadEditorFrame(ViewerFramePath, true);
        if (!string.IsNullOrWhiteSpace(ViewerFramePath))
        {
            OpenPaintWorkspace();
            AddActivity("editor", $"Loaded current frame {Path.GetFileName(ViewerFramePath)}.");
        }
    }

    private bool TryBuildPlanningProjectConfig(out ProjectConfig config, out string exportPath, out string validationMessage)
    {
        var projectId = SlugifyPlanningValue(PlanningProjectId);
        var rootPath = PlanningRootPath.Trim();
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(rootPath))
        {
            config = new ProjectConfig();
            exportPath = string.Empty;
            validationMessage = "Add a project id and root path before exporting or creating a starter workspace.";
            return false;
        }

        exportPath = string.IsNullOrWhiteSpace(PlanningExportPath)
            ? Path.Combine(rootPath, $"{projectId}.project.json")
            : PlanningExportPath.Trim();

        config = new ProjectConfig
        {
            SchemaVersion = 1,
            ProjectId = projectId,
            DisplayName = string.IsNullOrWhiteSpace(PlanningDisplayName) ? projectId : PlanningDisplayName.Trim(),
            RootPath = rootPath,
            RuntimeSpriteRoot = "sprites_runtime",
            AuthoredSpriteRoot = "sprites_authored",
            IncomingHandoffRoot = "incoming_sprites",
            ArtifactRoot = "artifacts",
            ReviewDataPath = ".sprite-workflow/reviews.json",
            RequestDataPath = ".sprite-workflow/requests.json",
            CandidateDataPath = ".sprite-workflow/candidates.json",
            DefaultAiProviderId = "generic-browser-ai",
            AiProviders =
            [
                new AiProviderConfig
                {
                    ProviderId = "generic-browser-ai",
                    DisplayName = "Generic Browser AI",
                    ProviderKind = "browser_chat",
                    ExecutionMode = "manual_browser",
                    SupportsAutomation = false,
                    Notes = "Provider-agnostic visible browser handoff for prompts, generations, and repair attempts.",
                },
                new AiProviderConfig
                {
                    ProviderId = "local-hidden-tools",
                    DisplayName = "Local Hidden Workflow Tools",
                    ProviderKind = "local_process",
                    ExecutionMode = "hidden_process",
                    SupportsAutomation = true,
                    Notes = "Use app-owned hidden processes for validation, exports, and other local automation helpers.",
                }
            ],
            VariantAxes = new VariantAxesConfig
            {
                Species = ParsePlanningList(PlanningSpeciesText).ToArray(),
                Age = ParsePlanningList(PlanningAgeText).ToArray(),
                Gender = ParsePlanningList(PlanningGenderText).ToArray(),
                Color = ParsePlanningList(PlanningColorText).ToArray(),
            },
            Families = ParsePlanningBlueprint(PlanningFamilyBlueprintText)
                .ToDictionary(
                    pair => pair.Family,
                    pair => pair.Sequences.Select(sequence => new AnimationSequenceConfig
                    {
                        SequenceId = sequence.SequenceId,
                        FrameCount = sequence.FrameCount,
                    }).ToArray(),
                    StringComparer.OrdinalIgnoreCase),
            WorkflowActions = [],
        };

        validationMessage = string.Empty;
        return true;
    }

    private static string ResolvePlanningWorkspacePath(string rootPath, string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return rootPath;
        }

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(rootPath, configuredPath);
    }

    [RelayCommand]
    private void LoadRuntimeFrameIntoEditor()
    {
        if (string.IsNullOrWhiteSpace(RuntimeViewerFramePath) || !File.Exists(RuntimeViewerFramePath))
        {
            EditorStatusMessage = "No runtime frame is available for the current slot.";
            AddActivity("editor", "Runtime template load skipped because no runtime frame is available.");
            return;
        }

        IsPlaybackEnabled = false;
        LoadEditorFrame(RuntimeViewerFramePath, true, ViewerFramePath);
        OpenPaintWorkspace();
        AddActivity("editor", $"Loaded runtime template {Path.GetFileName(RuntimeViewerFramePath)}.");
    }

    [RelayCommand]
    private void CreateBlankCurrentFrame()
    {
        if (!TryGetCurrentAuthoredFrameTargetPath(out var targetPath))
        {
            EditorStatusMessage = "Choose a valid row, family, sequence, and color before creating a blank frame.";
            return;
        }

        if (IsEditorDirty &&
            !string.IsNullOrWhiteSpace(_editorSaveFramePath) &&
            !_editorSaveFramePath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
        {
            EditorStatusMessage = "Save or revert the current edit before creating a different blank frame.";
            return;
        }

        try
        {
            var (width, height) = GetPreferredCanvasSizeForCurrentSlot();
            WriteBlankFrame(targetPath, width, height);
            IsPlaybackEnabled = false;
            LoadEditorFrame(targetPath, true);
            OpenPaintWorkspace();
            UpdateViewer();
            EditorStatusMessage = $"Created blank frame {Path.GetFileName(targetPath)} ({width}x{height}).";
            AddActivity("editor", $"Created blank frame {Path.GetFileName(targetPath)}.");
        }
        catch (Exception ex)
        {
            EditorStatusMessage = $"Unable to create blank frame: {ex.Message}";
            AddActivity("editor", $"Blank frame creation failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CreateBlankCurrentSequence()
    {
        var sequence = GetSelectedSequence();
        if (SelectedBaseVariant is null || sequence is null || string.IsNullOrWhiteSpace(SelectedViewerColor))
        {
            EditorStatusMessage = "Choose a valid row, family, sequence, and color before creating a blank sequence.";
            return;
        }

        if (IsEditorDirty)
        {
            EditorStatusMessage = "Save or revert the current edit before creating a blank sequence.";
            return;
        }

        try
        {
            var (width, height) = GetPreferredCanvasSizeForCurrentSlot();
            var createdCount = 0;
            for (var index = 0; index < sequence.FrameCount; index++)
            {
                if (!TryGetAuthoredFramePathForOffset(index - _currentFrameIndex, out var targetPath))
                {
                    continue;
                }

                if (File.Exists(targetPath))
                {
                    continue;
                }

                WriteBlankFrame(targetPath, width, height);
                createdCount++;
            }

            UpdateViewer();
            EditorStatusMessage = createdCount == 0
                ? "All frames in this sequence already exist."
                : $"Created {createdCount} blank frame(s) for {sequence.SequenceId}.";
            AddActivity("editor", createdCount == 0
                ? $"Blank sequence skipped for {sequence.SequenceId}; all frames already existed."
                : $"Created {createdCount} blank frame(s) for {sequence.SequenceId}.");
        }
        catch (Exception ex)
        {
            EditorStatusMessage = $"Unable to create blank sequence: {ex.Message}";
            AddActivity("editor", $"Blank sequence creation failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenAnimateWorkspace()
    {
        SurfaceAnimateWorkspace(true);
    }

    [RelayCommand]
    private void OpenPaintWorkspace()
    {
        IsPlaybackEnabled = false;
        SurfacePaintWorkspace(true);
    }

    [RelayCommand]
    private void SaveAndReturnToAnimate()
    {
        if (!IsEditorFrameLoaded)
        {
            EditorStatusMessage = "Load a frame into Paint before using Save + Replay.";
            return;
        }

        var hadUnsavedChanges = IsEditorDirty;
        if (hadUnsavedChanges)
        {
            SaveEditedFrame();
            if (IsEditorDirty)
            {
                return;
            }
        }

        SelectedWorkspaceTabIndex = 4;
        SelectedStudioTabIndex = 0;
        AddActivity("editor", hadUnsavedChanges
            ? "Saved the current frame and returned to Animate."
            : "Returned to Animate to review the current frame.");
    }

    [RelayCommand]
    private void ClearEditorFrame()
    {
        if (_editorPixels.Length == 0)
        {
            EditorStatusMessage = "Load a frame before clearing it.";
            return;
        }

        CaptureUndoState();
        for (var index = 0; index < _editorPixels.Length; index++)
        {
            ApplyColorToPixel(index, new Rgba32(0, 0, 0, 0));
        }

        EditorStatusMessage = "Cleared the current frame.";
    }

    [RelayCommand]
    private void UndoEditorEdit()
    {
        if (_undoHistory.Count == 0 || _editorPixels.Length == 0)
        {
            EditorStatusMessage = "Nothing to undo.";
            return;
        }

        var activePixels = GetActiveLayerPixels();
        if (activePixels is null)
        {
            EditorStatusMessage = "No active layer to undo.";
            return;
        }

        _redoHistory.Push(new EditorHistoryState(_activeEditorLayerId, (Rgba32[])activePixels.Clone()));
        ApplyHistoryState(_undoHistory.Pop());
        IsEditorDirty = true;
        EditorStatusMessage = "Undid the last editor change.";
        NotifyEditorHistoryChanged();
    }

    [RelayCommand]
    private void RedoEditorEdit()
    {
        if (_redoHistory.Count == 0 || _editorPixels.Length == 0)
        {
            EditorStatusMessage = "Nothing to redo.";
            return;
        }

        var activePixels = GetActiveLayerPixels();
        if (activePixels is null)
        {
            EditorStatusMessage = "No active layer to redo.";
            return;
        }

        _undoHistory.Push(new EditorHistoryState(_activeEditorLayerId, (Rgba32[])activePixels.Clone()));
        ApplyHistoryState(_redoHistory.Pop());
        IsEditorDirty = true;
        EditorStatusMessage = "Redid the last editor change.";
        NotifyEditorHistoryChanged();
    }

    [RelayCommand]
    private async Task RunWorkflowAction(WorkflowActionItemViewModel? action)
    {
        if (action is null)
        {
            return;
        }

        if (_workflowRunner is null)
        {
            action.RunStatus = "Unavailable";
            action.LastOutputPreview = "No workflow runner is configured for this project.";
            AddActivity("automation", $"{action.Name} unavailable because no workflow runner is configured.");
            return;
        }

        if (!action.CanRunHidden)
        {
            action.RunStatus = "External";
            action.LastOutputPreview = action.ActionHint;
            AddActivity("automation", $"{action.Name} remains an external step.");
            return;
        }

        if (action.IsRunning)
        {
            return;
        }

        action.IsRunning = true;
        action.RunStatus = "Running hidden";
        action.LastOutputPreview = "Process started in the background. It will stop automatically when the app closes.";
        AddActivity("automation", $"Started {action.Name}.");

        try
        {
            var result = await _workflowRunner.RunHiddenAsync(
                new WorkflowActionLaunchRequest(
                    action.ActionId,
                    action.Name,
                    action.Command,
                    action.Arguments,
                    action.WorkingDirectory));

            action.LastRunUtc = DateTimeOffset.UtcNow;
            action.RunStatus = result.WasStopped
                ? "Stopped"
                : result.ExitCode == 0
                    ? "Completed"
                    : $"Failed ({result.ExitCode})";
            action.LastOutputPreview = SummarizeWorkflowOutput(result.Output, result.WasStopped);
            AddActivity("automation", $"{action.Name} {action.RunStatus.ToLowerInvariant()}.");
        }
        catch (Exception ex)
        {
            action.LastRunUtc = DateTimeOffset.UtcNow;
            action.RunStatus = "Failed";
            action.LastOutputPreview = ex.Message;
            AddActivity("automation", $"{action.Name} failed: {ex.Message}");
        }
        finally
        {
            action.IsRunning = false;
        }
    }

    [RelayCommand]
    private void StopWorkflowAction(WorkflowActionItemViewModel? action)
    {
        if (action is null || _workflowRunner is null)
        {
            return;
        }

        if (!action.CanStop)
        {
            return;
        }

        if (_workflowRunner.TryStop(action.ActionId))
        {
            action.RunStatus = "Stopping";
            action.LastOutputPreview = "Stopping hidden process tree...";
            AddActivity("automation", $"Stopping {action.Name}.");
        }
    }

    [RelayCommand]
    private void StopAllAutomation()
    {
        if (_workflowRunner is null)
        {
            AddActivity("automation", "Stop all skipped because no workflow runner is configured.");
        }
        else
        {
            var stoppedCount = _workflowRunner.StopAll();
            foreach (var action in WorkflowActions.Where(action => action.IsRunning))
            {
                action.RunStatus = "Stopping";
                action.LastOutputPreview = "Stopping hidden process tree...";
            }

            AddActivity("automation", stoppedCount > 0
                ? $"Stop all requested for {stoppedCount} running hidden actions."
                : "Stop all requested, but no hidden actions were running.");
        }

        var pausedAnyTasks = false;
        foreach (var task in AutomationQueueItems.Where(task => task.Status.Equals("running", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            UpdateRequestStatus(task.RequestId, "paused");
            pausedAnyTasks = true;
        }

        if (pausedAnyTasks)
        {
            ControlMode = "manual";
            AddActivity("automation", "Paused running AI queue items and returned control to manual mode.");
        }
    }

    [RelayCommand]
    private void QueueSelectedRequestForAutomation()
    {
        if (SelectedRequestItem is null)
        {
            RequestSaveMessage = "Select a saved request first.";
            return;
        }

        UpdateRequestStatus(SelectedRequestItem.RequestId, "queued");
        AppendRequestHistory(SelectedRequestItem.RequestId, "queued", "Queued for assisted AI automation.");
        RequestSaveMessage = $"Queued '{SelectedRequestItem.Title}' for automation.";
        AddActivity("automation", $"Queued request {SelectedRequestItem.RequestId}.");
    }

    [RelayCommand]
    private void StartSelectedAutomationTask()
    {
        if (SelectedAutomationTaskItem is null)
        {
            RequestSaveMessage = "Select an automation task first.";
            return;
        }

        foreach (var runningTask in AutomationQueueItems.Where(task => task.Status.Equals("running", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (!runningTask.RequestId.Equals(SelectedAutomationTaskItem.RequestId, StringComparison.OrdinalIgnoreCase))
            {
                UpdateRequestStatus(runningTask.RequestId, "paused");
            }
        }

        UpdateRequestStatus(SelectedAutomationTaskItem.RequestId, "running");
        AppendRequestHistory(SelectedAutomationTaskItem.RequestId, "started", "AI automation focus started on this request.");
        ControlMode = "assisted_ai";
        RequestSaveMessage = $"Automation focus set to '{SelectedAutomationTaskItem.DisplayName}'.";
        AddActivity("automation", $"Started automation task {SelectedAutomationTaskItem.RequestId}.");
    }

    [RelayCommand]
    private void PauseSelectedAutomationTask()
    {
        if (SelectedAutomationTaskItem is null)
        {
            RequestSaveMessage = "Select an automation task first.";
            return;
        }

        UpdateRequestStatus(SelectedAutomationTaskItem.RequestId, "paused");
        AppendRequestHistory(SelectedAutomationTaskItem.RequestId, "paused", "Paused and returned control to manual editing.");
        ControlMode = "manual";
        RequestSaveMessage = $"Paused '{SelectedAutomationTaskItem.DisplayName}' and returned control to manual mode.";
        AddActivity("automation", $"Paused automation task {SelectedAutomationTaskItem.RequestId}.");
    }

    [RelayCommand]
    private void ResumeSelectedAutomationTask()
    {
        if (SelectedAutomationTaskItem is null)
        {
            RequestSaveMessage = "Select an automation task first.";
            return;
        }

        UpdateRequestStatus(SelectedAutomationTaskItem.RequestId, "queued");
        AppendRequestHistory(SelectedAutomationTaskItem.RequestId, "resumed", "Returned request to the assisted AI queue.");
        ControlMode = "assisted_ai";
        RequestSaveMessage = $"Returned '{SelectedAutomationTaskItem.DisplayName}' to the automation queue.";
        AddActivity("automation", $"Returned automation task {SelectedAutomationTaskItem.RequestId} to the queue.");
    }

    [RelayCommand]
    private void TakeManualControl()
    {
        ControlMode = "manual";
        foreach (var task in AutomationQueueItems.Where(task => task.Status.Equals("running", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            UpdateRequestStatus(task.RequestId, "paused");
        }

        RequestSaveMessage = "Manual control enabled. The current row, frame, and editor state were kept in place.";
        AddActivity("session", "Manual control enabled from the automation workspace.");
    }

    [RelayCommand]
    private void ReturnToAssistedAi()
    {
        ControlMode = "assisted_ai";
        RequestSaveMessage = "Assisted AI control enabled. Visible queue state and current editor context were preserved.";
        AddActivity("session", "Returned control to assisted AI mode.");
    }

    [RelayCommand]
    private void RecordSelectedAutomationGenerated()
    {
        RecordSelectedAutomationAttempt(
            "ai_generated_candidate",
            "AI generated a new candidate pass for visible review and staging.",
            "running");
    }

    [RelayCommand]
    private void RecordSelectedAutomationNeedsManualTouchup()
    {
        RecordSelectedAutomationAttempt(
            "manual_touchup_needed",
            "AI pass needs manual touchup in Studio before approval or export.",
            "paused");
    }

    [RelayCommand]
    private void RecordSelectedAutomationRejected()
    {
        RecordSelectedAutomationAttempt(
            "ai_rejected",
            "Rejected the current AI pass and kept the request in a paused state for revision.",
            "paused");
    }

    [RelayCommand]
    private void RecordSelectedAutomationApproved()
    {
        RecordSelectedAutomationAttempt(
            "ai_approved",
            "Approved the current AI/manual result and marked the request complete.",
            "completed");
    }

    [RelayCommand]
    private void RunProjectValidation()
    {
        if (string.IsNullOrWhiteSpace(PlanningRootPath))
        {
            ProjectValidationMessage = "Add or load a project root before running validation.";
            return;
        }

        try
        {
            Directory.CreateDirectory(_projectValidationReportDirectory);

            var report = BuildProjectValidationReport();
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var reportId = $"validation-{timestamp}";
            var markdownPath = Path.Combine(_projectValidationReportDirectory, $"{reportId}.md");
            var jsonPath = Path.Combine(_projectValidationReportDirectory, $"{reportId}.json");

            File.WriteAllText(markdownPath, BuildProjectValidationMarkdown(report));
            File.WriteAllText(
                jsonPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

            LoadValidationReports();
            SelectedValidationReportItem = ValidationReports.FirstOrDefault(item => item.ReportId.Equals(reportId, StringComparison.OrdinalIgnoreCase));
            ProjectValidationMessage = $"Validation report created at {markdownPath}.";
            AddActivity("planning", $"Generated project validation report {reportId}.");
        }
        catch (Exception ex)
        {
            ProjectValidationMessage = $"Unable to run validation: {ex.Message}";
            AddActivity("planning", $"Project validation failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenSelectedValidationReport()
    {
        if (SelectedValidationReportItem is null)
        {
            ProjectValidationMessage = "Select a validation report first.";
            return;
        }

        var targetPath = SelectedValidationReportItem.HasMarkdownPath
            ? SelectedValidationReportItem.MarkdownPath
            : SelectedValidationReportItem.JsonPath;
        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
        {
            ProjectValidationMessage = "The selected validation report file is missing.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true,
            });
            ProjectValidationMessage = $"Opened {targetPath}.";
            AddActivity("planning", $"Opened validation report {SelectedValidationReportItem.DisplayName}.");
        }
        catch (Exception ex)
        {
            ProjectValidationMessage = $"Unable to open validation report: {ex.Message}";
            AddActivity("planning", $"Could not open validation report: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenValidationReportFolder()
    {
        if (string.IsNullOrWhiteSpace(_projectValidationReportDirectory))
        {
            ProjectValidationMessage = "No validation report folder is configured for this project.";
            return;
        }

        try
        {
            Directory.CreateDirectory(_projectValidationReportDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _projectValidationReportDirectory,
                UseShellExecute = true,
            });
            ProjectValidationMessage = $"Opened validation report folder at {_projectValidationReportDirectory}.";
            AddActivity("planning", ProjectValidationMessage);
        }
        catch (Exception ex)
        {
            ProjectValidationMessage = $"Unable to open validation report folder: {ex.Message}";
            AddActivity("planning", $"Could not open validation report folder: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ExportProjectKit()
    {
        if (!TryBuildPlanningProjectConfig(out var config, out var exportPath, out var validationMessage))
        {
            ProjectKitMessage = validationMessage;
            return;
        }

        try
        {
            Directory.CreateDirectory(_projectKitDirectory);
            var kitId = $"{config.ProjectId}-kit-{DateTime.Now:yyyyMMdd-HHmmss}";
            var kitDirectory = Path.Combine(_projectKitDirectory, kitId);
            Directory.CreateDirectory(kitDirectory);

            var configService = new JsonProjectConfigService();
            var configTargetPath = Path.Combine(kitDirectory, Path.GetFileName(exportPath));
            configService.Save(configTargetPath, config);

            CopyIfExists(_reviewDataPath, Path.Combine(kitDirectory, "reviews.json"));
            CopyIfExists(_requestDataPath, Path.Combine(kitDirectory, "requests.json"));
            CopyIfExists(_candidateDataPath, Path.Combine(kitDirectory, "candidates.json"));
            CopyIfExists(_projectPaletteStorePath, Path.Combine(kitDirectory, "project-palettes.json"));
            CopyIfExists(_trustedExportHistoryPath, Path.Combine(kitDirectory, "trusted-export-history.json"));

            var latestValidationMarkdown = ValidationReports.FirstOrDefault()?.MarkdownPath;
            var latestValidationJson = ValidationReports.FirstOrDefault()?.JsonPath;
            CopyIfExists(latestValidationMarkdown, Path.Combine(kitDirectory, "latest-validation.md"));
            CopyIfExists(latestValidationJson, Path.Combine(kitDirectory, "latest-validation.json"));

            var planningChecklistPath = Path.Combine(config.RootPath, ".sprite-workflow", "planning-checklist.json");
            var planningChecklistMarkdownPath = Path.Combine(config.RootPath, ".sprite-workflow", "planning-checklist.md");
            CopyIfExists(planningChecklistPath, Path.Combine(kitDirectory, "planning-checklist.json"));
            CopyIfExists(planningChecklistMarkdownPath, Path.Combine(kitDirectory, "planning-checklist.md"));

            File.WriteAllText(Path.Combine(kitDirectory, "README.md"), BuildProjectKitReadme(config, configTargetPath));

            ProjectKitMessage = $"Exported reusable project kit to {kitDirectory}.";
            AddActivity("planning", ProjectKitMessage);
        }
        catch (Exception ex)
        {
            ProjectKitMessage = $"Unable to export project kit: {ex.Message}";
            AddActivity("planning", $"Project kit export failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenProjectKitFolder()
    {
        if (string.IsNullOrWhiteSpace(_projectKitDirectory))
        {
            ProjectKitMessage = "No project kit folder is configured for this project.";
            return;
        }

        try
        {
            Directory.CreateDirectory(_projectKitDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _projectKitDirectory,
                UseShellExecute = true,
            });
            ProjectKitMessage = $"Opened project kit folder at {_projectKitDirectory}.";
            AddActivity("planning", ProjectKitMessage);
        }
        catch (Exception ex)
        {
            ProjectKitMessage = $"Unable to open project kit folder: {ex.Message}";
            AddActivity("planning", $"Could not open project kit folder: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CreateValidationSandbox()
    {
        if (!TryBuildPlanningProjectConfig(out var config, out _, out var validationMessage))
        {
            ValidationSandboxMessage = validationMessage;
            return;
        }

        try
        {
            Directory.CreateDirectory(_validationSandboxDirectory);
            var sandboxRoot = Path.Combine(_validationSandboxDirectory, $"{config.ProjectId}-sandbox");
            if (Directory.Exists(sandboxRoot))
            {
                Directory.Delete(sandboxRoot, true);
            }

            config = CloneProjectConfigForRoot(config, sandboxRoot);
            var sandboxConfigPath = Path.Combine(sandboxRoot, $"{config.ProjectId}.project.json");
            var configService = new JsonProjectConfigService();

            Directory.CreateDirectory(sandboxRoot);
            Directory.CreateDirectory(ResolvePlanningWorkspacePath(sandboxRoot, config.RuntimeSpriteRoot));
            Directory.CreateDirectory(ResolvePlanningWorkspacePath(sandboxRoot, config.AuthoredSpriteRoot));
            Directory.CreateDirectory(ResolvePlanningWorkspacePath(sandboxRoot, config.IncomingHandoffRoot));
            Directory.CreateDirectory(ResolvePlanningWorkspacePath(sandboxRoot, config.ArtifactRoot));
            Directory.CreateDirectory(Path.Combine(sandboxRoot, ".sprite-workflow"));

            var reviewStore = new JsonProjectReviewStore();
            var requestStore = new JsonProjectRequestStore();
            var candidateStore = new JsonProjectCandidateStore();
            reviewStore.Save(ResolvePlanningWorkspacePath(sandboxRoot, config.ReviewDataPath), new ProjectReviewData());
            requestStore.Save(ResolvePlanningWorkspacePath(sandboxRoot, config.RequestDataPath), new ProjectRequestData());
            candidateStore.Save(ResolvePlanningWorkspacePath(sandboxRoot, config.CandidateDataPath), new ProjectCandidateData());

            var authoredRoot = ResolvePlanningWorkspacePath(sandboxRoot, config.AuthoredSpriteRoot);
            var checklistEntries = BuildPlanningAssetChecklistEntries(config, authoredRoot);
            foreach (var entry in checklistEntries)
            {
                Directory.CreateDirectory(entry.VariantDirectory);
                foreach (var frameFile in entry.FrameFiles)
                {
                    var outputPath = Path.Combine(entry.VariantDirectory, frameFile);
                    if (!File.Exists(outputPath))
                    {
                        WriteBlankFrame(outputPath, 64, 64);
                    }
                }
            }

            File.WriteAllText(
                Path.Combine(sandboxRoot, ".sprite-workflow", "planning-checklist.md"),
                BuildPlanningChecklistMarkdown(config, checklistEntries));
            configService.Save(sandboxConfigPath, config);

            ValidationSandboxMessage = $"Created validation sandbox at {sandboxRoot}.";
            AddActivity("planning", ValidationSandboxMessage);
        }
        catch (Exception ex)
        {
            ValidationSandboxMessage = $"Unable to create validation sandbox: {ex.Message}";
            AddActivity("planning", $"Validation sandbox creation failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenValidationSandboxFolder()
    {
        if (string.IsNullOrWhiteSpace(_validationSandboxDirectory))
        {
            ValidationSandboxMessage = "No validation sandbox folder is configured for this project.";
            return;
        }

        try
        {
            Directory.CreateDirectory(_validationSandboxDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _validationSandboxDirectory,
                UseShellExecute = true,
            });
            ValidationSandboxMessage = $"Opened validation sandbox folder at {_validationSandboxDirectory}.";
            AddActivity("planning", ValidationSandboxMessage);
        }
        catch (Exception ex)
        {
            ValidationSandboxMessage = $"Unable to open validation sandbox folder: {ex.Message}";
            AddActivity("planning", $"Could not open validation sandbox folder: {ex.Message}");
        }
    }

    [RelayCommand]
    private void JumpToSelectedTrustedExportBlocker()
    {
        if (SelectedTrustedExportBlockerItem is null)
        {
            TrustedExportMessage = "Select a trusted export blocker first.";
            return;
        }

        NavigateToTrustedExportBlocker(SelectedTrustedExportBlockerItem);
    }

    [RelayCommand]
    private void FocusAutomationCandidate(CandidateItemViewModel? candidate)
    {
        if (candidate is null)
        {
            return;
        }

        SelectedCandidateItem = candidate;
        SelectedWorkspaceTabIndex = 3;
        CandidateSaveMessage = $"Focused candidate '{candidate.Title}'.";
        AddActivity("candidate", $"Focused linked candidate '{candidate.Title}' from the automation workspace.");
    }

    [RelayCommand]
    private void LoadAutomationCandidateIntoPaint(CandidateItemViewModel? candidate)
    {
        if (candidate is null)
        {
            return;
        }

        SelectedCandidateItem = candidate;
        LoadSelectedCandidateIntoPaint();
    }

    [RelayCommand]
    private void OpenSelectedPlanningUnmappedAsset()
    {
        if (SelectedPlanningUnmappedEntryItem is null || string.IsNullOrWhiteSpace(_authoredSpriteRoot))
        {
            PlanningDiscoveryAdoptionMessage = "Select an unmapped discovered asset first.";
            return;
        }

        var fullPath = Path.Combine(_authoredSpriteRoot, SelectedPlanningUnmappedEntryItem.RelativePath);
        if (!File.Exists(fullPath))
        {
            PlanningDiscoveryAdoptionMessage = "The selected unmapped asset no longer exists on disk.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{fullPath}\"",
                UseShellExecute = true,
            });
            PlanningDiscoveryAdoptionMessage = $"Opened {SelectedPlanningUnmappedEntryItem.RelativePath}.";
            AddActivity("planning", PlanningDiscoveryAdoptionMessage);
        }
        catch (Exception ex)
        {
            PlanningDiscoveryAdoptionMessage = $"Unable to open unmapped asset: {ex.Message}";
            AddActivity("planning", $"Could not open unmapped asset: {ex.Message}");
        }
    }

    [RelayCommand]
    private void AdoptSelectedPlanningUnmappedEntry()
    {
        if (SelectedPlanningUnmappedEntryItem is null)
        {
            PlanningDiscoveryAdoptionMessage = "Select an unmapped discovered asset first.";
            return;
        }

        var family = string.IsNullOrWhiteSpace(SelectedPlanningUnmappedEntryItem.Family)
            ? "custom"
            : SelectedPlanningUnmappedEntryItem.Family;
        var sequenceId = string.IsNullOrWhiteSpace(SelectedPlanningUnmappedEntryItem.SequenceId)
            ? Path.GetFileNameWithoutExtension(SelectedPlanningUnmappedEntryItem.RelativePath)
            : SelectedPlanningUnmappedEntryItem.SequenceId;
        var frameCount = DiscoverFrameCountForSequence(SelectedPlanningUnmappedEntryItem.RelativePath, sequenceId);

        var blueprint = ParsePlanningBlueprint(PlanningFamilyBlueprintText);
        var familyIndex = blueprint.FindIndex(item => item.Family.Equals(family, StringComparison.OrdinalIgnoreCase));
        if (familyIndex < 0)
        {
            blueprint.Add((family, [(sequenceId, frameCount)]));
        }
        else
        {
            var sequences = blueprint[familyIndex].Sequences;
            var sequenceIndex = sequences.FindIndex(item => item.SequenceId.Equals(sequenceId, StringComparison.OrdinalIgnoreCase));
            if (sequenceIndex < 0)
            {
                sequences.Add((sequenceId, frameCount));
            }
            else
            {
                sequences[sequenceIndex] = (sequenceId, Math.Max(sequences[sequenceIndex].FrameCount, frameCount));
            }

            blueprint[familyIndex] = (blueprint[familyIndex].Family, sequences);
        }

        PlanningFamilyBlueprintText = BuildFamilyBlueprintText(blueprint);
        NotifyPlanningStateChanged();
        PlanningDiscoveryAdoptionMessage = $"Adopted {family}/{sequenceId} into the planning blueprint.";
        AddActivity("planning", PlanningDiscoveryAdoptionMessage);
    }

    [RelayCommand]
    private void OpenWorkflowActionFolder(WorkflowActionItemViewModel? action)
    {
        if (action is null || string.IsNullOrWhiteSpace(action.WorkingDirectory))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = action.WorkingDirectory,
                UseShellExecute = true,
            });
            action.LastOutputPreview = $"Opened {action.WorkingDirectory}.";
            AddActivity("automation", $"Opened folder for {action.Name}.");
        }
        catch (Exception ex)
        {
            action.LastOutputPreview = $"Unable to open folder: {ex.Message}";
            AddActivity("automation", $"Could not open folder for {action.Name}: {ex.Message}");
        }
    }

    [RelayCommand]
    private void UseEditorPaletteColor(EditorPaletteColorItemViewModel? color)
    {
        if (color is null)
        {
            return;
        }

        SelectedEditorColorHex = color.HexColor;
        UpdatePaletteSelection();
        EditorStatusMessage = $"Selected {color.HexColor} for the brush.";
    }

    [RelayCommand]
    private void UseQuickColorPreset(ColorPresetItemViewModel? preset)
    {
        if (preset is null)
        {
            return;
        }

        SelectedEditorColorHex = preset.HexColor;
        UpdatePaletteSelection();
        EditorStatusMessage = $"Selected {preset.HexColor} from the quick color picker.";
    }

    [RelayCommand]
    private void SelectEditorLayer(EditorLayerItemViewModel? layer)
    {
        if (layer is null || !_editorLayerPixels.ContainsKey(layer.LayerId))
        {
            return;
        }

        _activeEditorLayerId = layer.LayerId;
        RefreshEditorComposite();
        UpdateLayerSelection();
        EditorStatusMessage = $"Editing {layer.Name}.";
        OnPropertyChanged(nameof(EditorLayerSummary));
    }

    [RelayCommand]
    private void MoveEditorLayerUp(EditorLayerItemViewModel? layer)
    {
        MoveEditorLayer(layer, towardTop: true);
    }

    [RelayCommand]
    private void MoveEditorLayerDown(EditorLayerItemViewModel? layer)
    {
        MoveEditorLayer(layer, towardTop: false);
    }

    [RelayCommand]
    private void ToggleEditorLayerLock(EditorLayerItemViewModel? layer)
    {
        if (layer is null)
        {
            return;
        }

        layer.IsLocked = !layer.IsLocked;
        EditorStatusMessage = $"{layer.Name} {(layer.IsLocked ? "locked" : "unlocked")}.";
    }

    [RelayCommand]
    private void ToggleEditorLayerVisibility(EditorLayerItemViewModel? layer)
    {
        if (layer is null)
        {
            return;
        }

        layer.IsVisible = !layer.IsVisible;
        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorComparisonState();
        EditorStatusMessage = $"{layer.Name} {(layer.IsVisible ? "shown" : "hidden")}.";
    }

    [RelayCommand]
    private void AddBlankEditorLayer()
    {
        if (_editorWidth <= 0 || _editorHeight <= 0)
        {
            EditorStatusMessage = "Load a frame before adding layers.";
            return;
        }

        var layer = CreateEditorLayer("Paint layer", new Rgba32[_editorWidth * _editorHeight], isVisible: true, selectLayer: true, isLocked: false);
        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorComparisonState();
        EditorStatusMessage = $"Added {layer.Name}.";
    }

    [RelayCommand]
    private void DuplicateActiveEditorLayer()
    {
        var activeLayer = GetActiveLayerItem();
        var activePixels = GetActiveLayerPixels();
        if (activeLayer is null || activePixels is null)
        {
            EditorStatusMessage = "No active layer to duplicate.";
            return;
        }

        var layer = CreateEditorLayer($"{activeLayer.Name} Copy", (Rgba32[])activePixels.Clone(), isVisible: activeLayer.IsVisible, selectLayer: true, isLocked: activeLayer.IsLocked);
        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorComparisonState();
        EditorStatusMessage = $"Duplicated {activeLayer.Name} into {layer.Name}.";
        AddActivity("editor", EditorStatusMessage);
    }

    [RelayCommand]
    private void AddRuntimeTemplateLayer()
    {
        if (string.IsNullOrWhiteSpace(RuntimeViewerFramePath) || !File.Exists(RuntimeViewerFramePath))
        {
            EditorStatusMessage = "No runtime frame is available to use as a template layer.";
            return;
        }

        if (TryAddReferenceLayer(RuntimeViewerFramePath, "Runtime Template", 55, out var successMessage, out var errorMessage))
        {
            EditorStatusMessage = successMessage;
            AddActivity("editor", EditorStatusMessage);
        }
        else
        {
            EditorStatusMessage = errorMessage;
        }
    }

    [RelayCommand]
    private void AddPreviousFrameReferenceLayer()
    {
        if (!TryGetAuthoredFramePathForOffset(-1, out var previousPath) || !File.Exists(previousPath))
        {
            EditorStatusMessage = "No previous authored frame is available for a reference layer.";
            return;
        }

        if (TryAddReferenceLayer(previousPath, "Previous Reference", 50, out var successMessage, out var errorMessage))
        {
            EditorStatusMessage = successMessage;
            AddActivity("editor", EditorStatusMessage);
        }
        else
        {
            EditorStatusMessage = errorMessage;
        }
    }

    [RelayCommand]
    private void AddNextFrameReferenceLayer()
    {
        if (!TryGetAuthoredFramePathForOffset(1, out var nextPath) || !File.Exists(nextPath))
        {
            EditorStatusMessage = "No next authored frame is available for a reference layer.";
            return;
        }

        if (TryAddReferenceLayer(nextPath, "Next Reference", 50, out var successMessage, out var errorMessage))
        {
            EditorStatusMessage = successMessage;
            AddActivity("editor", EditorStatusMessage);
        }
        else
        {
            EditorStatusMessage = errorMessage;
        }
    }

    [RelayCommand]
    private void LiftSelectionToNewLayer()
    {
        if (!CanEditActiveLayer())
        {
            return;
        }

        var activeLayer = GetActiveLayerItem();
        var activePixels = GetActiveLayerPixels();
        if (activeLayer is null || activePixels is null)
        {
            EditorStatusMessage = "No editable layer is selected.";
            return;
        }

        if (_selectedEditorIndices.Count == 0)
        {
            EditorStatusMessage = "Select pixels before lifting them into a new layer.";
            return;
        }

        var selectedPixels = GetSelectedPixelsSnapshot();
        if (selectedPixels.Count == 0)
        {
            EditorStatusMessage = "The current selection has no pixels to lift.";
            return;
        }

        CaptureUndoState();
        var liftedPixels = new Rgba32[_editorWidth * _editorHeight];
        foreach (var pixel in selectedPixels)
        {
            var index = (pixel.Y * _editorWidth) + pixel.X;
            liftedPixels[index] = pixel.Color;
            ApplyColorToPixel(index, new Rgba32(0, 0, 0, 0));
        }

        var layer = CreateEditorLayer($"{activeLayer.Name} Lift", liftedPixels, isVisible: true, selectLayer: true, isLocked: false);
        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorPalette();
        RefreshEditorComparisonState();
        SetSelectedEditorIndices(selectedPixels.Select(pixel => (pixel.Y * _editorWidth) + pixel.X));
        IsEditorDirty = true;
        EditorStatusMessage = $"Lifted {selectedPixels.Count} pixels from {activeLayer.Name} into {layer.Name}.";
        AddActivity("editor", EditorStatusMessage);
    }

    [RelayCommand]
    private void CopySelectionToNewLayer()
    {
        var activeLayer = GetActiveLayerItem();
        if (activeLayer is null)
        {
            EditorStatusMessage = "No active layer to copy from.";
            return;
        }

        if (_selectedEditorIndices.Count == 0)
        {
            EditorStatusMessage = "Select pixels before copying them into a new layer.";
            return;
        }

        var selectedPixels = GetSelectedPixelsSnapshot()
            .Where(pixel => pixel.Color.A > 0)
            .ToList();
        if (selectedPixels.Count == 0)
        {
            EditorStatusMessage = "The current selection has no painted pixels to copy.";
            return;
        }

        CaptureUndoState();
        var copiedPixels = new Rgba32[_editorWidth * _editorHeight];
        foreach (var pixel in selectedPixels)
        {
            copiedPixels[(pixel.Y * _editorWidth) + pixel.X] = pixel.Color;
        }

        var layer = CreateEditorLayer($"{activeLayer.Name} Overlay", copiedPixels, isVisible: true, selectLayer: true, isLocked: false);
        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorPalette();
        RefreshEditorComparisonState();
        SetSelectedEditorIndices(selectedPixels.Select(pixel => (pixel.Y * _editorWidth) + pixel.X));
        IsEditorDirty = true;
        EditorStatusMessage = $"Copied {selectedPixels.Count} pixels from {activeLayer.Name} into {layer.Name}.";
        AddActivity("editor", EditorStatusMessage);
    }

    private bool TryAddReferenceLayer(string sourcePath, string layerPrefix, int opacityPercent, out string successMessage, out string errorMessage)
    {
        successMessage = string.Empty;
        errorMessage = string.Empty;

        try
        {
            foreach (var existingLayer in EditorLayers.Where(layer => layer.Name.StartsWith(layerPrefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                existingLayer.ThumbnailBitmap = null;
                DetachLayerHandlers(existingLayer);
                _editorLayerPixels.Remove(existingLayer.LayerId);
                EditorLayers.Remove(existingLayer);
            }

            using var image = Image.Load<Rgba32>(sourcePath);
            if (_editorWidth <= 0 || _editorHeight <= 0)
            {
                _editorWidth = image.Width;
                _editorHeight = image.Height;
            }

            var pixels = image.Width == _editorWidth && image.Height == _editorHeight
                ? CopyPixels(image)
                : ResizePixelsToEditorCanvas(image);

            var layer = CreateEditorLayer(layerPrefix, pixels, isVisible: true, selectLayer: false, isLocked: true);
            layer.OpacityPercent = opacityPercent;
            var baseLayerIndex = EditorLayers.ToList().FindIndex(item => item.Name.StartsWith("Base Layer", StringComparison.OrdinalIgnoreCase));
            if (baseLayerIndex > 0)
            {
                EditorLayers.Move(EditorLayers.IndexOf(layer), baseLayerIndex - 1);
            }

            RefreshEditorComposite();
            RefreshEditorPixels();
            RefreshEditorComparisonState();
            successMessage = $"Added {layer.Name} from {Path.GetFileName(sourcePath)}.";
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Unable to add {layerPrefix.ToLowerInvariant()} layer: {ex.Message}";
            return false;
        }
    }

    [RelayCommand]
    private void LockAllButActiveEditorLayer()
    {
        var activeLayer = GetActiveLayerItem();
        if (activeLayer is null)
        {
            EditorStatusMessage = "No active layer to protect.";
            return;
        }

        foreach (var layer in EditorLayers)
        {
            layer.IsLocked = layer.LayerId != activeLayer.LayerId;
        }

        UpdateLayerSelection();
        OnPropertyChanged(nameof(EditorLayerSummary));
        OnPropertyChanged(nameof(ActiveEditorLayerSummary));
        OnPropertyChanged(nameof(EditorLayerWorkflowHint));
        EditorStatusMessage = $"Locked every layer except {activeLayer.Name}.";
        AddActivity("editor", EditorStatusMessage);
    }

    [RelayCommand]
    private void UnlockAllEditorLayers()
    {
        if (EditorLayers.Count == 0)
        {
            EditorStatusMessage = "Load a frame before unlocking layers.";
            return;
        }

        foreach (var layer in EditorLayers)
        {
            layer.IsLocked = false;
        }

        OnPropertyChanged(nameof(EditorLayerSummary));
        OnPropertyChanged(nameof(ActiveEditorLayerSummary));
        OnPropertyChanged(nameof(EditorLayerWorkflowHint));
        EditorStatusMessage = "Unlocked all layers.";
        AddActivity("editor", EditorStatusMessage);
    }

    [RelayCommand]
    private void SoloActiveEditorLayer()
    {
        var activeLayer = GetActiveLayerItem();
        if (activeLayer is null)
        {
            EditorStatusMessage = "No active layer to isolate.";
            return;
        }

        foreach (var layer in EditorLayers)
        {
            layer.IsVisible = layer.LayerId == activeLayer.LayerId;
        }

        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorComparisonState();
        EditorStatusMessage = $"Showing only {activeLayer.Name}.";
        AddActivity("editor", EditorStatusMessage);
    }

    [RelayCommand]
    private void ShowAllEditorLayers()
    {
        if (EditorLayers.Count == 0)
        {
            EditorStatusMessage = "Load a frame before showing layers.";
            return;
        }

        foreach (var layer in EditorLayers)
        {
            layer.IsVisible = true;
        }

        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorComparisonState();
        EditorStatusMessage = "Showing all layers.";
        AddActivity("editor", EditorStatusMessage);
    }

    [RelayCommand]
    private void RemoveActiveEditorLayer()
    {
        var activeLayer = GetActiveLayerItem();
        if (activeLayer is null)
        {
            EditorStatusMessage = "No active layer to remove.";
            return;
        }

        if (EditorLayers.Count <= 1)
        {
            EditorStatusMessage = "At least one layer must remain.";
            return;
        }

        activeLayer.ThumbnailBitmap = null;
        DetachLayerHandlers(activeLayer);
        _editorLayerPixels.Remove(activeLayer.LayerId);
        EditorLayers.Remove(activeLayer);
        _activeEditorLayerId = EditorLayers.Last().LayerId;
        UpdateLayerSelection();
        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorComparisonState();
        OnPropertyChanged(nameof(EditorLayerSummary));
        EditorStatusMessage = $"Removed {activeLayer.Name}.";
    }

    [RelayCommand]
    private void MergeActiveLayerDown()
    {
        var activeLayer = GetActiveLayerItem();
        var activePixels = GetActiveLayerPixels();
        if (activeLayer is null || activePixels is null)
        {
            EditorStatusMessage = "No active layer is available to merge.";
            return;
        }

        var activeIndex = EditorLayers.IndexOf(activeLayer);
        if (activeIndex < 0 || activeIndex >= EditorLayers.Count - 1)
        {
            EditorStatusMessage = "Choose a layer above another layer to merge down.";
            return;
        }

        var targetLayer = EditorLayers[activeIndex + 1];
        if (!_editorLayerPixels.TryGetValue(targetLayer.LayerId, out var targetPixels))
        {
            EditorStatusMessage = "Could not find the lower layer pixels.";
            return;
        }

        if (targetLayer.IsLocked)
        {
            EditorStatusMessage = $"{targetLayer.Name} is locked. Unlock it before merging.";
            return;
        }

        CaptureUndoState();
        var mergedPixels = (Rgba32[])targetPixels.Clone();
        var effectiveTopOpacity = activeLayer.IsVisible ? activeLayer.OpacityPercent : 0;
        for (var index = 0; index < mergedPixels.Length; index++)
        {
            mergedPixels[index] = AlphaBlend(mergedPixels[index], ApplyLayerOpacity(activePixels[index], effectiveTopOpacity));
        }

        _editorLayerPixels[targetLayer.LayerId] = mergedPixels;
        activeLayer.ThumbnailBitmap = null;
        DetachLayerHandlers(activeLayer);
        _editorLayerPixels.Remove(activeLayer.LayerId);
        EditorLayers.Remove(activeLayer);
        _activeEditorLayerId = targetLayer.LayerId;
        UpdateLayerSelection();
        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorPalette();
        RefreshEditorComparisonState();
        OnPropertyChanged(nameof(EditorLayerSummary));
        IsEditorDirty = true;
        EditorStatusMessage = $"Merged {activeLayer.Name} into {targetLayer.Name}.";
    }

    [RelayCommand]
    private void FlattenVisibleLayers()
    {
        if (_editorPixels.Length == 0 || EditorLayers.Count == 0)
        {
            EditorStatusMessage = "Load a frame before flattening layers.";
            return;
        }

        var visibleLayers = EditorLayers.Where(layer => layer.IsVisible).ToList();
        if (visibleLayers.Count == 0)
        {
            EditorStatusMessage = "There are no visible layers to flatten.";
            return;
        }

        CaptureUndoState();
        foreach (var layer in visibleLayers)
        {
            layer.ThumbnailBitmap = null;
            DetachLayerHandlers(layer);
            _editorLayerPixels.Remove(layer.LayerId);
            EditorLayers.Remove(layer);
        }

        var flattenedLayer = CreateEditorLayer("Flattened", (Rgba32[])_editorPixels.Clone(), isVisible: true, selectLayer: true, isLocked: false);
        EditorLayers.Move(EditorLayers.IndexOf(flattenedLayer), 0);
        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorPalette();
        RefreshEditorComparisonState();
        OnPropertyChanged(nameof(EditorLayerSummary));
        IsEditorDirty = true;
        EditorStatusMessage = $"Flattened {visibleLayers.Count} visible layers into {flattenedLayer.Name}.";
    }

    [RelayCommand]
    private void FlattenVisibleCopyToNewLayer()
    {
        if (_editorPixels.Length == 0 || EditorLayers.Count == 0)
        {
            EditorStatusMessage = "Load a frame before creating a flattened copy.";
            return;
        }

        var visibleLayers = EditorLayers.Count(layer => layer.IsVisible);
        if (visibleLayers == 0)
        {
            EditorStatusMessage = "There are no visible layers to flatten into a copy.";
            return;
        }

        CaptureUndoState();
        var layer = CreateEditorLayer("Flattened Copy", (Rgba32[])_editorPixels.Clone(), isVisible: true, selectLayer: true, isLocked: false);
        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorPalette();
        RefreshEditorComparisonState();
        OnPropertyChanged(nameof(EditorLayerSummary));
        IsEditorDirty = true;
        EditorStatusMessage = $"Created {layer.Name} from {visibleLayers} visible layers without collapsing the stack.";
        AddActivity("editor", EditorStatusMessage);
    }

    [RelayCommand]
    private void MergeActiveEditorLayerUp()
    {
        var activeLayer = GetActiveLayerItem();
        if (activeLayer is null)
        {
            EditorStatusMessage = "No active layer to merge.";
            return;
        }

        var currentIndex = EditorLayers.IndexOf(activeLayer);
        if (currentIndex <= 0)
        {
            EditorStatusMessage = $"{activeLayer.Name} is already at the top of the stack.";
            return;
        }

        var targetLayer = EditorLayers[currentIndex - 1];
        if (!_editorLayerPixels.TryGetValue(activeLayer.LayerId, out var activePixels) ||
            !_editorLayerPixels.TryGetValue(targetLayer.LayerId, out var targetPixels))
        {
            EditorStatusMessage = "One of the layers could not be read for merge.";
            return;
        }

        CaptureUndoState();
        var merged = new Rgba32[Math.Max(activePixels.Length, targetPixels.Length)];
        for (var index = 0; index < merged.Length; index++)
        {
            var bottom = index < activePixels.Length ? ApplyLayerOpacity(activePixels[index], activeLayer.OpacityPercent) : new Rgba32(0, 0, 0, 0);
            var top = index < targetPixels.Length ? ApplyLayerOpacity(targetPixels[index], targetLayer.OpacityPercent) : new Rgba32(0, 0, 0, 0);
            merged[index] = AlphaBlend(bottom, top);
        }

        _editorLayerPixels[targetLayer.LayerId] = merged;
        targetLayer.OpacityPercent = 100;
        targetLayer.IsVisible = targetLayer.IsVisible || activeLayer.IsVisible;
        activeLayer.ThumbnailBitmap = null;
        DetachLayerHandlers(activeLayer);
        _editorLayerPixels.Remove(activeLayer.LayerId);
        EditorLayers.Remove(activeLayer);
        SelectEditorLayer(targetLayer);
        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorPalette();
        RefreshEditorComparisonState();
        RefreshEditorLayerStats();
        IsEditorDirty = true;
        EditorStatusMessage = $"Merged {activeLayer.Name} up into {targetLayer.Name}.";
        AddActivity("editor", EditorStatusMessage);
    }

    [RelayCommand]
    private void SaveCurrentColorToPalette()
    {
        if (string.IsNullOrWhiteSpace(SelectedEditorColorHex))
        {
            EditorStatusMessage = "Choose a color before saving a swatch.";
            return;
        }

        if (SavedEditorPalette.Any(item => item.HexColor.Equals(SelectedEditorColorHex, StringComparison.OrdinalIgnoreCase)))
        {
            EditorStatusMessage = $"{SelectedEditorColorHex} is already in the saved palette.";
            return;
        }

        var parsedColor = TryParseHexColor(SelectedEditorColorHex, out var rgba) ? rgba : new Rgba32(255, 255, 255, 255);
        SavedEditorPalette.Insert(0, new EditorPaletteColorItemViewModel(SelectedEditorColorHex, CreateBrush(parsedColor), true));
        while (SavedEditorPalette.Count > 24)
        {
            SavedEditorPalette.RemoveAt(SavedEditorPalette.Count - 1);
        }

        UpdatePaletteSelection();
        OnPropertyChanged(nameof(SavedPaletteSummary));
        EditorStatusMessage = $"Saved {SelectedEditorColorHex} to the custom palette.";
    }

    [RelayCommand]
    private void RemoveCurrentColorFromPalette()
    {
        var removed = false;
        for (var index = SavedEditorPalette.Count - 1; index >= 0; index--)
        {
            if (!SavedEditorPalette[index].HexColor.Equals(SelectedEditorColorHex, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            SavedEditorPalette.RemoveAt(index);
            removed = true;
        }

        if (!removed)
        {
            EditorStatusMessage = $"{SelectedEditorColorHex} is not in the saved palette.";
            return;
        }

        OnPropertyChanged(nameof(SavedPaletteSummary));
        EditorStatusMessage = $"Removed {SelectedEditorColorHex} from the custom palette.";
    }

    [RelayCommand]
    private void ClearSavedPalette()
    {
        SavedEditorPalette.Clear();
        OnPropertyChanged(nameof(SavedPaletteSummary));
        EditorStatusMessage = "Cleared the saved palette.";
    }

    [RelayCommand]
    private void ExtractCurrentFramePalette()
    {
        var palette = ExtractPaletteFromCurrentContext();
        if (palette.Count == 0)
        {
            ProjectPaletteMessage = "No visible colors were found in the current editor/frame context.";
            return;
        }

        ReplaceSavedPalette(palette);
        ProjectPaletteMessage = $"Loaded {palette.Count} colors into saved swatches from the current frame context.";
        EditorStatusMessage = ProjectPaletteMessage;
        AddActivity("editor", ProjectPaletteMessage);
    }

    [RelayCommand]
    private void SaveSavedSwatchesAsProjectPalette()
    {
        SaveCurrentSwatchesAsPalette("project");
    }

    [RelayCommand]
    private void SaveSavedSwatchesAsSpeciesPalette()
    {
        if (SelectedBaseVariant is null)
        {
            ProjectPaletteMessage = "Select a sprite row before saving a species palette.";
            return;
        }

        SaveCurrentSwatchesAsPalette("species");
    }

    private void SaveCurrentSwatchesAsPalette(string scopeKind)
    {
        var colors = SavedEditorPalette
            .Select(item => item.HexColor)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (colors.Count == 0)
        {
            ProjectPaletteMessage = "Save or extract a few swatches first.";
            return;
        }

        var scopeKey = BuildProjectPaletteScopeKey(scopeKind);
        var paletteName = BuildProjectPaletteName(scopeKind);
        var existing = ProjectPalettes.FirstOrDefault(item =>
            item.Name.Equals(paletteName, StringComparison.OrdinalIgnoreCase) &&
            item.ScopeKind.Equals(scopeKind, StringComparison.OrdinalIgnoreCase) &&
            item.ScopeKey.Equals(scopeKey, StringComparison.OrdinalIgnoreCase));
        var updated = new ProjectPaletteItemViewModel(
            existing?.PaletteId ?? Guid.NewGuid().ToString("N"),
            paletteName,
            scopeKind,
            scopeKey,
            colors,
            DateTimeOffset.UtcNow);

        if (existing is not null)
        {
            ProjectPalettes[ProjectPalettes.IndexOf(existing)] = updated;
        }
        else
        {
            ProjectPalettes.Insert(0, updated);
        }

        SelectedProjectPaletteItem = updated;
        PersistProjectPalettes();
        OnPropertyChanged(nameof(ProjectPaletteSummary));
        OnPropertyChanged(nameof(SelectedProjectPaletteSummary));
        ProjectPaletteMessage = $"Saved {updated.ScopeLabel.ToLowerInvariant()} '{paletteName}'.";
        AddActivity("editor", ProjectPaletteMessage);
    }

    [RelayCommand]
    private void ApplySelectedProjectPalette()
    {
        if (SelectedProjectPaletteItem is null)
        {
            ProjectPaletteMessage = "Select a project palette first.";
            return;
        }

        ReplaceSavedPalette(SelectedProjectPaletteItem.Colors);
        ProjectPaletteMessage = $"Loaded project palette '{SelectedProjectPaletteItem.Name}' into saved swatches.";
        EditorStatusMessage = ProjectPaletteMessage;
        AddActivity("editor", ProjectPaletteMessage);
    }

    [RelayCommand]
    private void DeleteSelectedProjectPalette()
    {
        if (SelectedProjectPaletteItem is null)
        {
            ProjectPaletteMessage = "Select a project palette first.";
            return;
        }

        var name = SelectedProjectPaletteItem.Name;
        ProjectPalettes.Remove(SelectedProjectPaletteItem);
        SelectedProjectPaletteItem = ProjectPalettes.FirstOrDefault();
        PersistProjectPalettes();
        OnPropertyChanged(nameof(ProjectPaletteSummary));
        OnPropertyChanged(nameof(SelectedProjectPaletteSummary));
        ProjectPaletteMessage = $"Deleted project palette '{name}'.";
        AddActivity("editor", ProjectPaletteMessage);
    }

    [RelayCommand]
    private void EditPixel(EditorPixelItemViewModel? pixel)
    {
        ApplyEditorTool(pixel);
    }

    public void SelectEditorToolShortcut(string tool)
    {
        if (!EditorToolOptions.Contains(tool) || SelectedEditorTool.Equals(tool, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedEditorTool = tool;
    }

    public void AdjustEditorZoomShortcut(int delta)
    {
        if (EditorZoomOptions.Count == 0)
        {
            return;
        }

        var currentIndex = EditorZoomOptions.IndexOf(SelectedEditorZoom);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = Math.Clamp(currentIndex + delta, 0, EditorZoomOptions.Count - 1);
        if (nextIndex != currentIndex)
        {
            SelectedEditorZoom = EditorZoomOptions[nextIndex];
        }
    }

    public void ToggleEditorMirrorHorizontalShortcut() => EditorMirrorHorizontal = !EditorMirrorHorizontal;

    public void ToggleEditorMirrorVerticalShortcut() => EditorMirrorVertical = !EditorMirrorVertical;

    public void BeginEditorSelection(EditorPixelItemViewModel? pixel)
    {
        if (pixel is null || pixel.Index < 0 || pixel.Index >= _editorPixels.Length)
        {
            return;
        }

        if (SelectedEditorTool.Equals("lasso", StringComparison.OrdinalIgnoreCase))
        {
            _lassoSelectionPoints.Clear();
            _lassoSelectionPoints.Add((pixel.X, pixel.Y));
            SetSelectedEditorIndices([pixel.Index]);
            EditorStatusMessage = $"Started lasso at {pixel.X},{pixel.Y}.";
            return;
        }

        _selectionAnchorIndex = pixel.Index;
        UpdateEditorSelection(pixel);
    }

    public void UpdateEditorSelection(EditorPixelItemViewModel? pixel)
    {
        if (pixel is null || pixel.Index < 0 || pixel.Index >= _editorPixels.Length)
        {
            return;
        }

        if (SelectedEditorTool.Equals("lasso", StringComparison.OrdinalIgnoreCase))
        {
            if (_lassoSelectionPoints.Count == 0 || _lassoSelectionPoints[^1] != (pixel.X, pixel.Y))
            {
                _lassoSelectionPoints.Add((pixel.X, pixel.Y));
                SetSelectedEditorIndices(BuildLassoSelectionIndices());
                EditorStatusMessage = $"Tracing lasso with {_lassoSelectionPoints.Count} points.";
            }

            return;
        }

        if (_selectionAnchorIndex is null)
        {
            return;
        }

        SetEditorSelectionRectangle(_selectionAnchorIndex.Value, pixel.Index);
    }

    public void EndEditorSelection()
    {
        _selectionAnchorIndex = null;
        if (SelectedEditorTool.Equals("lasso", StringComparison.OrdinalIgnoreCase))
        {
            var count = _selectedEditorIndices.Count;
            EditorStatusMessage = count == 0
                ? "Lasso selection did not capture any pixels."
                : $"Lasso selected {count} pixels.";
            _lassoSelectionPoints.Clear();
        }
    }

    [RelayCommand]
    private void SelectAllEditorPixels()
    {
        if (_editorPixels.Length == 0)
        {
            return;
        }

        SetSelectedEditorIndices(Enumerable.Range(0, _editorPixels.Length));
        EditorStatusMessage = $"Selected all {_selectedEditorIndices.Count} pixels.";
    }

    [RelayCommand]
    private void ClearEditorSelection()
    {
        if (_selectedEditorIndices.Count == 0)
        {
            EditorStatusMessage = "No selection to clear.";
            return;
        }

        SetSelectedEditorIndices([]);
        EditorStatusMessage = "Cleared the current selection.";
    }

    [RelayCommand]
    private void DeleteEditorSelection()
    {
        if (_selectedEditorIndices.Count == 0)
        {
            EditorStatusMessage = "Select pixels before deleting them.";
            return;
        }

        CaptureUndoState();
        foreach (var index in _selectedEditorIndices.ToArray())
        {
            ApplyColorToPixel(index, new Rgba32(0, 0, 0, 0));
        }

        EditorStatusMessage = $"Deleted {_selectedEditorIndices.Count} selected pixels.";
    }

    [RelayCommand]
    private void CopyEditorSelection()
    {
        if (!TryBuildClipboardFromSelection(out var clipboard))
        {
            EditorStatusMessage = "Select pixels before copying them.";
            return;
        }

        _editorClipboard = clipboard;
        OnPropertyChanged(nameof(EditorSelectionSummary));
        EditorStatusMessage = $"Copied {_selectedEditorIndices.Count} selected pixels.";
    }

    [RelayCommand]
    private void PasteEditorSelection()
    {
        if (_editorClipboard is null)
        {
            EditorStatusMessage = "Copy a selection before pasting it.";
            return;
        }

        var targetOriginX = _editorClipboard.OriginX;
        var targetOriginY = _editorClipboard.OriginY;

        if (_selectedEditorIndices.Count > 0 && TryGetSelectionBounds(out var minX, out var minY, out _, out _))
        {
            targetOriginX = minX;
            targetOriginY = minY;
        }

        CaptureUndoState();
        var pastedIndices = new List<int>(_editorClipboard.Pixels.Count);
        foreach (var pixel in _editorClipboard.Pixels)
        {
            var targetX = targetOriginX + pixel.OffsetX;
            var targetY = targetOriginY + pixel.OffsetY;
            if (targetX < 0 || targetY < 0 || targetX >= _editorWidth || targetY >= _editorHeight)
            {
                continue;
            }

            var targetIndex = (targetY * _editorWidth) + targetX;
            ApplyColorToPixel(targetIndex, pixel.Color);
            pastedIndices.Add(targetIndex);
        }

        SetSelectedEditorIndices(pastedIndices);
        EditorStatusMessage = pastedIndices.Count == 0
            ? "Clipboard content was outside the canvas."
            : $"Pasted {pastedIndices.Count} pixels into the current frame.";
    }

    [RelayCommand]
    private void MoveSelectionLeft() => MoveSelectedPixels(-1, 0);

    [RelayCommand]
    private void MoveSelectionRight() => MoveSelectedPixels(1, 0);

    [RelayCommand]
    private void MoveSelectionUp() => MoveSelectedPixels(0, -1);

    [RelayCommand]
    private void MoveSelectionDown() => MoveSelectedPixels(0, 1);

    [RelayCommand]
    private void FlipSelectionHorizontal() => FlipSelectedPixels(horizontal: true);

    [RelayCommand]
    private void FlipSelectionVertical() => FlipSelectedPixels(horizontal: false);

    [RelayCommand]
    private void RotateSelectionLeft() => RotateSelectedPixels(clockwise: false);

    [RelayCommand]
    private void RotateSelectionRight() => RotateSelectedPixels(clockwise: true);

    [RelayCommand]
    private void ScaleSelectionUp() => ScaleSelectedPixels(2.0, "Scaled selection up 2x.");

    [RelayCommand]
    private void ScaleSelectionDown() => ScaleSelectedPixels(0.5, "Scaled selection down 50%.");

    [RelayCommand]
    private void ResizeSelectionFromHandle(string? handle)
    {
        if (!TryGetSelectionBounds(out var minX, out var minY, out var maxX, out var maxY))
        {
            EditorStatusMessage = "Select pixels before using the on-canvas handles.";
            return;
        }

        var deltaLeft = 0;
        var deltaTop = 0;
        var deltaRight = 0;
        var deltaBottom = 0;
        var label = handle ?? string.Empty;

        switch ((handle ?? string.Empty).ToUpperInvariant())
        {
            case "W":
                deltaLeft = -1;
                break;
            case "E":
                deltaRight = 1;
                break;
            case "N":
                deltaTop = -1;
                break;
            case "S":
                deltaBottom = 1;
                break;
            case "NW":
                deltaLeft = -1;
                deltaTop = -1;
                break;
            case "NE":
                deltaRight = 1;
                deltaTop = -1;
                break;
            case "SW":
                deltaLeft = -1;
                deltaBottom = 1;
                break;
            case "SE":
                deltaRight = 1;
                deltaBottom = 1;
                break;
            default:
                EditorStatusMessage = "Unknown transform handle.";
                return;
        }

        ResizeSelectedPixelsToBounds(
            Math.Max(0, minX + deltaLeft),
            Math.Max(0, minY + deltaTop),
            Math.Min(_editorWidth - 1, maxX + deltaRight),
            Math.Min(_editorHeight - 1, maxY + deltaBottom),
            $"Expanded selection from handle {label}.");
    }

    public void ApplyEditorTool(EditorPixelItemViewModel? pixel)
    {
        if (pixel is null || _editorPixels.Length == 0 || pixel.Index < 0 || pixel.Index >= _editorPixels.Length)
        {
            return;
        }

        if (SelectedEditorTool is not "select" and not "dropper" &&
            !CanEditActiveLayer())
        {
            return;
        }

        switch (SelectedEditorTool)
        {
            case "select":
                return;
            case "move":
                return;
            case "line":
                if (!TryParseHexColor(SelectedEditorColorHex, out var lineColor))
                {
                    EditorStatusMessage = $"Color '{SelectedEditorColorHex}' is invalid.";
                    return;
                }

                _shapeAnchorIndex ??= pixel.Index;
                PreviewShape(pixel.Index, lineColor, "line");
                EditorStatusMessage = $"Drawing line from {_shapeAnchorIndex.Value % _editorWidth},{_shapeAnchorIndex.Value / _editorWidth} to {pixel.X},{pixel.Y}.";
                return;
            case "rectangle":
                if (!TryParseHexColor(SelectedEditorColorHex, out var rectangleColor))
                {
                    EditorStatusMessage = $"Color '{SelectedEditorColorHex}' is invalid.";
                    return;
                }

                _shapeAnchorIndex ??= pixel.Index;
                PreviewShape(pixel.Index, rectangleColor, "rectangle");
                EditorStatusMessage = $"Drawing rectangle from {_shapeAnchorIndex.Value % _editorWidth},{_shapeAnchorIndex.Value / _editorWidth} to {pixel.X},{pixel.Y}.";
                return;
            case "ellipse":
                if (!TryParseHexColor(SelectedEditorColorHex, out var ellipseColor))
                {
                    EditorStatusMessage = $"Color '{SelectedEditorColorHex}' is invalid.";
                    return;
                }

                _shapeAnchorIndex ??= pixel.Index;
                PreviewShape(pixel.Index, ellipseColor, "ellipse");
                EditorStatusMessage = $"Drawing ellipse from {_shapeAnchorIndex.Value % _editorWidth},{_shapeAnchorIndex.Value / _editorWidth} to {pixel.X},{pixel.Y}.";
                return;
            case "dropper":
                SelectedEditorColorHex = pixel.HexColor;
                UpdatePaletteSelection();
                EditorStatusMessage = $"Picked {pixel.HexColor} from {pixel.Label}.";
                return;
            case "erase":
                EnsureStrokeSnapshot();
                ApplyBrushAt(pixel.Index, new Rgba32(0, 0, 0, 0));
                EditorStatusMessage = $"Erased pixel {pixel.Label}.";
                return;
            case "fill":
                if (!TryParseHexColor(SelectedEditorColorHex, out var fillColor))
                {
                    EditorStatusMessage = $"Color '{SelectedEditorColorHex}' is invalid.";
                    return;
                }

                CaptureUndoState();
                FloodFillMirrored(pixel.Index, fillColor);
                EditorStatusMessage = $"Filled region from {pixel.Label} with {SelectedEditorColorHex}.";
                return;
            default:
                if (!TryParseHexColor(SelectedEditorColorHex, out var selectedColor))
                {
                    EditorStatusMessage = $"Color '{SelectedEditorColorHex}' is invalid.";
                    return;
                }

                EnsureStrokeSnapshot();
                ApplyBrushAt(pixel.Index, selectedColor);
                EditorStatusMessage = $"Painted pixel {pixel.Label} with {SelectedEditorColorHex}.";
                return;
        }
    }

    public void BeginEditorStroke()
    {
        _strokeSnapshotCaptured = false;
        _shapeAnchorIndex = null;
        _shapePreviewBasePixels = null;
        _moveAnchorIndex = null;
        _movePreviewBasePixels = null;
        _movePreviewPixels = null;

        if (SelectedEditorTool is "brush" or "erase")
        {
            EnsureStrokeSnapshot();
        }
        else if (SelectedEditorTool is "line" or "rectangle" or "ellipse")
        {
            CaptureUndoState();
            _strokeSnapshotCaptured = true;
            _shapePreviewBasePixels = (Rgba32[])(GetActiveLayerPixels()?.Clone() ?? _editorPixels.Clone());
        }
    }

    public bool BeginEditorMove(EditorPixelItemViewModel? pixel)
    {
        if (!CanEditActiveLayer())
        {
            return false;
        }

        if (pixel is null || _selectedEditorIndices.Count == 0)
        {
            EditorStatusMessage = "Select pixels before moving them.";
            return false;
        }

        if (!_selectedEditorIndices.Contains(pixel.Index))
        {
            EditorStatusMessage = "Start dragging from inside the current selection.";
            return false;
        }

        var activePixels = GetActiveLayerPixels();
        if (activePixels is null)
        {
            EditorStatusMessage = "No active layer is available for moving pixels.";
            return false;
        }

        _movePreviewPixels = GetSelectedPixelsSnapshot();
        _movePreviewBasePixels = (Rgba32[])activePixels.Clone();
        foreach (var selectedPixel in _movePreviewPixels)
        {
            _movePreviewBasePixels[(selectedPixel.Y * _editorWidth) + selectedPixel.X] = new Rgba32(0, 0, 0, 0);
        }

        _moveAnchorIndex = pixel.Index;
        CaptureUndoState();
        _strokeSnapshotCaptured = true;
        EditorStatusMessage = "Dragging selected pixels.";
        return true;
    }

    public void UpdateEditorMove(EditorPixelItemViewModel? pixel)
    {
        if (pixel is null ||
            _moveAnchorIndex is null ||
            _movePreviewBasePixels is null ||
            _movePreviewPixels is null ||
            _editorWidth <= 0)
        {
            return;
        }

        var anchorX = _moveAnchorIndex.Value % _editorWidth;
        var anchorY = _moveAnchorIndex.Value / _editorWidth;
        var deltaX = pixel.X - anchorX;
        var deltaY = pixel.Y - anchorY;
        PreviewMovedSelection(deltaX, deltaY);
    }

    public void EndEditorStroke()
    {
        _strokeSnapshotCaptured = false;
        _shapeAnchorIndex = null;
        _shapePreviewBasePixels = null;
        _moveAnchorIndex = null;
        _movePreviewBasePixels = null;
        _movePreviewPixels = null;
    }

    public void NotifyEditorStrokeCompleted()
    {
        if (!AutoCaptureOnStroke || !IsEditorFrameLoaded || !IsEditorDirty)
        {
            return;
        }

        CaptureEditorReviewBundleInternal("stroke");
    }

    private void SetEditorSelectionRectangle(int anchorIndex, int currentIndex)
    {
        if (_editorWidth <= 0 || _editorHeight <= 0)
        {
            return;
        }

        var anchorX = anchorIndex % _editorWidth;
        var anchorY = anchorIndex / _editorWidth;
        var currentX = currentIndex % _editorWidth;
        var currentY = currentIndex / _editorWidth;
        var minX = Math.Min(anchorX, currentX);
        var maxX = Math.Max(anchorX, currentX);
        var minY = Math.Min(anchorY, currentY);
        var maxY = Math.Max(anchorY, currentY);

        var indices = new List<int>((maxX - minX + 1) * (maxY - minY + 1));
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                indices.Add((y * _editorWidth) + x);
            }
        }

        SetSelectedEditorIndices(indices);
        EditorStatusMessage = $"Selected rectangle {minX},{minY} to {maxX},{maxY}.";
    }

    private IReadOnlyList<int> BuildLassoSelectionIndices()
    {
        if (_lassoSelectionPoints.Count == 0 || _editorWidth <= 0 || _editorHeight <= 0)
        {
            return [];
        }

        if (_lassoSelectionPoints.Count < 3)
        {
            return _lassoSelectionPoints
                .Select(point => (point.Y * _editorWidth) + point.X)
                .Distinct()
                .ToList();
        }

        var minX = _lassoSelectionPoints.Min(point => point.X);
        var maxX = _lassoSelectionPoints.Max(point => point.X);
        var minY = _lassoSelectionPoints.Min(point => point.Y);
        var maxY = _lassoSelectionPoints.Max(point => point.Y);
        var indices = new List<int>();

        for (var y = Math.Max(0, minY); y <= Math.Min(_editorHeight - 1, maxY); y++)
        {
            for (var x = Math.Max(0, minX); x <= Math.Min(_editorWidth - 1, maxX); x++)
            {
                if (IsPointInsidePolygon(x + 0.5, y + 0.5, _lassoSelectionPoints))
                {
                    indices.Add((y * _editorWidth) + x);
                }
            }
        }

        if (indices.Count == 0)
        {
            indices.AddRange(_lassoSelectionPoints.Select(point => (point.Y * _editorWidth) + point.X));
        }

        return indices;
    }

    private static bool IsPointInsidePolygon(double x, double y, IReadOnlyList<(int X, int Y)> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var xi = polygon[i].X + 0.5;
            var yi = polygon[i].Y + 0.5;
            var xj = polygon[j].X + 0.5;
            var yj = polygon[j].Y + 0.5;

            var intersects = ((yi > y) != (yj > y)) &&
                             (x < ((xj - xi) * (y - yi) / ((yj - yi) == 0 ? double.Epsilon : (yj - yi)) + xi));
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private void SetSelectedEditorIndices(IEnumerable<int> indices)
    {
        _selectedEditorIndices.Clear();
        foreach (var index in indices.Distinct())
        {
            if (index >= 0 && index < _editorPixels.Length)
            {
                _selectedEditorIndices.Add(index);
            }
        }

        for (var index = 0; index < EditorPixels.Count; index++)
        {
            EditorPixels[index].IsSelected = _selectedEditorIndices.Contains(index);
        }

        OnPropertyChanged(nameof(EditorSelectionSummary));
        OnPropertyChanged(nameof(EditorSelectionBoundsSummary));
        OnPropertyChanged(nameof(EditorSelectionTransformHint));
        OnPropertyChanged(nameof(EditorLayerWorkflowHint));
        NotifyEditorSelectionOverlayChanged();
    }

    private bool TryGetSelectionBounds(out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = minY = maxX = maxY = 0;
        if (_selectedEditorIndices.Count == 0 || _editorWidth <= 0)
        {
            return false;
        }

        minX = int.MaxValue;
        minY = int.MaxValue;
        maxX = int.MinValue;
        maxY = int.MinValue;
        foreach (var index in _selectedEditorIndices)
        {
            var x = index % _editorWidth;
            var y = index / _editorWidth;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        return true;
    }

    private bool TryBuildClipboardFromSelection(out EditorClipboardState clipboard)
    {
        clipboard = new EditorClipboardState(0, 0, 0, 0, Array.Empty<EditorClipboardPixel>());
        var activePixels = GetActiveLayerPixels();
        if (activePixels is null ||
            !TryGetSelectionBounds(out var minX, out var minY, out var maxX, out var maxY))
        {
            return false;
        }

        var pixels = new List<EditorClipboardPixel>();
        foreach (var index in _selectedEditorIndices.OrderBy(index => index))
        {
            var x = index % _editorWidth;
            var y = index / _editorWidth;
            pixels.Add(new EditorClipboardPixel(x - minX, y - minY, activePixels[index]));
        }

        clipboard = new EditorClipboardState(maxX - minX + 1, maxY - minY + 1, minX, minY, pixels);
        return true;
    }

    private List<SelectedPixelState> GetSelectedPixelsSnapshot()
    {
        var activePixels = GetActiveLayerPixels();
        if (activePixels is null)
        {
            return [];
        }

        return _selectedEditorIndices
            .OrderBy(index => index)
            .Select(index => new SelectedPixelState(index % _editorWidth, index / _editorWidth, activePixels[index]))
            .ToList();
    }

    private void MoveSelectedPixels(int deltaX, int deltaY)
    {
        if (_selectedEditorIndices.Count == 0)
        {
            EditorStatusMessage = "Select pixels before moving them.";
            return;
        }

        var selectedPixels = GetSelectedPixelsSnapshot();

        CaptureUndoState();
        var movedIndices = ApplySelectedPixels(selectedPixels, pixel => (pixel.X + deltaX, pixel.Y + deltaY));
        EditorStatusMessage = movedIndices.Count == 0
            ? "Selection moved completely off the canvas."
            : $"Moved selection by ({deltaX},{deltaY}).";
    }

    private void FlipSelectedPixels(bool horizontal)
    {
        if (!TryGetSelectionBounds(out var minX, out var minY, out var maxX, out var maxY))
        {
            EditorStatusMessage = "Select pixels before flipping them.";
            return;
        }

        var selectedPixels = GetSelectedPixelsSnapshot();

        CaptureUndoState();
        ApplySelectedPixels(selectedPixels, pixel =>
        {
            var targetX = horizontal ? maxX - (pixel.X - minX) : pixel.X;
            var targetY = horizontal ? pixel.Y : maxY - (pixel.Y - minY);
            return (targetX, targetY);
        });
        EditorStatusMessage = horizontal
            ? "Flipped selection horizontally."
            : "Flipped selection vertically.";
    }

    private void RotateSelectedPixels(bool clockwise)
    {
        if (!TryGetSelectionBounds(out var minX, out var minY, out var maxX, out var maxY))
        {
            EditorStatusMessage = "Select pixels before rotating them.";
            return;
        }

        var selectedPixels = GetSelectedPixelsSnapshot();
        var width = maxX - minX + 1;
        var height = maxY - minY + 1;

        CaptureUndoState();
        ApplySelectedPixels(selectedPixels, pixel =>
        {
            var localX = pixel.X - minX;
            var localY = pixel.Y - minY;
            var targetLocalX = clockwise ? height - 1 - localY : localY;
            var targetLocalY = clockwise ? localX : width - 1 - localX;
            return (minX + targetLocalX, minY + targetLocalY);
        });

        EditorStatusMessage = clockwise
            ? "Rotated selection 90 degrees clockwise."
            : "Rotated selection 90 degrees counterclockwise.";
    }

    private void ScaleSelectedPixels(double scale, string successMessage)
    {
        if (!TryGetSelectionBounds(out var minX, out var minY, out var maxX, out var maxY))
        {
            EditorStatusMessage = "Select pixels before scaling them.";
            return;
        }

        var selectedPixels = GetSelectedPixelsSnapshot();
        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var scaledWidth = Math.Max(1, (int)Math.Round(width * scale));
        var scaledHeight = Math.Max(1, (int)Math.Round(height * scale));

        CaptureUndoState();
        ApplySelectedPixels(selectedPixels, pixel =>
        {
            var localX = width == 1 ? 0 : pixel.X - minX;
            var localY = height == 1 ? 0 : pixel.Y - minY;
            var normalizedX = width == 1 ? 0.0 : localX / (double)(width - 1);
            var normalizedY = height == 1 ? 0.0 : localY / (double)(height - 1);
            var targetX = minX + (int)Math.Round(normalizedX * Math.Max(0, scaledWidth - 1));
            var targetY = minY + (int)Math.Round(normalizedY * Math.Max(0, scaledHeight - 1));
            return (targetX, targetY);
        });

        EditorStatusMessage = successMessage;
    }

    private void ResizeSelectedPixelsToBounds(int newMinX, int newMinY, int newMaxX, int newMaxY, string successMessage)
    {
        if (!TryGetSelectionBounds(out var minX, out var minY, out var maxX, out var maxY))
        {
            EditorStatusMessage = "Select pixels before resizing them.";
            return;
        }

        if (newMaxX < newMinX || newMaxY < newMinY)
        {
            EditorStatusMessage = "That resize would collapse the selection.";
            return;
        }

        if (newMinX == minX && newMinY == minY && newMaxX == maxX && newMaxY == maxY)
        {
            EditorStatusMessage = "The selection is already at the edge of the canvas.";
            return;
        }

        var selectedPixels = GetSelectedPixelsSnapshot();
        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var newWidth = newMaxX - newMinX + 1;
        var newHeight = newMaxY - newMinY + 1;

        CaptureUndoState();
        ApplySelectedPixels(selectedPixels, pixel =>
        {
            var localX = width == 1 ? 0 : pixel.X - minX;
            var localY = height == 1 ? 0 : pixel.Y - minY;
            var normalizedX = width == 1 ? 0.0 : localX / (double)(width - 1);
            var normalizedY = height == 1 ? 0.0 : localY / (double)(height - 1);
            var targetX = newMinX + (int)Math.Round(normalizedX * Math.Max(0, newWidth - 1));
            var targetY = newMinY + (int)Math.Round(normalizedY * Math.Max(0, newHeight - 1));
            return (targetX, targetY);
        });

        EditorStatusMessage = successMessage;
    }

    private void PreviewMovedSelection(int deltaX, int deltaY)
    {
        if (_movePreviewBasePixels is null || _movePreviewPixels is null)
        {
            return;
        }

        var previewPixels = (Rgba32[])_movePreviewBasePixels.Clone();
        var movedIndices = new List<int>(_movePreviewPixels.Count);
        foreach (var pixel in _movePreviewPixels)
        {
            var targetX = pixel.X + deltaX;
            var targetY = pixel.Y + deltaY;
            if (targetX < 0 || targetY < 0 || targetX >= _editorWidth || targetY >= _editorHeight)
            {
                continue;
            }

            var targetIndex = (targetY * _editorWidth) + targetX;
            previewPixels[targetIndex] = pixel.Color;
            movedIndices.Add(targetIndex);
        }

        _editorLayerPixels[_activeEditorLayerId] = previewPixels;
        RefreshEditorComposite();
        IsEditorDirty = true;
        RefreshEditorPalette();
        RefreshEditorPixels();
        RefreshEditorComparisonState();
        SetSelectedEditorIndices(movedIndices);
        EditorStatusMessage = movedIndices.Count == 0
            ? "Selection moved completely off the canvas."
            : $"Dragging selection by ({deltaX},{deltaY}).";
    }

    private List<int> ApplySelectedPixels(IEnumerable<SelectedPixelState> selectedPixels, Func<SelectedPixelState, (int X, int Y)> transform)
    {
        var pixels = selectedPixels.ToList();
        foreach (var pixel in pixels)
        {
            ApplyColorToPixel((pixel.Y * _editorWidth) + pixel.X, new Rgba32(0, 0, 0, 0));
        }

        var transformedIndices = new List<int>(pixels.Count);
        foreach (var pixel in pixels)
        {
            var (targetX, targetY) = transform(pixel);
            if (targetX < 0 || targetY < 0 || targetX >= _editorWidth || targetY >= _editorHeight)
            {
                continue;
            }

            var targetIndex = (targetY * _editorWidth) + targetX;
            ApplyColorToPixel(targetIndex, pixel.Color);
            transformedIndices.Add(targetIndex);
        }

        SetSelectedEditorIndices(transformedIndices);
        return transformedIndices;
    }

    private void PreviewShape(int currentIndex, Rgba32 color, string shapeKind)
    {
        if (_shapePreviewBasePixels is null)
        {
            _shapePreviewBasePixels = (Rgba32[])(GetActiveLayerPixels()?.Clone() ?? _editorPixels.Clone());
        }

        if (_shapeAnchorIndex is null || _editorWidth <= 0 || _editorHeight <= 0)
        {
            return;
        }

        if (!IsEditorDirty)
        {
            IsPlaybackEnabled = false;
        }

        var previewPixels = (Rgba32[])_shapePreviewBasePixels.Clone();
        var stampIndices = new HashSet<int>();
        var startX = _shapeAnchorIndex.Value % _editorWidth;
        var startY = _shapeAnchorIndex.Value / _editorWidth;
        var endX = currentIndex % _editorWidth;
        var endY = currentIndex / _editorWidth;

        if (shapeKind.Equals("rectangle", StringComparison.OrdinalIgnoreCase))
        {
            if (EditorFillShapes)
            {
                DrawRectangleFilled(stampIndices, startX, startY, endX, endY);
            }
            else
            {
                DrawRectangleOutline(stampIndices, startX, startY, endX, endY);
            }
        }
        else if (shapeKind.Equals("ellipse", StringComparison.OrdinalIgnoreCase))
        {
            if (EditorFillShapes)
            {
                DrawEllipseFilled(stampIndices, startX, startY, endX, endY);
            }
            else
            {
                DrawEllipseOutline(stampIndices, startX, startY, endX, endY);
            }
        }
        else
        {
            DrawLine(stampIndices, startX, startY, endX, endY);
        }

        foreach (var index in stampIndices)
        {
            if (index >= 0 && index < previewPixels.Length)
            {
                previewPixels[index] = color;
            }
        }

        _editorLayerPixels[_activeEditorLayerId] = previewPixels;
        RefreshEditorComposite();
        IsEditorDirty = true;
        RefreshEditorPalette();
        RefreshEditorPixels();
        RefreshEditorComparisonState();
    }

    private void DrawLine(HashSet<int> indices, int startX, int startY, int endX, int endY)
    {
        var x0 = startX;
        var y0 = startY;
        var x1 = endX;
        var y1 = endY;
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            StampBrush(indices, x0, y0);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var doubledError = 2 * error;
            if (doubledError >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (doubledError <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private void DrawRectangleOutline(HashSet<int> indices, int startX, int startY, int endX, int endY)
    {
        var minX = Math.Min(startX, endX);
        var maxX = Math.Max(startX, endX);
        var minY = Math.Min(startY, endY);
        var maxY = Math.Max(startY, endY);

        for (var x = minX; x <= maxX; x++)
        {
            StampBrush(indices, x, minY);
            StampBrush(indices, x, maxY);
        }

        for (var y = minY; y <= maxY; y++)
        {
            StampBrush(indices, minX, y);
            StampBrush(indices, maxX, y);
        }
    }

    private void DrawRectangleFilled(HashSet<int> indices, int startX, int startY, int endX, int endY)
    {
        var minX = Math.Min(startX, endX);
        var maxX = Math.Max(startX, endX);
        var minY = Math.Min(startY, endY);
        var maxY = Math.Max(startY, endY);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                StampBrush(indices, x, y);
            }
        }
    }

    private void DrawEllipseOutline(HashSet<int> indices, int startX, int startY, int endX, int endY)
    {
        DrawEllipse(indices, startX, startY, endX, endY, filled: false);
    }

    private void DrawEllipseFilled(HashSet<int> indices, int startX, int startY, int endX, int endY)
    {
        DrawEllipse(indices, startX, startY, endX, endY, filled: true);
    }

    private void DrawEllipse(HashSet<int> indices, int startX, int startY, int endX, int endY, bool filled)
    {
        var minX = Math.Min(startX, endX);
        var maxX = Math.Max(startX, endX);
        var minY = Math.Min(startY, endY);
        var maxY = Math.Max(startY, endY);

        var radiusX = Math.Max(0.5, (maxX - minX) / 2.0);
        var radiusY = Math.Max(0.5, (maxY - minY) / 2.0);
        var centerX = minX + radiusX;
        var centerY = minY + radiusY;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var normalizedX = (x - centerX) / radiusX;
                var normalizedY = (y - centerY) / radiusY;
                var distance = (normalizedX * normalizedX) + (normalizedY * normalizedY);
                if (filled)
                {
                    if (distance <= 1.0)
                    {
                        StampBrush(indices, x, y);
                    }
                }
                else if (distance <= 1.08 && distance >= 0.72)
                {
                    StampBrush(indices, x, y);
                }
            }
        }
    }

    private void StampBrush(HashSet<int> indices, int centerX, int centerY)
    {
        var halfLow = Math.Max(0, SelectedEditorBrushSize - 1) / 2;
        var halfHigh = SelectedEditorBrushSize / 2;
        for (var y = centerY - halfLow; y <= centerY + halfHigh; y++)
        {
            for (var x = centerX - halfLow; x <= centerX + halfHigh; x++)
            {
                if (x < 0 || y < 0 || x >= _editorWidth || y >= _editorHeight)
                {
                    continue;
                }

                indices.Add((y * _editorWidth) + x);
            }
        }
    }

    [RelayCommand]
    private void SaveEditedFrame()
    {
        if (string.IsNullOrWhiteSpace(_editorSaveFramePath) || _editorPixels.Length == 0)
        {
            EditorStatusMessage = "Load a frame before saving.";
            AddActivity("editor", "Save skipped because no frame is loaded.");
            return;
        }

        try
        {
            if (File.Exists(_editorSaveFramePath))
            {
                CreateFrameHistorySnapshot(_editorSaveFramePath, "before-save");
            }

            using var image = new Image<Rgba32>(_editorWidth, _editorHeight);
            for (var y = 0; y < _editorHeight; y++)
            {
                for (var x = 0; x < _editorWidth; x++)
                {
                    image[x, y] = _editorPixels[(y * _editorWidth) + x];
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_editorSaveFramePath) ?? string.Empty);
            image.Save(_editorSaveFramePath);
            CreateFrameHistorySnapshot(_editorSaveFramePath, "saved");
            _editorBaselinePixels = (Rgba32[])_editorPixels.Clone();
            InitializeEditorLayers(_editorBaselinePixels);
            IsEditorDirty = false;
            _undoHistory.Clear();
            _redoHistory.Clear();
            EditorStatusMessage = $"Saved {_editorSaveFramePath} at {DateTime.Now:h:mm tt}.";
            NotifyEditorHistoryChanged();
            RefreshEditorComparisonState();
            RefreshFrameHistory();
            UpdateViewer();
            NotifyLoopStateChanged();
            PersistWorkspaceState();
            AddActivity("editor", $"Saved {Path.GetFileName(_editorSaveFramePath)}.");
            if (AutoCaptureOnSave)
            {
                CaptureEditorReviewBundleInternal("save");
            }
        }
        catch (Exception ex)
        {
            EditorStatusMessage = $"Unable to save frame: {ex.Message}";
            AddActivity("editor", $"Save failed for {Path.GetFileName(_editorSaveFramePath)}: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CaptureEditorReviewBundle()
    {
        CaptureEditorReviewBundleInternal("manual");
    }

    [RelayCommand]
    private void RevertEditedFrame()
    {
        if (string.IsNullOrWhiteSpace(_editorSaveFramePath))
        {
            EditorStatusMessage = "No frame loaded to revert.";
            AddActivity("editor", "Revert skipped because no frame is loaded.");
            return;
        }

        LoadEditorFrame(_editorSaveFramePath, true);
        RefreshFrameHistory();
        EditorStatusMessage = $"Reloaded {_editorSaveFramePath} from disk.";
        AddActivity("editor", $"Reverted {Path.GetFileName(_editorSaveFramePath)} from disk.");
    }

    [RelayCommand]
    private void RestoreFrameHistoryVersion(FrameHistoryItemViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(_editorSaveFramePath) || !File.Exists(item.FilePath))
        {
            FrameHistoryMessage = "Select a valid frame history entry first.";
            return;
        }

        if (IsEditorDirty)
        {
            FrameHistoryMessage = "Save or revert the active edit before restoring a history version.";
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_editorSaveFramePath) ?? string.Empty);
            File.Copy(item.FilePath, _editorSaveFramePath, true);
            LoadEditorFrame(_editorSaveFramePath, true);
            RefreshFrameHistory();
            FrameHistoryMessage = $"Restored {item.Label} into {Path.GetFileName(_editorSaveFramePath)}.";
            AddActivity("editor", FrameHistoryMessage);
        }
        catch (Exception ex)
        {
            FrameHistoryMessage = $"Unable to restore frame history: {ex.Message}";
            AddActivity("editor", $"Frame history restore failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenFrameHistoryFolder()
    {
        if (string.IsNullOrWhiteSpace(_frameHistoryDirectory))
        {
            FrameHistoryMessage = "No frame history folder is configured for this project.";
            return;
        }

        try
        {
            Directory.CreateDirectory(_frameHistoryDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _frameHistoryDirectory,
                UseShellExecute = true,
            });
            FrameHistoryMessage = $"Opened frame history folder at {_frameHistoryDirectory}.";
            AddActivity("editor", FrameHistoryMessage);
        }
        catch (Exception ex)
        {
            FrameHistoryMessage = $"Unable to open frame history folder: {ex.Message}";
            AddActivity("editor", $"Could not open frame history folder: {ex.Message}");
        }
    }

    private void CaptureEditorReviewBundleInternal(string reason)
    {
        if (_editorPixels.Length == 0)
        {
            EditorStatusMessage = "Load a frame before capturing editor review files.";
            AddActivity("editor", "Capture skipped because no frame is loaded.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_editorCaptureDirectory))
        {
            EditorStatusMessage = "No editor capture folder is configured for this project.";
            AddActivity("editor", "Capture skipped because no editor capture folder is configured.");
            return;
        }

        try
        {
            Directory.CreateDirectory(_editorCaptureDirectory);

            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            var rowSlug = SelectedBaseVariant is null
                ? "unscoped-row"
                : $"{SelectedBaseVariant.Species}-{SelectedBaseVariant.Age}-{SelectedBaseVariant.Gender}".ToLowerInvariant();
            var bundleDirectory = Path.Combine(_editorCaptureDirectory, $"{stamp}-{rowSlug}-{reason}");
            Directory.CreateDirectory(bundleDirectory);

            var liveCanvasPath = Path.Combine(bundleDirectory, "live-editor-canvas.png");
            SaveEditorPixelsToPath(liveCanvasPath);

            var authoredReferencePath = CopyReferenceIntoBundle(ViewerFramePath, bundleDirectory, "authored-reference");
            var runtimeReferencePath = CopyReferenceIntoBundle(RuntimeViewerFramePath, bundleDirectory, "runtime-reference");
            var saveTargetReferencePath = CopyReferenceIntoBundle(_editorSaveFramePath, bundleDirectory, "save-target");

            var metadata = new
            {
                capturedAtLocal = DateTime.Now.ToString("O"),
                reason,
                workspace = CurrentWorkspaceLabel,
                row = SelectedBaseVariant?.DisplayName,
                species = SelectedBaseVariant?.Species,
                age = SelectedBaseVariant?.Age,
                gender = SelectedBaseVariant?.Gender,
                family = SelectedViewerFamily,
                sequence = SelectedViewerSequenceId,
                frame = ViewerSelectionSummary,
                selectedTool = SelectedEditorTool,
                isDirty = IsEditorDirty,
                editorLoadedFramePath = _loadedEditorFramePath,
                editorSaveFramePath = _editorSaveFramePath,
                viewerFramePath = ViewerFramePath,
                runtimeViewerFramePath = RuntimeViewerFramePath,
                files = new
                {
                    liveCanvasPath,
                    authoredReferencePath,
                    runtimeReferencePath,
                    saveTargetReferencePath
                }
            };

            File.WriteAllText(
                Path.Combine(bundleDirectory, "metadata.json"),
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));

            LastEditorCapturePath = bundleDirectory;
            EditorStatusMessage = $"Captured editor review bundle to {bundleDirectory}.";
            AddActivity("editor", $"Captured {reason} review bundle for {ViewerSelectionSummary}.");
        }
        catch (Exception ex)
        {
            EditorStatusMessage = $"Unable to capture editor review bundle: {ex.Message}";
            AddActivity("editor", $"Capture failed: {ex.Message}");
        }
    }

    private void SaveEditorPixelsToPath(string outputPath)
    {
        using var image = new Image<Rgba32>(_editorWidth, _editorHeight);
        for (var y = 0; y < _editorHeight; y++)
        {
            for (var x = 0; x < _editorWidth; x++)
            {
                image[x, y] = _editorPixels[(y * _editorWidth) + x];
            }
        }

        image.Save(outputPath);
    }

    private Bitmap? CreateBitmapFromPixels(Rgba32[] pixels)
    {
        if (_editorWidth <= 0 || _editorHeight <= 0 || pixels.Length != _editorWidth * _editorHeight)
        {
            return null;
        }

        using var image = new Image<Rgba32>(_editorWidth, _editorHeight);
        for (var y = 0; y < _editorHeight; y++)
        {
            for (var x = 0; x < _editorWidth; x++)
            {
                image[x, y] = pixels[(y * _editorWidth) + x];
            }
        }

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    private void RefreshEditorComparisonState()
    {
        if (_editorWidth <= 0 || _editorHeight <= 0 || _editorPixels.Length == 0 || _editorBaselinePixels.Length != _editorPixels.Length)
        {
            EditorPreviewBitmap = null;
            EditorBaselineBitmap = null;
            EditorDiffBitmap = null;
        }
        else
        {
            EditorPreviewBitmap = CreateBitmapFromPixels(_editorPixels);
            EditorBaselineBitmap = CreateBitmapFromPixels(_editorBaselinePixels);

            var diffPixels = new Rgba32[_editorPixels.Length];
            for (var index = 0; index < _editorPixels.Length; index++)
            {
                diffPixels[index] = _editorPixels[index].Equals(_editorBaselinePixels[index])
                    ? new Rgba32(0, 0, 0, 0)
                    : new Rgba32(255, 96, 64, 255);
            }

            EditorDiffBitmap = CreateBitmapFromPixels(diffPixels);
        }

        OnPropertyChanged(nameof(ChangedEditorPixelCount));
        OnPropertyChanged(nameof(EditorComparisonSummary));
        OnPropertyChanged(nameof(EditorBlinkCompareBitmap));
        OnPropertyChanged(nameof(EditorBlinkCompareSummary));
        OnPropertyChanged(nameof(HasEditorBaselineBitmap));
        OnPropertyChanged(nameof(IsEditorBaselineBitmapMissing));
        OnPropertyChanged(nameof(HasEditorDiffBitmap));
        OnPropertyChanged(nameof(IsEditorDiffBitmapMissing));
    }

    private int CalculateChangedEditorPixelCount()
    {
        if (_editorPixels.Length == 0 || _editorBaselinePixels.Length != _editorPixels.Length)
        {
            return 0;
        }

        var changed = 0;
        for (var index = 0; index < _editorPixels.Length; index++)
        {
            if (!_editorPixels[index].Equals(_editorBaselinePixels[index]))
            {
                changed++;
            }
        }

        return changed;
    }

    private static string CopyReferenceIntoBundle(string sourcePath, string bundleDirectory, string prefix)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return string.Empty;
        }

        var extension = Path.GetExtension(sourcePath);
        var targetPath = Path.Combine(bundleDirectory, $"{prefix}{extension}");
        File.Copy(sourcePath, targetPath, true);
        return targetPath;
    }

    [RelayCommand]
    private void CopyPreviousFrameIntoCurrent()
    {
        if (!TryGetAuthoredFramePathForOffset(0, out var currentPath) ||
            !TryGetAuthoredFramePathForOffset(-1, out var previousPath))
        {
            EditorStatusMessage = "Select a valid authored frame before using frame helpers.";
            return;
        }

        if (!File.Exists(previousPath))
        {
            EditorStatusMessage = "Previous authored frame is missing.";
            return;
        }

        if (!EnsureCurrentFrameReadyForTransfer(currentPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(currentPath) ?? string.Empty);
            File.Copy(previousPath, currentPath, true);
            UpdateViewer();
            LoadEditorFrame(currentPath, true);
            EditorStatusMessage = $"Copied {Path.GetFileName(previousPath)} into {Path.GetFileName(currentPath)}.";
        }
        catch (Exception ex)
        {
            EditorStatusMessage = $"Unable to copy previous frame: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DuplicateCurrentFrameToNext()
    {
        if (!TryGetAuthoredFramePathForOffset(0, out var currentPath) ||
            !TryGetAuthoredFramePathForOffset(1, out var nextPath))
        {
            EditorStatusMessage = "Select a valid authored frame before using frame helpers.";
            return;
        }

        if (!EnsureCurrentFrameReadyForTransfer(currentPath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(nextPath) ?? string.Empty);
            File.Copy(currentPath, nextPath, true);
            UpdateViewer();
            LoadEditorFrame(currentPath, false);
            EditorStatusMessage = $"Duplicated {Path.GetFileName(currentPath)} into {Path.GetFileName(nextPath)}.";
        }
        catch (Exception ex)
        {
            EditorStatusMessage = $"Unable to duplicate current frame: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SwapCurrentFrameWithPrevious()
    {
        if (!TryGetAuthoredFramePathForOffset(0, out var currentPath) ||
            !TryGetAuthoredFramePathForOffset(-1, out var previousPath))
        {
            EditorStatusMessage = "Select a valid authored frame before using frame helpers.";
            return;
        }

        if (!File.Exists(previousPath))
        {
            EditorStatusMessage = "Previous authored frame is missing.";
            return;
        }

        if (!EnsureCurrentFrameReadyForTransfer(currentPath))
        {
            return;
        }

        try
        {
            var currentBytes = File.Exists(currentPath) ? File.ReadAllBytes(currentPath) : Array.Empty<byte>();
            var previousBytes = File.ReadAllBytes(previousPath);

            Directory.CreateDirectory(Path.GetDirectoryName(currentPath) ?? string.Empty);
            File.WriteAllBytes(currentPath, previousBytes);
            if (currentBytes.Length == 0)
            {
                File.Delete(previousPath);
            }
            else
            {
                File.WriteAllBytes(previousPath, currentBytes);
            }

            UpdateViewer();
            LoadEditorFrame(currentPath, true);
            EditorStatusMessage = $"Swapped {Path.GetFileName(currentPath)} with {Path.GetFileName(previousPath)}.";
        }
        catch (Exception ex)
        {
            EditorStatusMessage = $"Unable to swap adjacent frames: {ex.Message}";
        }
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
            NotifyTrustedStateChanged();
            AddActivity("review", $"Saved review for {SelectedBaseVariant.DisplayName}.");
        }
        catch (Exception ex)
        {
            ReviewSaveMessage = $"Unable to save review: {ex.Message}";
            AddActivity("review", $"Could not save review for {SelectedBaseVariant.DisplayName}: {ex.Message}");
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
        NotifyTrustedStateChanged();
        AddActivity("review", $"Cleared review state for {SelectedBaseVariant.DisplayName}.");
    }

    [RelayCommand]
    private void MarkSelectedApproved() => SetSelectedReviewStatus("approved");

    [RelayCommand]
    private void MarkSelectedNeedsReview() => SetSelectedReviewStatus("needs_review");

    [RelayCommand]
    private void MarkSelectedToBeRepaired() => SetSelectedReviewStatus("to_be_repaired");

    [RelayCommand]
    private void SaveCurrentFrameReview()
    {
        if (_saveReviewData is null || !TryGetCurrentFrameReviewContext(out var frameId))
        {
            FrameReviewSaveMessage = "No current frame is ready to save.";
            return;
        }

        try
        {
            var key = BuildCurrentFrameReviewKey();
            if (string.IsNullOrWhiteSpace(key))
            {
                FrameReviewSaveMessage = "No current frame is ready to save.";
                return;
            }

            var record = new FrameReviewRecord
            {
                Species = SelectedBaseVariant!.Species,
                Age = SelectedBaseVariant.Age,
                Gender = SelectedBaseVariant.Gender,
                Color = SelectedViewerColor,
                Family = SelectedViewerFamily,
                SequenceId = SelectedViewerSequenceId,
                FrameIndex = _currentFrameIndex,
                FrameId = frameId,
                Status = SelectedFrameReviewStatus,
                Note = SelectedFrameReviewNote.Trim(),
                IssueTags = ParseFrameIssueTags(SelectedFrameIssueTagsText).ToArray(),
                UpdatedUtc = DateTimeOffset.UtcNow,
            };

            var removed = record.Status.Equals("unreviewed", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(record.Note) &&
                record.IssueTags.Length == 0;
            if (removed)
            {
                _frameReviewLookup.Remove(key);
            }
            else
            {
                _frameReviewLookup[key] = record;
            }

            _isLoadingFrameReview = true;
            SelectedFrameReviewUpdatedUtc = removed ? null : record.UpdatedUtc;
            _isLoadingFrameReview = false;
            _saveReviewData(BuildReviewData());
            FrameReviewSaveMessage = removed
                ? $"Cleared frame review for {frameId} at {DateTime.Now:h:mm tt}."
                : $"Saved frame review for {frameId} at {DateTime.Now:h:mm tt}.";
            OnPropertyChanged(nameof(CurrentFrameReviewSummary));
            OnPropertyChanged(nameof(CurrentFrameReviewUpdatedLabel));
            RefreshFrameReviewQueue();
            NotifyTrustedStateChanged();
            AddActivity("review", removed
                ? $"Cleared frame review for {frameId}."
                : $"Saved frame review for {frameId}.");
        }
        catch (Exception ex)
        {
            FrameReviewSaveMessage = $"Unable to save frame review: {ex.Message}";
            AddActivity("review", $"Could not save frame review: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ClearCurrentFrameReview()
    {
        var key = BuildCurrentFrameReviewKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            FrameReviewSaveMessage = "No current frame is selected.";
            return;
        }

        _frameReviewLookup.Remove(key);
        _isLoadingFrameReview = true;
        SelectedFrameReviewStatus = "unreviewed";
        SelectedFrameReviewNote = string.Empty;
        SelectedFrameIssueTagsText = string.Empty;
        SelectedFrameReviewUpdatedUtc = null;
        _isLoadingFrameReview = false;
        FrameReviewSaveMessage = "Unsaved frame review changes.";
        OnPropertyChanged(nameof(CurrentFrameReviewSummary));
        OnPropertyChanged(nameof(CurrentFrameReviewUpdatedLabel));
        RefreshFrameReviewQueue();
        NotifyTrustedStateChanged();
        AddActivity("review", "Cleared the current frame review.");
    }

    [RelayCommand]
    private void MarkCurrentFrameApproved() => SetCurrentFrameReviewStatus("approved");

    [RelayCommand]
    private void MarkCurrentFrameNeedsReview() => SetCurrentFrameReviewStatus("needs_review");

    [RelayCommand]
    private void MarkCurrentFrameToBeRepaired() => SetCurrentFrameReviewStatus("to_be_repaired");

    [RelayCommand]
    private void MarkCurrentFrameTemplateOnly() => SetCurrentFrameReviewStatus("template_only");

    [RelayCommand]
    private void AddCurrentFrameIssueTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        var tags = ParseFrameIssueTags(SelectedFrameIssueTagsText).ToList();
        if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(tag);
            SelectedFrameIssueTagsText = string.Join(", ", tags);
            AddActivity("review", $"Added frame issue tag '{tag}' to {ViewerSelectionSummary}.");
        }
    }

    [RelayCommand]
    private void ClearCurrentFrameIssueTags()
    {
        if (string.IsNullOrWhiteSpace(SelectedFrameIssueTagsText))
        {
            FrameReviewSaveMessage = "No frame issue tags to clear.";
            return;
        }

        SelectedFrameIssueTagsText = string.Empty;
        AddActivity("review", $"Cleared frame issue tags for {ViewerSelectionSummary}.");
    }

    [RelayCommand]
    private void CreateRequestFromSelected()
    {
        var target = SelectedBaseVariant;
        if (target is null)
        {
            RequestSaveMessage = "Select a row first to create a targeted request.";
            return;
        }

        PrefillRequestDraft(
            target.ReviewStatus.Equals("to_be_repaired", StringComparison.OrdinalIgnoreCase) ? "repair_existing" : "polish_existing",
            $"{ToTitleCase(target.Species)} {ToTitleCase(target.Age)} {ToTitleCase(target.Gender)}",
            $"{target.Species} | {target.Age} | {target.Gender}",
            target.ReviewNote,
            $"Preserve established {target.Species} silhouette, anatomy, and trusted family style.",
            "Do not drift into anthropomorphic posing or off-model proportions.",
            target.ReviewNote);
        RequestSaveMessage = "Draft prefilled from the selected row.";
        SelectedWorkspaceTabIndex = 3;
        AddActivity("request", $"Prefilled request draft from {target.DisplayName}.");
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
    private void CreateRequestFromCurrentFrame()
    {
        if (SelectedBaseVariant is null || !TryGetCurrentFrameReviewContext(out var frameId))
        {
            RequestSaveMessage = "Select a current frame first.";
            return;
        }

        var requestType = SelectedFrameReviewStatus.Equals("to_be_repaired", StringComparison.OrdinalIgnoreCase)
            ? "repair_existing"
            : "polish_existing";
        var titlePrefix = $"{ToTitleCase(SelectedBaseVariant.Species)} {ToTitleCase(SelectedBaseVariant.Age)} {ToTitleCase(SelectedBaseVariant.Gender)} {SelectedViewerFamily} {frameId}";
        var targetScope = $"{SelectedBaseVariant.Species} | {SelectedBaseVariant.Age} | {SelectedBaseVariant.Gender} | {SelectedViewerColor} | {SelectedViewerFamily} | {frameId}";
        var frameTagsSummary = BuildFrameIssueTagsSummary();
        var details = string.IsNullOrWhiteSpace(SelectedFrameReviewNote)
            ? $"Review and refine {frameId} in {SelectedViewerFamily}/{SelectedViewerSequenceId}. Tags: {frameTagsSummary}."
            : $"{SelectedFrameReviewNote} Tags: {frameTagsSummary}";

        PrefillRequestDraft(
            requestType,
            titlePrefix,
            targetScope,
            details,
            $"Preserve the established {SelectedBaseVariant.Species} body proportions, palette intent, and current sequence timing.",
            "Do not introduce anthropomorphic posing, outline corruption, or off-model anatomy.",
            $"{SelectedFrameReviewNote} {frameTagsSummary}".Trim());
        RequestSaveMessage = $"Draft prefilled from current frame {frameId}.";
        SelectedWorkspaceTabIndex = 3;
        AddActivity("request", $"Prefilled request draft from frame {frameId}.");
    }

    [RelayCommand]
    private void CreateRequestFromSelectedCandidate()
    {
        if (SelectedCandidateItem is null)
        {
            RequestSaveMessage = "Select a candidate first.";
            return;
        }

        var requestType = SelectedCandidateItem.Status.Equals("rejected", StringComparison.OrdinalIgnoreCase)
            ? "repair_existing"
            : "polish_existing";
        var details = string.IsNullOrWhiteSpace(SelectedCandidateItem.Note)
            ? $"Review candidate '{SelectedCandidateItem.Title}' against its current target and refine as needed."
            : SelectedCandidateItem.Note;

        PrefillRequestDraft(
            requestType,
            $"{SelectedCandidateItem.Title} {requestType.Replace('_', ' ')}",
            SelectedCandidateItem.TargetScope,
            details,
            "Preserve the best readable parts of the currently staged candidate while keeping the project style consistent.",
            "Do not accept broken silhouettes, placeholder artifacts, or drift away from the established template/reference.",
            SelectedCandidateItem.Note);
        RequestSaveMessage = $"Draft prefilled from candidate '{SelectedCandidateItem.Title}'.";
        SelectedWorkspaceTabIndex = 3;
        AddActivity("request", $"Prefilled request draft from candidate '{SelectedCandidateItem.Title}'.");
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
        var requestId = selectedId ?? BuildRequestId(DraftRequestType, DraftRequestTargetScope);
        var existing = Requests.FirstOrDefault(item => item.RequestId.Equals(requestId, StringComparison.OrdinalIgnoreCase));
        var history = (existing?.History ?? [])
            .Prepend(new RequestHistoryItemViewModel(existing is null ? "saved" : "updated", existing is null ? "Saved request draft." : "Updated request draft.", updatedUtc))
            .Take(24)
            .ToList();
        var request = new RequestItemViewModel(
            requestId,
            DraftRequestType,
            DraftRequestStatus,
            DraftRequestTitle.Trim(),
            DraftRequestTargetScope.Trim(),
            DraftRequestDetails.Trim(),
            DraftRequestMustPreserve.Trim(),
            DraftRequestMustAvoid.Trim(),
            DraftRequestSourceNote.Trim(),
            BuildRequestHealthSummary(
                DraftRequestTargetScope.Trim(),
                DraftRequestTitle.Trim(),
                DraftRequestDetails.Trim(),
                DraftRequestSourceNote.Trim()),
            history,
            updatedUtc);
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
        OnPropertyChanged(nameof(SelectedRequestHistorySummary));
        OnPropertyChanged(nameof(DraftRequestPreview));
        RefreshAutomationQueue();
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
            AppendRequestHistory(requestId, "exported", $"Exported handoff to {exportPath}.");
            RequestSaveMessage = $"Exported request handoff to {exportPath}.";
        }
        catch (Exception ex)
        {
            RequestSaveMessage = $"Unable to export request: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StageCurrentAuthoredCandidate() => StageCandidateFromCurrentFrame(useRuntimeTemplate: false);

    [RelayCommand]
    private void StageRuntimeCandidate() => StageCandidateFromCurrentFrame(useRuntimeTemplate: true);

    [RelayCommand]
    private void StageEditorCanvasCandidate()
    {
        if (_saveCandidateData is null)
        {
            CandidateSaveMessage = "No candidate store configured.";
            return;
        }

        if (_editorPixels.Length == 0 || !TryGetCurrentFrameReviewContext(out var frameId) || SelectedBaseVariant is null)
        {
            CandidateSaveMessage = "Load or create a frame in Paint before staging the live editor canvas.";
            return;
        }

        try
        {
            Directory.CreateDirectory(_candidateAssetDirectory);
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            var rowSlug = $"{SelectedBaseVariant.Species}-{SelectedBaseVariant.Age}-{SelectedBaseVariant.Gender}".ToLowerInvariant();
            var candidateId = $"{timestamp}-{rowSlug}-{SelectedViewerFamily}-{SelectedViewerSequenceId}-{_currentFrameIndex:00}-editor";
            var candidateDirectory = Path.Combine(_candidateAssetDirectory, candidateId);
            Directory.CreateDirectory(candidateDirectory);

            var candidateImagePath = Path.Combine(candidateDirectory, "candidate.png");
            SaveEditorPixelsToPath(candidateImagePath);

            var referenceImagePath = CopyReferenceIntoBundle(ViewerFramePath, candidateDirectory, "reference");
            var title = $"{ToTitleCase(SelectedBaseVariant.Species)} {ToTitleCase(SelectedBaseVariant.Age)} {ToTitleCase(SelectedBaseVariant.Gender)} {SelectedViewerFamily} {frameId} editor";
            var targetScope = $"{SelectedBaseVariant.Species} | {SelectedBaseVariant.Age} | {SelectedBaseVariant.Gender} | {SelectedViewerColor} | {SelectedViewerFamily} | {frameId}";
            var candidate = new CandidateItemViewModel(
                candidateId,
                title,
                targetScope,
                "editor_canvas",
                "staged",
                SelectedRequestItem?.RequestId ?? string.Empty,
                candidateImagePath,
                referenceImagePath,
                ViewerFramePath,
                string.Empty,
                "Staged from the live Paint canvas before approving or applying it to the authored slot.",
                DateTimeOffset.UtcNow);

            Candidates.Insert(0, candidate);
            SelectedCandidateItem = candidate;
            PersistCandidates();
            if (!string.IsNullOrWhiteSpace(candidate.RequestId))
            {
                AppendRequestHistory(candidate.RequestId, "candidate_staged", $"Staged live editor candidate '{candidate.Title}'.");
            }
            CandidateSaveMessage = $"Staged editor candidate '{candidate.Title}'.";
            OnPropertyChanged(nameof(CandidateSummary));
            AddActivity("candidate", $"Staged live editor canvas for {frameId}.");
        }
        catch (Exception ex)
        {
            CandidateSaveMessage = $"Unable to stage editor candidate: {ex.Message}";
            AddActivity("candidate", $"Editor candidate staging failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveSelectedCandidate()
    {
        if (SelectedCandidateItem is null)
        {
            CandidateSaveMessage = "Select a candidate first.";
            return;
        }

        SelectedCandidateItem.UpdatedUtc = DateTimeOffset.UtcNow;
        PersistCandidates();
        CandidateSaveMessage = $"Saved candidate '{SelectedCandidateItem.Title}'.";
        OnPropertyChanged(nameof(CandidateSummary));
    }

    [RelayCommand]
    private void MarkSelectedCandidateApproved() => SetSelectedCandidateStatus("approved");

    [RelayCommand]
    private void MarkSelectedCandidateRejected() => SetSelectedCandidateStatus("rejected");

    [RelayCommand]
    private void ApplySelectedCandidateToCurrentFrame()
    {
        if (SelectedCandidateItem is null || string.IsNullOrWhiteSpace(SelectedCandidateItem.CandidateImagePath) || !File.Exists(SelectedCandidateItem.CandidateImagePath))
        {
            CandidateSaveMessage = "Select a candidate with an available image first.";
            return;
        }

        if (!TryGetCurrentAuthoredFrameTargetPath(out var targetPath))
        {
            CandidateSaveMessage = "No current authored target is selected.";
            return;
        }

        if (!EnsureCurrentFrameReadyForTransfer(targetPath))
        {
            CandidateSaveMessage = "Save or revert the active edit before applying a candidate.";
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
            var backupPath = string.Empty;
            if (File.Exists(targetPath))
            {
                var candidateDirectory = Path.GetDirectoryName(SelectedCandidateItem.CandidateImagePath) ?? _candidateAssetDirectory;
                Directory.CreateDirectory(candidateDirectory);
                backupPath = Path.Combine(candidateDirectory, $"pre-import-{Path.GetFileName(targetPath)}");
                File.Copy(targetPath, backupPath, true);
            }

            File.Copy(SelectedCandidateItem.CandidateImagePath, targetPath, true);
            SelectedCandidateItem.Status = "imported";
            SelectedCandidateItem.SetImportBackupPath(backupPath);
            SelectedCandidateItem.UpdatedUtc = DateTimeOffset.UtcNow;
            PersistCandidates();
            if (!string.IsNullOrWhiteSpace(SelectedCandidateItem.RequestId))
            {
                AppendRequestHistory(SelectedCandidateItem.RequestId, "candidate_imported", $"Applied candidate '{SelectedCandidateItem.Title}' to {Path.GetFileName(targetPath)}.");
            }
            UpdateViewer();
            LoadEditorFrame(targetPath, true);
            CandidateSaveMessage = string.IsNullOrWhiteSpace(backupPath)
                ? $"Applied candidate '{SelectedCandidateItem.Title}' into {Path.GetFileName(targetPath)}."
                : $"Applied candidate '{SelectedCandidateItem.Title}' into {Path.GetFileName(targetPath)} with a restore backup.";
            OnPropertyChanged(nameof(CandidateSummary));
            OnPropertyChanged(nameof(SelectedCandidateSummary));
            AddActivity("candidate", $"Applied '{SelectedCandidateItem.Title}' into {Path.GetFileName(targetPath)}.");
        }
        catch (Exception ex)
        {
            CandidateSaveMessage = $"Unable to apply candidate: {ex.Message}";
            AddActivity("candidate", $"Candidate apply failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void RestoreSelectedCandidateBackup()
    {
        if (SelectedCandidateItem is null || string.IsNullOrWhiteSpace(SelectedCandidateItem.ImportBackupPath) || !File.Exists(SelectedCandidateItem.ImportBackupPath))
        {
            CandidateSaveMessage = "This candidate does not have an import backup to restore.";
            return;
        }

        var targetPath = !string.IsNullOrWhiteSpace(SelectedCandidateItem.TargetFramePath)
            ? SelectedCandidateItem.TargetFramePath
            : ViewerFramePath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            CandidateSaveMessage = "No authored target is available for restore.";
            return;
        }

        if (!EnsureCurrentFrameReadyForTransfer(targetPath))
        {
            CandidateSaveMessage = "Save or revert the active edit before restoring a backup.";
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
            File.Copy(SelectedCandidateItem.ImportBackupPath, targetPath, true);
            SelectedCandidateItem.Status = "approved";
            SelectedCandidateItem.UpdatedUtc = DateTimeOffset.UtcNow;
            PersistCandidates();
            UpdateViewer();
            LoadEditorFrame(targetPath, true);
            CandidateSaveMessage = $"Restored backup for '{SelectedCandidateItem.Title}' into {Path.GetFileName(targetPath)}.";
            OnPropertyChanged(nameof(CandidateSummary));
            OnPropertyChanged(nameof(SelectedCandidateSummary));
            AddActivity("candidate", $"Restored backup for '{SelectedCandidateItem.Title}'.");
        }
        catch (Exception ex)
        {
            CandidateSaveMessage = $"Unable to restore candidate backup: {ex.Message}";
            AddActivity("candidate", $"Candidate restore failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void DeleteSelectedCandidate()
    {
        if (SelectedCandidateItem is null)
        {
            CandidateSaveMessage = "Select a candidate first.";
            return;
        }

        var candidate = SelectedCandidateItem;
        Candidates.Remove(candidate);
        if (!string.IsNullOrWhiteSpace(candidate.CandidateImagePath) && File.Exists(candidate.CandidateImagePath))
        {
            try
            {
                File.Delete(candidate.CandidateImagePath);
            }
            catch
            {
                // Keep record deletion resilient even if the file is busy.
            }
        }

        if (!string.IsNullOrWhiteSpace(candidate.ReferenceImagePath) && File.Exists(candidate.ReferenceImagePath))
        {
            try
            {
                File.Delete(candidate.ReferenceImagePath);
            }
            catch
            {
                // Keep record deletion resilient even if the file is busy.
            }
        }

        SelectedCandidateItem = Candidates.FirstOrDefault();
        PersistCandidates();
        CandidateSaveMessage = $"Deleted candidate '{candidate.Title}'.";
        OnPropertyChanged(nameof(CandidateSummary));
    }

    [RelayCommand]
    private void LoadSelectedCandidateIntoPaint()
    {
        if (SelectedCandidateItem is null || string.IsNullOrWhiteSpace(SelectedCandidateItem.CandidateImagePath) || !File.Exists(SelectedCandidateItem.CandidateImagePath))
        {
            CandidateSaveMessage = "Select a candidate with an available image first.";
            return;
        }

        IsPlaybackEnabled = false;
        LoadEditorFrame(SelectedCandidateItem.CandidateImagePath, true, ViewerFramePath);
        OpenPaintWorkspace();
        CandidateSaveMessage = $"Loaded candidate '{SelectedCandidateItem.Title}' into Paint.";
        AddActivity("candidate", $"Loaded candidate '{SelectedCandidateItem.Title}' into Paint.");
    }

    partial void OnSelectedSpeciesFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedFamilyFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedCoverageStatusFilterChanged(string value) => ApplyFilters();
    partial void OnSelectedReviewStatusFilterChanged(string value) => ApplyFilters();

    partial void OnSelectedViewerColorChanged(string value)
    {
        IsPlaybackEnabled = false;
        _currentFrameIndex = 0;
        _playbackFrameIndex = 0;
        UpdateViewer();
        NotifyLoopStateChanged();
        PersistWorkspaceState();
    }

    partial void OnSelectedViewerFamilyChanged(string value)
    {
        IsPlaybackEnabled = false;
        UpdateSequenceOptions(true);
        UpdateViewer();
        OnPropertyChanged(nameof(ProjectPaletteSaveHint));
        NotifyLoopStateChanged();
        PersistWorkspaceState();
    }

    partial void OnSelectedViewerSequenceIdChanged(string value)
    {
        IsPlaybackEnabled = false;
        _currentFrameIndex = 0;
        _playbackFrameIndex = 0;
        UpdateViewer();
        OnPropertyChanged(nameof(ProjectPaletteSaveHint));
        NotifyLoopStateChanged();
        PersistWorkspaceState();
    }

    partial void OnSelectedWorkspaceTabIndexChanged(int value)
    {
        NotifyLoopStateChanged();
        PersistWorkspaceState();
    }

    partial void OnSelectedStudioTabIndexChanged(int value)
    {
        NotifyLoopStateChanged();
        PersistWorkspaceState();
    }
    partial void OnIsEditorDirtyChanged(bool value) => NotifyLoopStateChanged();
    partial void OnAutoCaptureOnStrokeChanged(bool value) => OnPropertyChanged(nameof(EditorCaptureSummary));
    partial void OnAutoCaptureOnSaveChanged(bool value) => OnPropertyChanged(nameof(EditorCaptureSummary));
    partial void OnLastEditorCapturePathChanged(string value) => OnPropertyChanged(nameof(LastEditorCaptureSummary));
    partial void OnIsEditorBlinkCompareEnabledChanged(bool value)
    {
        if (value && HasEditorBaselineBitmap && IsEditorFrameLoaded)
        {
            _showEditorBaselineBlinkFrame = false;
            _editorBlinkTimer.Start();
        }
        else
        {
            _editorBlinkTimer.Stop();
            _showEditorBaselineBlinkFrame = false;
        }

        OnPropertyChanged(nameof(EditorBlinkCompareSummary));
        OnPropertyChanged(nameof(EditorBlinkCompareBitmap));
    }
    partial void OnControlModeChanged(string value)
    {
        OnPropertyChanged(nameof(ControlModeSummary));
        OnPropertyChanged(nameof(ControlModeHint));
        OnPropertyChanged(nameof(AutomationQueueHint));

        if (_isRestoringWorkspaceState)
        {
            return;
        }

        PersistWorkspaceState(includeEditorDraft: true);
        AddActivity("session", $"Switched control to {value.Replace('_', ' ')}.");
    }

    partial void OnIsOnionSkinEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(OnionSkinOpacityFraction));
        OnPropertyChanged(nameof(OnionSkinSummary));
        UpdateViewer();
    }

    partial void OnOnionSkinOpacityChanged(int value)
    {
        OnPropertyChanged(nameof(OnionSkinOpacityFraction));
        OnPropertyChanged(nameof(OnionSkinSummary));
    }

    partial void OnPreviewPlaybackMsChanged(int value)
    {
        var clampedValue = Math.Clamp(value, 50, 1000);

        if (clampedValue != value)
        {
            PreviewPlaybackMs = clampedValue;
            return;
        }

        _previewTimer.Interval = TimeSpan.FromMilliseconds(clampedValue);
        OnPropertyChanged(nameof(PlaybackSpeedSummary));
        EditorStatusMessage = $"Playback speed set to {clampedValue} ms per frame.";
    }

    partial void OnSelectedEditorToolChanged(string value)
    {
        EditorStatusMessage = $"Editor tool set to {ToTitleCase(value)}.";
        OnPropertyChanged(nameof(SelectedEditorColorSummary));
        OnPropertyChanged(nameof(EditorToolHint));
    }

    partial void OnSelectedEditorBrushSizeChanged(int value)
    {
        OnPropertyChanged(nameof(SelectedEditorColorSummary));
        EditorStatusMessage = $"Brush size set to {value}px.";
    }

    partial void OnSelectedEditorZoomChanged(int value)
    {
        OnPropertyChanged(nameof(EditorCanvasWidth));
        OnPropertyChanged(nameof(EditorCanvasHeight));
        OnPropertyChanged(nameof(EditorPixelSize));
        NotifyEditorSelectionOverlayChanged();
        EditorStatusMessage = $"Zoom set to {value}px per sprite pixel.";
    }

    partial void OnEditorMirrorHorizontalChanged(bool value)
    {
        OnPropertyChanged(nameof(EditorMirrorSummary));
        EditorStatusMessage = $"Horizontal mirror {(value ? "enabled" : "disabled")}.";
    }

    partial void OnEditorMirrorVerticalChanged(bool value)
    {
        OnPropertyChanged(nameof(EditorMirrorSummary));
        EditorStatusMessage = $"Vertical mirror {(value ? "enabled" : "disabled")}.";
    }

    partial void OnEditorFillShapesChanged(bool value)
    {
        OnPropertyChanged(nameof(EditorShapeModeSummary));
        OnPropertyChanged(nameof(EditorToolHint));
        EditorStatusMessage = value ? "Shape fill enabled." : "Shape outline mode enabled.";
    }

    partial void OnSelectedEditorColorHexChanged(string value)
    {
        if (!_isSynchronizingEditorColor && TryParseHexColor(value, out var color))
        {
            _isSynchronizingEditorColor = true;
            EditorRed = color.R;
            EditorGreen = color.G;
            EditorBlue = color.B;
            EditorAlpha = color.A;
            _isSynchronizingEditorColor = false;
            SyncHsvFromChannels();
        }

        UpdatePaletteSelection();
        OnPropertyChanged(nameof(SelectedEditorColorSummary));
        OnPropertyChanged(nameof(EditorSelectedColorBrush));
    }

    partial void OnEditorRedChanged(int value) => SyncHexFromChannels();
    partial void OnEditorGreenChanged(int value) => SyncHexFromChannels();
    partial void OnEditorBlueChanged(int value) => SyncHexFromChannels();
    partial void OnEditorAlphaChanged(int value) => SyncHexFromChannels();
    partial void OnEditorHueChanged(int value) => SyncChannelsFromHsv();
    partial void OnEditorSaturationChanged(int value) => SyncChannelsFromHsv();
    partial void OnEditorValueChanged(int value) => SyncChannelsFromHsv();

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

        OnPropertyChanged(nameof(PlaybackModeSummary));
    }

    partial void OnIsBlinkCompareEnabledChanged(bool value)
    {
        if (value && HasCurrentFrameBitmap && HasRuntimeFrameBitmap)
        {
            _compareBlinkTimer.Start();
        }
        else
        {
            _compareBlinkTimer.Stop();
            _showRuntimeBlinkFrame = false;
            OnPropertyChanged(nameof(BlinkCompareBitmap));
        }

        OnPropertyChanged(nameof(BlinkCompareSummary));
    }

    partial void OnDraftRequestTypeChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestStatusChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestTitleChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestTargetScopeChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestDetailsChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestMustPreserveChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestMustAvoidChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnDraftRequestSourceNoteChanged(string value) => OnPropertyChanged(nameof(DraftRequestPreview));
    partial void OnPlanningSpeciesTextChanged(string value) => NotifyPlanningStateChanged();
    partial void OnPlanningAgeTextChanged(string value) => NotifyPlanningStateChanged();
    partial void OnPlanningGenderTextChanged(string value) => NotifyPlanningStateChanged();
    partial void OnPlanningColorTextChanged(string value) => NotifyPlanningStateChanged();
    partial void OnPlanningFamilyBlueprintTextChanged(string value) => NotifyPlanningStateChanged();
    partial void OnPlanningProjectIdChanged(string value) => NotifyPlanningStateChanged();
    partial void OnPlanningDisplayNameChanged(string value) => NotifyPlanningStateChanged();
    partial void OnPlanningRootPathChanged(string value) => NotifyPlanningStateChanged();
    partial void OnPlanningExportPathChanged(string value) => NotifyPlanningStateChanged();
    partial void OnSelectedFrameReviewStatusChanged(string value)
    {
        if (_isLoadingFrameReview)
        {
            return;
        }

        FrameReviewSaveMessage = "Unsaved frame review changes.";
        OnPropertyChanged(nameof(CurrentFrameReviewSummary));
        NotifyTrustedStateChanged();
    }

    partial void OnSelectedFrameReviewNoteChanged(string value)
    {
        if (_isLoadingFrameReview)
        {
            return;
        }

        FrameReviewSaveMessage = "Unsaved frame review changes.";
        OnPropertyChanged(nameof(CurrentFrameReviewSummary));
    }

    partial void OnSelectedFrameIssueTagsTextChanged(string value)
    {
        if (_isLoadingFrameReview)
        {
            return;
        }

        FrameReviewSaveMessage = "Unsaved frame review changes.";
        OnPropertyChanged(nameof(CurrentFrameReviewSummary));
    }

    partial void OnSelectedFrameReviewUpdatedUtcChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(CurrentFrameReviewUpdatedLabel));

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
        OnPropertyChanged(nameof(SelectedBaseVariantFrameQualitySummary));
        OnPropertyChanged(nameof(SelectedBaseVariantFrameIssueSummary));
        OnPropertyChanged(nameof(CurrentFrameReviewTargetSummary));
        OnPropertyChanged(nameof(ProjectPaletteSaveHint));
        UpdateAuditProgressSummary();
        NotifyLoopStateChanged();
        PersistWorkspaceState();

        if (!_isSynchronizingSelection)
        {
            _isSynchronizingSelection = true;
            SelectedRepairQueueItem = newValue?.ReviewStatus.Equals("to_be_repaired", StringComparison.OrdinalIgnoreCase) == true
                ? newValue
                : null;
            _isSynchronizingSelection = false;
        }

        UpdateViewer();

        if (newValue is not null)
        {
            AddActivity("review", $"Selected {newValue.DisplayName}.");
        }
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
        SurfaceQueueWorkspace(false);
        NotifyCurrentTaskChanged();
    }

    partial void OnSelectedFrameReviewQueueItemChanged(FrameReviewQueueItemViewModel? value)
    {
        if (_isSynchronizingFrameQueueSelection || value is null)
        {
            return;
        }

        var variant = _allBaseVariants.FirstOrDefault(
            row => row.Species.Equals(value.Species, StringComparison.OrdinalIgnoreCase) &&
                   row.Age.Equals(value.Age, StringComparison.OrdinalIgnoreCase) &&
                   row.Gender.Equals(value.Gender, StringComparison.OrdinalIgnoreCase));
        if (variant is null)
        {
            return;
        }

        _isSynchronizingFrameQueueSelection = true;
        SelectedBaseVariant = variant;
        if (ViewerColorOptions.Contains(value.Color))
        {
            SelectedViewerColor = value.Color;
        }

        if (ViewerFamilyOptions.Contains(value.Family))
        {
            SelectedViewerFamily = value.Family;
        }

        if (ViewerSequenceOptions.Contains(value.SequenceId))
        {
            SelectedViewerSequenceId = value.SequenceId;
        }

        _currentFrameIndex = value.FrameIndex;
        _playbackFrameIndex = value.FrameIndex;
        UpdateViewer();
        _isSynchronizingFrameQueueSelection = false;
        SurfaceAnimateWorkspace(false);
        NotifyCurrentTaskChanged();
        AddActivity("review", $"Jumped to flagged frame {value.FrameId} from the frame review queue.");
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
        OnPropertyChanged(nameof(SelectedRequestHealthSummary));
        OnPropertyChanged(nameof(StudioAiHandoffSummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskHistorySummary));
        TryNavigateToTargetScope(value.TargetScope);
        NotifyCurrentTaskChanged();
    }

    partial void OnSelectedAutomationTaskItemChanged(AutomationTaskItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedAutomationTaskSummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskPromptSummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskActivitySummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskCandidateSummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskHistorySummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskCandidates));
        OnPropertyChanged(nameof(SelectedAutomationTaskLinkedCandidatesSummary));
        OnPropertyChanged(nameof(StudioAiHandoffSummary));
        NotifyCurrentTaskChanged();

        if (value is null)
        {
            return;
        }

        var matchingRequest = Requests.FirstOrDefault(request =>
            request.RequestId.Equals(value.RequestId, StringComparison.OrdinalIgnoreCase));
        if (matchingRequest is not null && !ReferenceEquals(SelectedRequestItem, matchingRequest))
        {
            SelectedRequestItem = matchingRequest;
        }

        TryNavigateToTargetScope(value.TargetScope);
    }

    partial void OnSelectedCandidateItemChanged(CandidateItemViewModel? oldValue, CandidateItemViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= SelectedCandidateItemOnPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += SelectedCandidateItemOnPropertyChanged;
        }

        LoadSelectedCandidatePreview(newValue);
        OnPropertyChanged(nameof(SelectedCandidateSummary));
        OnPropertyChanged(nameof(SelectedCandidateTargetSummary));
        NotifyCurrentTaskChanged();
        OnPropertyChanged(nameof(StudioCandidateHandoffSummary));
    }

    partial void OnSelectedProjectPaletteItemChanged(ProjectPaletteItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedProjectPaletteSummary));
    }

    partial void OnSelectedTrustedExportHistoryItemChanged(TrustedExportHistoryItemViewModel? value)
    {
        OnPropertyChanged(nameof(TrustedExportHistorySummary));
    }

    partial void OnSelectedValidationReportItemChanged(ValidationReportItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedValidationReportSummary));
    }

    partial void OnSelectedPlanningUnmappedEntryItemChanged(PlanningUnmappedEntryItemViewModel? value)
    {
        OnPropertyChanged(nameof(PlanningUnmappedEntrySummary));
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
            RefreshFrameReviewQueue();
            NotifyTrustedStateChanged();
        }

        if (e.PropertyName is nameof(BaseVariantRowItemViewModel.FrameReviewState)
            or nameof(BaseVariantRowItemViewModel.ReviewedFrameCount)
            or nameof(BaseVariantRowItemViewModel.ApprovedFrameCount)
            or nameof(BaseVariantRowItemViewModel.FlaggedFrameCount)
            or nameof(BaseVariantRowItemViewModel.TemplateFrameCount)
            or nameof(BaseVariantRowItemViewModel.FrameIssueSummary))
        {
            OnPropertyChanged(nameof(SelectedBaseVariantFrameQualitySummary));
            OnPropertyChanged(nameof(SelectedBaseVariantFrameIssueSummary));
        }
    }

    private void SelectedCandidateItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender != SelectedCandidateItem)
        {
            return;
        }

        if (e.PropertyName is nameof(CandidateItemViewModel.Status) or nameof(CandidateItemViewModel.Note) or nameof(CandidateItemViewModel.UpdatedUtc))
        {
            CandidateSaveMessage = "Unsaved candidate changes.";
            OnPropertyChanged(nameof(SelectedCandidateSummary));
            OnPropertyChanged(nameof(CandidateSummary));
        }
    }

    private void SetBitmap(ref Bitmap? target, Bitmap? value)
    {
        if (ReferenceEquals(target, value))
        {
            return;
        }

        SetProperty(ref target, value);
    }

    private void SelectVariantByOffset(int delta)
    {
        if (FilteredBaseVariants.Count == 0)
        {
            return;
        }

        var currentIndex = SelectedBaseVariant is null ? 0 : FilteredBaseVariants.IndexOf(SelectedBaseVariant);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = (currentIndex + delta + FilteredBaseVariants.Count) % FilteredBaseVariants.Count;
        SelectedBaseVariant = FilteredBaseVariants[nextIndex];
    }

    private void UpdateAuditProgressSummary()
    {
        if (SelectedBaseVariant is null || FilteredBaseVariants.Count == 0)
        {
            AuditProgressSummary = "No row selected.";
            return;
        }

        var currentIndex = FilteredBaseVariants.IndexOf(SelectedBaseVariant);
        var needsAttention = FilteredBaseVariants.Count(row => !row.ReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase));
        AuditProgressSummary = $"Audit row {currentIndex + 1}/{FilteredBaseVariants.Count}  |  {needsAttention} still need attention";
    }

    private void EnsureStrokeSnapshot()
    {
        if (_strokeSnapshotCaptured)
        {
            return;
        }

        CaptureUndoState();
        _strokeSnapshotCaptured = true;
    }

    private void CaptureUndoState()
    {
        var activePixels = GetActiveLayerPixels();
        if (activePixels is null || activePixels.Length == 0)
        {
            return;
        }

        _undoHistory.Push(new EditorHistoryState(_activeEditorLayerId, (Rgba32[])activePixels.Clone()));
        _redoHistory.Clear();
        NotifyEditorHistoryChanged();
    }

    private void ApplyHistoryState(EditorHistoryState state)
    {
        if (!_editorLayerPixels.ContainsKey(state.LayerId))
        {
            return;
        }

        _activeEditorLayerId = state.LayerId;
        _editorLayerPixels[state.LayerId] = (Rgba32[])state.Pixels.Clone();
        RefreshEditorComposite();
        UpdateLayerSelection();
        RefreshEditorPalette();
        RefreshEditorPixels();
        RefreshEditorComparisonState();
        OnPropertyChanged(nameof(EditorLayerSummary));
    }

    private void SyncHexFromChannels()
    {
        if (_isSynchronizingEditorColor || _isSynchronizingEditorHsv)
        {
            return;
        }

        _isSynchronizingEditorColor = true;
        SelectedEditorColorHex = ToHexColor(new Rgba32((byte)EditorRed, (byte)EditorGreen, (byte)EditorBlue, (byte)EditorAlpha));
        _isSynchronizingEditorColor = false;
        SyncHsvFromChannels();
        OnPropertyChanged(nameof(EditorSelectedColorBrush));
        RebuildEditorQuickColorPresets();
    }

    private void SyncHsvFromChannels()
    {
        if (_isSynchronizingEditorHsv)
        {
            return;
        }

        _isSynchronizingEditorHsv = true;
        var red = EditorRed / 255.0;
        var green = EditorGreen / 255.0;
        var blue = EditorBlue / 255.0;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;

        double hue;
        if (delta == 0)
        {
            hue = 0;
        }
        else if (Math.Abs(max - red) < double.Epsilon)
        {
            hue = 60 * (((green - blue) / delta) % 6);
        }
        else if (Math.Abs(max - green) < double.Epsilon)
        {
            hue = 60 * (((blue - red) / delta) + 2);
        }
        else
        {
            hue = 60 * (((red - green) / delta) + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        var saturation = max == 0 ? 0 : delta / max;
        EditorHue = (int)Math.Round(hue);
        EditorSaturation = (int)Math.Round(saturation * 100);
        EditorValue = (int)Math.Round(max * 100);
        _isSynchronizingEditorHsv = false;
        OnPropertyChanged(nameof(EditorHsvSummary));
        RebuildEditorQuickColorPresets();
    }

    private void SyncChannelsFromHsv()
    {
        if (_isSynchronizingEditorHsv)
        {
            return;
        }

        _isSynchronizingEditorHsv = true;

        var color = CreateColorFromHsv(EditorHue, EditorSaturation, EditorValue, EditorAlpha);
        _isSynchronizingEditorColor = true;
        EditorRed = color.R;
        EditorGreen = color.G;
        EditorBlue = color.B;
        _isSynchronizingEditorColor = false;
        _isSynchronizingEditorHsv = false;

        SelectedEditorColorHex = ToHexColor(color);
        OnPropertyChanged(nameof(EditorSelectedColorBrush));
        OnPropertyChanged(nameof(EditorHsvSummary));
        RebuildEditorQuickColorPresets();
    }

    private void RebuildEditorQuickColorPresets()
    {
        EditorHuePresets.Clear();
        EditorTonePresets.Clear();

        for (var index = 0; index < 12; index++)
        {
            var hue = index * 30;
            var color = CreateColorFromHsv(hue, 100, 100, EditorAlpha);
            var hex = ToHexColor(color);
            EditorHuePresets.Add(new ColorPresetItemViewModel(
                $"{hue} deg",
                hex,
                CreateBrush(color),
                $"Hue {hue}"));
        }

        var toneSteps = new (string Label, int Saturation, int Value)[]
        {
            ("Shadow", Math.Min(EditorSaturation + 10, 100), 22),
            ("Deep", Math.Min(EditorSaturation + 5, 100), 38),
            ("Base", EditorSaturation, Math.Max(EditorValue, 48)),
            ("Soft", Math.Max(EditorSaturation - 20, 0), Math.Max(EditorValue, 68)),
            ("Light", Math.Max(EditorSaturation - 30, 0), 82),
            ("Highlight", Math.Max(EditorSaturation - 40, 0), 95)
        };

        foreach (var step in toneSteps)
        {
            var color = CreateColorFromHsv(EditorHue, step.Saturation, step.Value, EditorAlpha);
            var hex = ToHexColor(color);
            EditorTonePresets.Add(new ColorPresetItemViewModel(
                step.Label,
                hex,
                CreateBrush(color),
                $"{step.Label}: {hex}"));
        }

        OnPropertyChanged(nameof(EditorQuickColorSummary));
    }

    private static Rgba32 CreateColorFromHsv(int hueDegrees, int saturationPercent, int valuePercent, int alpha)
    {
        var hue = ((hueDegrees % 360) + 360) % 360;
        var saturation = Math.Clamp(saturationPercent / 100.0, 0.0, 1.0);
        var value = Math.Clamp(valuePercent / 100.0, 0.0, 1.0);
        var chroma = value * saturation;
        var huePrime = hue / 60.0;
        var x = chroma * (1 - Math.Abs((huePrime % 2) - 1));

        var (redPrime, greenPrime, bluePrime) = huePrime switch
        {
            >= 0 and < 1 => (chroma, x, 0.0),
            >= 1 and < 2 => (x, chroma, 0.0),
            >= 2 and < 3 => (0.0, chroma, x),
            >= 3 and < 4 => (0.0, x, chroma),
            >= 4 and < 5 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x)
        };

        var match = value - chroma;
        return new Rgba32(
            (byte)Math.Round((redPrime + match) * 255),
            (byte)Math.Round((greenPrime + match) * 255),
            (byte)Math.Round((bluePrime + match) * 255),
            (byte)Math.Clamp(alpha, 0, 255));
    }

    private void NotifyEditorHistoryChanged()
    {
        OnPropertyChanged(nameof(EditorHistorySummary));
    }

    private void NotifyEditorSelectionOverlayChanged()
    {
        OnPropertyChanged(nameof(HasEditorSelectionOverlay));
        OnPropertyChanged(nameof(EditorSelectionOverlayLeft));
        OnPropertyChanged(nameof(EditorSelectionOverlayTop));
        OnPropertyChanged(nameof(EditorSelectionOverlayWidth));
        OnPropertyChanged(nameof(EditorSelectionOverlayHeight));
        OnPropertyChanged(nameof(EditorSelectionHandleWestLeft));
        OnPropertyChanged(nameof(EditorSelectionHandleEastLeft));
        OnPropertyChanged(nameof(EditorSelectionHandleNorthTop));
        OnPropertyChanged(nameof(EditorSelectionHandleSouthTop));
        OnPropertyChanged(nameof(EditorSelectionHandleCenterX));
        OnPropertyChanged(nameof(EditorSelectionHandleCenterY));
        OnPropertyChanged(nameof(EditorSelectionToolbarLeft));
        OnPropertyChanged(nameof(EditorSelectionToolbarTop));
    }

    private void LoadEditorFrame(string? framePath, bool forceReload, string? saveTargetPath = null)
    {
        if (string.IsNullOrWhiteSpace(framePath) || !File.Exists(framePath))
        {
            ClearEditor("No authored frame is loaded for the editor.");
            AddActivity("editor", "Editor load skipped because no source frame was available.");
            return;
        }

        if (!forceReload &&
            string.Equals(_loadedEditorFramePath, framePath, StringComparison.OrdinalIgnoreCase) &&
            _editorPixels.Length > 0)
        {
            AddActivity("editor", $"Frame {Path.GetFileName(framePath)} is already loaded.");
            return;
        }

        if (!forceReload &&
            IsEditorDirty &&
            !string.Equals(_loadedEditorFramePath, framePath, StringComparison.OrdinalIgnoreCase))
        {
            EditorStatusMessage = "Editor has unsaved changes. Save or revert before switching frames.";
            AddActivity("editor", "Editor load blocked because the current frame has unsaved changes.");
            return;
        }

        try
        {
            using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(framePath);
            _editorWidth = image.Width;
            _editorHeight = image.Height;
            _editorPixels = new Rgba32[_editorWidth * _editorHeight];

            for (var y = 0; y < _editorHeight; y++)
            {
                for (var x = 0; x < _editorWidth; x++)
                {
                    _editorPixels[(y * _editorWidth) + x] = image[x, y];
                }
            }

            _editorBaselinePixels = (Rgba32[])_editorPixels.Clone();
            InitializeEditorLayers(_editorBaselinePixels);
            _loadedEditorFramePath = framePath;
            _editorSaveFramePath = string.IsNullOrWhiteSpace(saveTargetPath) ? framePath : saveTargetPath;
            EditorLoadedFramePath = framePath;
            IsEditorDirty = false;
            _undoHistory.Clear();
            _redoHistory.Clear();
            _strokeSnapshotCaptured = false;
            _selectedEditorIndices.Clear();
            _editorClipboard = null;
            _selectionAnchorIndex = null;
            _moveAnchorIndex = null;
            _movePreviewBasePixels = null;
            _movePreviewPixels = null;
            _shapePreviewBasePixels = null;
            RefreshEditorPalette();
            RefreshEditorPixels();
            RefreshEditorComparisonState();
            _isSynchronizingEditorColor = true;
            EditorRed = 255;
            EditorGreen = 255;
            EditorBlue = 255;
            EditorAlpha = 255;
            if (_editorPixels.Length > 0)
            {
                var firstOpaque = _editorPixels.FirstOrDefault(pixel => pixel.A > 0);
                if (firstOpaque.A > 0)
                {
                    EditorRed = firstOpaque.R;
                    EditorGreen = firstOpaque.G;
                    EditorBlue = firstOpaque.B;
                    EditorAlpha = firstOpaque.A;
                    SelectedEditorColorHex = ToHexColor(firstOpaque);
                }
            }
            _isSynchronizingEditorColor = false;
            SyncHsvFromChannels();
            EditorStatusMessage = _editorSaveFramePath.Equals(framePath, StringComparison.OrdinalIgnoreCase)
                ? $"Loaded {Path.GetFileName(framePath)} into the in-app editor."
                : $"Loaded template {Path.GetFileName(framePath)}. Saving goes to {Path.GetFileName(_editorSaveFramePath)}.";
            OnPropertyChanged(nameof(EditorCanvasSummary));
            OnPropertyChanged(nameof(EditorCanvasWidth));
            OnPropertyChanged(nameof(EditorCanvasHeight));
            OnPropertyChanged(nameof(EditorPixelSize));
            OnPropertyChanged(nameof(EditorTemplateSummary));
            OnPropertyChanged(nameof(EditorSelectedColorBrush));
            NotifyEditorHistoryChanged();
            OnPropertyChanged(nameof(EditorSelectionSummary));
            OnPropertyChanged(nameof(EditorSelectionBoundsSummary));
            OnPropertyChanged(nameof(EditorSelectionTransformHint));
            OnPropertyChanged(nameof(IsEditorFrameLoaded));
            OnPropertyChanged(nameof(IsEditorFrameMissing));
            OnPropertyChanged(nameof(EditorComparisonSummary));
            OnPropertyChanged(nameof(HasEditorBaselineBitmap));
            OnPropertyChanged(nameof(IsEditorBaselineBitmapMissing));
            OnPropertyChanged(nameof(HasEditorDiffBitmap));
            OnPropertyChanged(nameof(IsEditorDiffBitmapMissing));
            OnPropertyChanged(nameof(EditorLayerSummary));
            RefreshFrameHistory();
            NotifyLoopStateChanged();
            PersistWorkspaceState(includeEditorDraft: true);
            AddActivity("editor", EditorStatusMessage);
        }
        catch (Exception ex)
        {
            ClearEditor($"Unable to load frame into editor: {ex.Message}");
            AddActivity("editor", $"Failed to load frame {Path.GetFileName(framePath)}: {ex.Message}");
        }
    }

    private void ApplyBrushAt(int centerIndex, Rgba32 color)
    {
        if (_editorWidth <= 0 || _editorHeight <= 0)
        {
            return;
        }

        var centerX = centerIndex % _editorWidth;
        var centerY = centerIndex / _editorWidth;
        var halfLow = Math.Max(0, SelectedEditorBrushSize - 1) / 2;
        var halfHigh = SelectedEditorBrushSize / 2;
        foreach (var (mirrorX, mirrorY) in EnumerateMirroredCenters(centerX, centerY))
        {
            for (var y = mirrorY - halfLow; y <= mirrorY + halfHigh; y++)
            {
                for (var x = mirrorX - halfLow; x <= mirrorX + halfHigh; x++)
                {
                    if (x < 0 || y < 0 || x >= _editorWidth || y >= _editorHeight)
                    {
                        continue;
                    }

                    ApplyColorToPixel((y * _editorWidth) + x, color);
                }
            }
        }
    }

    private void ApplyColorToPixel(int index, Rgba32 color)
    {
        var activePixels = GetActiveLayerPixels();
        if (activePixels is null || index < 0 || index >= activePixels.Length)
        {
            return;
        }

        if (activePixels[index].Equals(color))
        {
            return;
        }

        if (!IsEditorDirty)
        {
            IsPlaybackEnabled = false;
        }

        activePixels[index] = color;
        _editorPixels[index] = CompositePixelAt(index);
        var hexColor = ToHexColor(_editorPixels[index]);
        var pixel = EditorPixels[index];
        pixel.HexColor = hexColor;
        pixel.Brush = CreateBrush(_editorPixels[index]);
        IsEditorDirty = true;
        RefreshEditorPalette();
        RefreshEditorComparisonState();
    }

    private void FloodFillMirrored(int startIndex, Rgba32 replacementColor)
    {
        var activePixels = GetActiveLayerPixels();
        if (activePixels is null || startIndex < 0 || startIndex >= activePixels.Length)
        {
            return;
        }

        if (!IsEditorDirty)
        {
            IsPlaybackEnabled = false;
        }

        var originalPixels = (Rgba32[])activePixels.Clone();
        var fillPixels = (Rgba32[])activePixels.Clone();
        var visited = new bool[activePixels.Length];
        foreach (var mirroredIndex in EnumerateMirroredIndices(startIndex))
        {
            var targetColor = originalPixels[mirroredIndex];
            if (targetColor.Equals(replacementColor))
            {
                continue;
            }

            var queue = new Queue<int>();
            queue.Enqueue(mirroredIndex);
            visited[mirroredIndex] = true;

            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                if (!originalPixels[index].Equals(targetColor))
                {
                    continue;
                }

                fillPixels[index] = replacementColor;

                var x = index % _editorWidth;
                var y = index / _editorWidth;

                EnqueueIfMatch(x - 1, y, targetColor, queue);
                EnqueueIfMatch(x + 1, y, targetColor, queue);
                EnqueueIfMatch(x, y - 1, targetColor, queue);
                EnqueueIfMatch(x, y + 1, targetColor, queue);
            }
        }

        _editorLayerPixels[_activeEditorLayerId] = fillPixels;
        RefreshEditorComposite();
        IsEditorDirty = true;
        RefreshEditorPalette();
        RefreshEditorPixels();
        RefreshEditorComparisonState();
        return;

        void EnqueueIfMatch(int x, int y, Rgba32 targetColor, Queue<int> queue)
        {
            if (x < 0 || y < 0 || x >= _editorWidth || y >= _editorHeight)
            {
                return;
            }

            var index = (y * _editorWidth) + x;
            if (visited[index])
            {
                return;
            }

            visited[index] = true;
            if (originalPixels[index].Equals(targetColor))
            {
                queue.Enqueue(index);
            }
        }
    }

    private IEnumerable<int> EnumerateMirroredIndices(int index)
    {
        var x = index % _editorWidth;
        var y = index / _editorWidth;
        foreach (var (mirrorX, mirrorY) in EnumerateMirroredCenters(x, y))
        {
            yield return (mirrorY * _editorWidth) + mirrorX;
        }
    }

    private IEnumerable<(int X, int Y)> EnumerateMirroredCenters(int x, int y)
    {
        var points = new HashSet<(int X, int Y)> { (x, y) };
        if (EditorMirrorHorizontal)
        {
            points.Add(((_editorWidth - 1) - x, y));
        }

        if (EditorMirrorVertical)
        {
            points.Add((x, (_editorHeight - 1) - y));
        }

        if (EditorMirrorHorizontal && EditorMirrorVertical)
        {
            points.Add(((_editorWidth - 1) - x, (_editorHeight - 1) - y));
        }

        return points;
    }

    private void RefreshEditorPixels()
    {
        EditorPixels.Clear();
        for (var index = 0; index < _editorPixels.Length; index++)
        {
            var x = _editorWidth == 0 ? 0 : index % _editorWidth;
            var y = _editorWidth == 0 ? 0 : index / _editorWidth;
            var color = _editorPixels[index];
            EditorPixels.Add(new EditorPixelItemViewModel(index, x, y, ToHexColor(color), CreateBrush(color), _selectedEditorIndices.Contains(index)));
        }
    }

    private void InitializeEditorLayers(Rgba32[] basePixels)
    {
        foreach (var layer in EditorLayers.ToList())
        {
            layer.ThumbnailBitmap = null;
            DetachLayerHandlers(layer);
        }

        _editorLayerPixels.Clear();
        EditorLayers.Clear();
        _nextEditorLayerId = 1;

        CreateEditorLayer("Base Layer", (Rgba32[])basePixels.Clone(), isVisible: true, selectLayer: true, isLocked: true);
        CreateEditorLayer("Overlay Layer", new Rgba32[basePixels.Length], isVisible: true, selectLayer: false, isLocked: false);
        RefreshEditorComposite();
        UpdateLayerSelection();
        OnPropertyChanged(nameof(EditorLayerSummary));
        OnPropertyChanged(nameof(ActiveEditorLayerSummary));
        OnPropertyChanged(nameof(EditorLayerWorkflowHint));
    }

    private EditorLayerItemViewModel CreateEditorLayer(string name, Rgba32[] pixels, bool isVisible, bool selectLayer, bool isLocked)
    {
        var layerId = _nextEditorLayerId++;
        var layer = new EditorLayerItemViewModel(layerId, $"{name} {layerId}", isVisible, false, 100, isLocked);
        EditorLayers.Insert(0, layer);
        _editorLayerPixels[layerId] = pixels;
        AttachLayerHandlers(layer);
        if (selectLayer)
        {
            _activeEditorLayerId = layerId;
        }

        UpdateLayerSelection();
        OnPropertyChanged(nameof(EditorLayerSummary));
        OnPropertyChanged(nameof(ActiveEditorLayerSummary));
        OnPropertyChanged(nameof(EditorLayerWorkflowHint));
        return layer;
    }

    private void AttachLayerHandlers(EditorLayerItemViewModel layer)
    {
        layer.PropertyChanged -= OnEditorLayerChanged;
        layer.PropertyChanged += OnEditorLayerChanged;
    }

    private void DetachLayerHandlers(EditorLayerItemViewModel layer)
    {
        layer.PropertyChanged -= OnEditorLayerChanged;
    }

    private void OnEditorLayerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorLayerItemViewModel.IsVisible) or nameof(EditorLayerItemViewModel.OpacityPercent))
        {
            RefreshEditorComposite();
            RefreshEditorPixels();
            RefreshEditorComparisonState();
        }

        if (e.PropertyName is nameof(EditorLayerItemViewModel.Name) or
            nameof(EditorLayerItemViewModel.IsVisible) or
            nameof(EditorLayerItemViewModel.IsSelected) or
            nameof(EditorLayerItemViewModel.IsLocked) or
            nameof(EditorLayerItemViewModel.OpacityPercent))
        {
            OnPropertyChanged(nameof(EditorLayerSummary));
            OnPropertyChanged(nameof(ActiveEditorLayerSummary));
            OnPropertyChanged(nameof(EditorLayerWorkflowHint));
        }
    }

    private void MoveEditorLayer(EditorLayerItemViewModel? layer, bool towardTop)
    {
        if (layer is null)
        {
            return;
        }

        var currentIndex = EditorLayers.IndexOf(layer);
        if (currentIndex < 0)
        {
            return;
        }

        var targetIndex = towardTop ? Math.Max(0, currentIndex - 1) : Math.Min(EditorLayers.Count - 1, currentIndex + 1);
        if (targetIndex == currentIndex)
        {
            EditorStatusMessage = towardTop
                ? $"{layer.Name} is already at the top."
                : $"{layer.Name} is already at the bottom.";
            return;
        }

        EditorLayers.Move(currentIndex, targetIndex);
        RefreshEditorComposite();
        RefreshEditorPixels();
        RefreshEditorComparisonState();
        OnPropertyChanged(nameof(EditorLayerSummary));
        EditorStatusMessage = towardTop
            ? $"Moved {layer.Name} higher in the stack."
            : $"Moved {layer.Name} lower in the stack.";
    }

    private void UpdateLayerSelection()
    {
        foreach (var layer in EditorLayers)
        {
            layer.IsSelected = layer.LayerId == _activeEditorLayerId;
        }

        OnPropertyChanged(nameof(ActiveEditorLayerSummary));
        OnPropertyChanged(nameof(EditorLayerWorkflowHint));
    }

    private EditorLayerItemViewModel? GetActiveLayerItem() => EditorLayers.FirstOrDefault(layer => layer.LayerId == _activeEditorLayerId);

    private bool CanEditActiveLayer()
    {
        var activeLayer = GetActiveLayerItem();
        if (activeLayer is null)
        {
            EditorStatusMessage = "No active layer is selected.";
            return false;
        }

        if (activeLayer.IsLocked)
        {
            EditorStatusMessage = $"{activeLayer.Name} is locked. Unlock it before editing.";
            return false;
        }

        return true;
    }

    private Rgba32[]? GetActiveLayerPixels()
    {
        return _editorLayerPixels.TryGetValue(_activeEditorLayerId, out var pixels) ? pixels : null;
    }

    private static Rgba32[] CopyPixels(Image<Rgba32> image)
    {
        var pixels = new Rgba32[image.Width * image.Height];
        image.CopyPixelDataTo(pixels);
        return pixels;
    }

    private Rgba32[] ResizePixelsToEditorCanvas(Image<Rgba32> image)
    {
        using var resized = image.Clone(context => context.Resize(_editorWidth, _editorHeight, KnownResamplers.NearestNeighbor));
        return CopyPixels(resized);
    }

    private void RefreshEditorComposite()
    {
        if (_editorWidth <= 0 || _editorHeight <= 0)
        {
            _editorPixels = [];
            RefreshEditorLayerStats();
            return;
        }

        _editorPixels = new Rgba32[_editorWidth * _editorHeight];
        for (var index = 0; index < _editorPixels.Length; index++)
        {
            _editorPixels[index] = CompositePixelAt(index);
        }

        RefreshEditorLayerStats();
    }

    private void RefreshEditorLayerStats()
    {
        foreach (var layer in EditorLayers)
        {
            if (!_editorLayerPixels.TryGetValue(layer.LayerId, out var pixels))
            {
                layer.FilledPixelCount = 0;
                SetLayerThumbnail(layer, null);
                continue;
            }

            var filledCount = 0;
            for (var index = 0; index < pixels.Length; index++)
            {
                if (pixels[index].A > 0)
                {
                    filledCount++;
                }
            }

            layer.FilledPixelCount = filledCount;
            SetLayerThumbnail(layer, filledCount == 0 ? null : CreateBitmapFromPixels(pixels));
        }
    }

    private static void SetLayerThumbnail(EditorLayerItemViewModel layer, Bitmap? bitmap)
    {
        layer.ThumbnailBitmap = bitmap;
    }

    private Rgba32 CompositePixelAt(int index)
    {
        var composite = new Rgba32(0, 0, 0, 0);
        foreach (var layer in EditorLayers.Reverse())
        {
            if (!layer.IsVisible || !_editorLayerPixels.TryGetValue(layer.LayerId, out var pixels) || index >= pixels.Length)
            {
                continue;
            }

            composite = AlphaBlend(composite, ApplyLayerOpacity(pixels[index], layer.OpacityPercent));
        }

        return composite;
    }

    private static Rgba32 ApplyLayerOpacity(Rgba32 color, int opacityPercent)
    {
        if (opacityPercent >= 100)
        {
            return color;
        }

        if (opacityPercent <= 0 || color.A == 0)
        {
            return new Rgba32(0, 0, 0, 0);
        }

        var scaledAlpha = (byte)Math.Clamp((int)Math.Round(color.A * (opacityPercent / 100.0)), 0, 255);
        return new Rgba32(color.R, color.G, color.B, scaledAlpha);
    }

    private static Rgba32 AlphaBlend(Rgba32 bottom, Rgba32 top)
    {
        var topAlpha = top.A / 255f;
        var bottomAlpha = bottom.A / 255f;
        var outAlpha = topAlpha + (bottomAlpha * (1f - topAlpha));
        if (outAlpha <= 0f)
        {
            return new Rgba32(0, 0, 0, 0);
        }

        byte Blend(byte bottomChannel, byte topChannel)
        {
            var value = ((topChannel * topAlpha) + (bottomChannel * bottomAlpha * (1f - topAlpha))) / outAlpha;
            return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
        }

        return new Rgba32(
            Blend(bottom.R, top.R),
            Blend(bottom.G, top.G),
            Blend(bottom.B, top.B),
            (byte)Math.Clamp((int)Math.Round(outAlpha * 255f), 0, 255));
    }

    private void RefreshEditorPalette()
    {
        var palette = _editorPixels
            .Select(ToHexColor)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(color => color, StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToList();

        if (!palette.Contains(SelectedEditorColorHex, StringComparer.OrdinalIgnoreCase))
        {
            palette.Insert(0, SelectedEditorColorHex);
        }

        EditorPalette.Clear();
        foreach (var color in palette.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var parsedColor = TryParseHexColor(color, out var rgba) ? rgba : new Rgba32(255, 255, 255, 255);
            EditorPalette.Add(new EditorPaletteColorItemViewModel(color, CreateBrush(parsedColor), color.Equals(SelectedEditorColorHex, StringComparison.OrdinalIgnoreCase)));
        }

        UpdatePaletteSelection();
    }

    private void UpdatePaletteSelection()
    {
        foreach (var color in EditorPalette)
        {
            color.IsSelected = color.HexColor.Equals(SelectedEditorColorHex, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var color in SavedEditorPalette)
        {
            color.IsSelected = color.HexColor.Equals(SelectedEditorColorHex, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void ClearEditor(string message)
    {
        IsEditorBlinkCompareEnabled = false;
        foreach (var layer in EditorLayers.ToList())
        {
            layer.ThumbnailBitmap = null;
            DetachLayerHandlers(layer);
        }

        _editorPixels = [];
        _editorBaselinePixels = [];
        _editorLayerPixels.Clear();
        EditorLayers.Clear();
        _nextEditorLayerId = 1;
        _activeEditorLayerId = 0;
        _loadedEditorFramePath = string.Empty;
        _editorSaveFramePath = string.Empty;
        _editorWidth = 0;
        _editorHeight = 0;
        _undoHistory.Clear();
        _redoHistory.Clear();
        _strokeSnapshotCaptured = false;
        _selectedEditorIndices.Clear();
        _editorClipboard = null;
        _selectionAnchorIndex = null;
        _moveAnchorIndex = null;
        _movePreviewBasePixels = null;
        _movePreviewPixels = null;
        _shapePreviewBasePixels = null;
        EditorLoadedFramePath = string.Empty;
        EditorPreviewBitmap = null;
        EditorBaselineBitmap = null;
        EditorDiffBitmap = null;
        EditorPixels.Clear();
        FrameHistoryItems.Clear();
        EditorPalette.Clear();
        IsEditorDirty = false;
        FrameHistoryMessage = "Frame history appears here after you start saving edits.";
        EditorStatusMessage = message;
        OnPropertyChanged(nameof(EditorCanvasSummary));
        OnPropertyChanged(nameof(EditorCanvasWidth));
        OnPropertyChanged(nameof(EditorCanvasHeight));
        OnPropertyChanged(nameof(EditorPixelSize));
        OnPropertyChanged(nameof(EditorTemplateSummary));
        OnPropertyChanged(nameof(EditorSelectedColorBrush));
        OnPropertyChanged(nameof(EditorSelectionSummary));
        OnPropertyChanged(nameof(EditorSelectionBoundsSummary));
        OnPropertyChanged(nameof(EditorSelectionTransformHint));
        OnPropertyChanged(nameof(IsEditorFrameLoaded));
            OnPropertyChanged(nameof(IsEditorFrameMissing));
            OnPropertyChanged(nameof(EditorComparisonSummary));
            OnPropertyChanged(nameof(EditorBlinkCompareSummary));
            OnPropertyChanged(nameof(EditorBlinkCompareBitmap));
            OnPropertyChanged(nameof(HasEditorBaselineBitmap));
            OnPropertyChanged(nameof(IsEditorBaselineBitmapMissing));
            OnPropertyChanged(nameof(HasEditorDiffBitmap));
        OnPropertyChanged(nameof(IsEditorDiffBitmapMissing));
        OnPropertyChanged(nameof(SavedPaletteSummary));
        OnPropertyChanged(nameof(EditorLayerSummary));
        OnPropertyChanged(nameof(ActiveEditorLayerSummary));
        OnPropertyChanged(nameof(EditorLayerWorkflowHint));
        OnPropertyChanged(nameof(FrameHistorySummary));
        NotifyEditorHistoryChanged();
        NotifyLoopStateChanged();
        PersistWorkspaceState();
    }

    private void RefreshFrameHistory()
    {
        FrameHistoryItems.Clear();

        if (string.IsNullOrWhiteSpace(_editorSaveFramePath) || string.IsNullOrWhiteSpace(_frameHistoryDirectory))
        {
            FrameHistoryMessage = "Frame history appears here after you start saving edits.";
            OnPropertyChanged(nameof(FrameHistorySummary));
            return;
        }

        var historyDirectory = GetFrameHistoryDirectory(_editorSaveFramePath);
        if (!Directory.Exists(historyDirectory))
        {
            FrameHistoryMessage = "No saved history snapshots exist for the current frame yet.";
            OnPropertyChanged(nameof(FrameHistorySummary));
            return;
        }

        var items = Directory.EnumerateFiles(historyDirectory, "*.png", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.CreationTimeUtc)
            .Select(info => new FrameHistoryItemViewModel(
                Path.GetFileNameWithoutExtension(info.Name),
                info.FullName,
                new DateTimeOffset(info.CreationTimeUtc, TimeSpan.Zero)))
            .ToList();

        foreach (var item in items)
        {
            FrameHistoryItems.Add(item);
        }

        FrameHistoryMessage = items.Count == 0
            ? "No saved history snapshots exist for the current frame yet."
            : $"Loaded {items.Count} history snapshot(s) for {Path.GetFileName(_editorSaveFramePath)}.";
        OnPropertyChanged(nameof(FrameHistorySummary));
    }

    private void CreateFrameHistorySnapshot(string sourcePath, string label)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) ||
            string.IsNullOrWhiteSpace(_frameHistoryDirectory) ||
            !File.Exists(sourcePath))
        {
            return;
        }

        var historyDirectory = GetFrameHistoryDirectory(sourcePath);
        Directory.CreateDirectory(historyDirectory);
        var snapshotPath = Path.Combine(historyDirectory, $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{label}.png");
        File.Copy(sourcePath, snapshotPath, true);
    }

    private string GetFrameHistoryDirectory(string framePath)
    {
        var relativePath = GetTrustedExportRelativePath(framePath)
            .Replace(Path.DirectorySeparatorChar, '-')
            .Replace(Path.AltDirectorySeparatorChar, '-');
        var directoryName = Path.GetFileNameWithoutExtension(relativePath);
        return Path.Combine(_frameHistoryDirectory, directoryName);
    }

    private void NotifyPlanningStateChanged()
    {
        RebuildPlanningChecklist();
        OnPropertyChanged(nameof(PlanningProjectModeSummary));
        OnPropertyChanged(nameof(PlanningDiscoverySummary));
        OnPropertyChanged(nameof(PlanningAdoptionSummary));
        OnPropertyChanged(nameof(PlanningGapSummary));
        OnPropertyChanged(nameof(PlanningAxisSummary));
        OnPropertyChanged(nameof(PlanningChecklistSummary));
        OnPropertyChanged(nameof(PlanningProjectExportSummary));
        OnPropertyChanged(nameof(PlanningAnimationSummary));
        OnPropertyChanged(nameof(PlanningStarterHint));
        OnPropertyChanged(nameof(PlanningBaseRowCount));
        OnPropertyChanged(nameof(PlanningVariantTargetCount));
        OnPropertyChanged(nameof(PlanningSequenceTargetCount));
        OnPropertyChanged(nameof(PlanningBaseFrameTargetCount));
        RefreshPlanningDiagnostics();
        PersistWorkspaceState();
    }

    private void NotifyTrustedStateChanged()
    {
        OnPropertyChanged(nameof(TrustedExportSummary));
        OnPropertyChanged(nameof(TrustedExportHint));
        OnPropertyChanged(nameof(TrustedExportBlockerSummary));
        RefreshTrustedExportBlockers();
        RefreshPlanningDiagnostics();
        RefreshProjectReadiness();
    }

    private void RefreshPlanningDiagnostics()
    {
        PlanningDiagnostics.Clear();
        PlanningAdoptionEntries.Clear();
        PlanningDiscoveryCategories.Clear();
        PlanningUnmappedEntries.Clear();

        var plannedSpecies = ParsePlanningList(PlanningSpeciesText);
        var plannedAges = ParsePlanningList(PlanningAgeText);
        var plannedGenders = ParsePlanningList(PlanningGenderText);
        var plannedColors = ParsePlanningList(PlanningColorText);

        var discoveredSpecies = _allBaseVariants.Select(row => row.Species).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList();
        var discoveredAges = _allBaseVariants.Select(row => row.Age).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList();
        var discoveredGenders = _allBaseVariants.Select(row => row.Gender).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList();
        var discoveredColors = ViewerColorOptions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList();

        PlanningDiagnostics.Add(new PlanningDiagnosticItemViewModel("Axes In Plan", PlanningAxisSummary));
        PlanningDiagnostics.Add(new PlanningDiagnosticItemViewModel("Axes Discovered", PlanningAdoptionSummary));
        PlanningDiagnostics.Add(new PlanningDiagnosticItemViewModel(
            "Species Delta",
            BuildAxisDeltaSummary("Missing", plannedSpecies.Except(discoveredSpecies, StringComparer.OrdinalIgnoreCase), "Extra", discoveredSpecies.Except(plannedSpecies, StringComparer.OrdinalIgnoreCase))));
        PlanningDiagnostics.Add(new PlanningDiagnosticItemViewModel(
            "Age / Gender Delta",
            $"{BuildAxisDeltaSummary("Missing ages", plannedAges.Except(discoveredAges, StringComparer.OrdinalIgnoreCase), "Extra ages", discoveredAges.Except(plannedAges, StringComparer.OrdinalIgnoreCase))}  |  {BuildAxisDeltaSummary("Missing genders", plannedGenders.Except(discoveredGenders, StringComparer.OrdinalIgnoreCase), "Extra genders", discoveredGenders.Except(plannedGenders, StringComparer.OrdinalIgnoreCase))}"));
        PlanningDiagnostics.Add(new PlanningDiagnosticItemViewModel(
            "Color Delta",
            BuildAxisDeltaSummary("Missing", plannedColors.Except(discoveredColors, StringComparer.OrdinalIgnoreCase), "Extra", discoveredColors.Except(plannedColors, StringComparer.OrdinalIgnoreCase))));
        PlanningDiagnostics.Add(new PlanningDiagnosticItemViewModel("Coverage Gap", PlanningGapSummary));
        PlanningDiagnostics.Add(new PlanningDiagnosticItemViewModel(
            "Review State",
            $"{TrustedExportSummary}  |  Needs row review: {_allBaseVariants.Count(row => !row.ReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase))}  |  Flagged frame reviews: {FrameReviewQueueItems.Count}"));

        if (TryBuildPlanningProjectConfig(out var config, out _, out _))
        {
            var authoredRoot = ResolvePlanningWorkspacePath(config.RootPath, config.AuthoredSpriteRoot);
            var plannedRelativeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in BuildPlanningAssetChecklistEntries(config, authoredRoot)
                         .OrderBy(item => item.Species)
                         .ThenBy(item => item.Age)
                         .ThenBy(item => item.Gender)
                         .ThenBy(item => item.Color)
                         .ThenBy(item => item.Family)
                         .ThenBy(item => item.SequenceId)
                         .Take(120))
            {
                var existingFiles = Directory.Exists(entry.VariantDirectory)
                    ? Directory.EnumerateFiles(entry.VariantDirectory, "*.png", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).Where(name => !string.IsNullOrWhiteSpace(name)).Cast<string>().ToList()
                    : [];
                foreach (var plannedFile in entry.FrameFiles)
                {
                    plannedRelativeFiles.Add(Path.Combine(entry.Species, entry.Age, entry.Gender, entry.Color, plannedFile));
                }

                var existingPlanned = existingFiles.Count(file => entry.FrameFiles.Contains(file, StringComparer.OrdinalIgnoreCase));
                var extraFiles = existingFiles.Count(file => !entry.FrameFiles.Contains(file, StringComparer.OrdinalIgnoreCase));
                PlanningAdoptionEntries.Add(new PlanningAdoptionEntryItemViewModel(
                    $"{entry.Species} | {entry.Age} | {entry.Gender} | {entry.Color}",
                    $"{entry.Family} / {entry.SequenceId}",
                    entry.FrameFiles.Count,
                    existingPlanned,
                    extraFiles));
            }

            if (Directory.Exists(authoredRoot))
            {
                foreach (var filePath in Directory.EnumerateFiles(authoredRoot, "*.png", SearchOption.AllDirectories)
                             .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                             .Take(400))
                {
                    var relativePath = Path.GetRelativePath(authoredRoot, filePath);
                    if (plannedRelativeFiles.Contains(relativePath))
                    {
                        continue;
                    }

                    var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var speciesValue = parts.Length > 0 ? parts[0] : string.Empty;
                    var ageValue = parts.Length > 1 ? parts[1] : string.Empty;
                    var genderValue = parts.Length > 2 ? parts[2] : string.Empty;
                    var colorValue = parts.Length > 3 ? parts[3] : string.Empty;
                    var frameName = Path.GetFileNameWithoutExtension(filePath);
                    var underscoreIndex = frameName.LastIndexOf('_');
                    var sequenceId = underscoreIndex > 0 ? frameName[..underscoreIndex] : frameName;
                    var family = _familySequences
                        .FirstOrDefault(pair => pair.Value.Any(sequence => sequence.SequenceId.Equals(sequenceId, StringComparison.OrdinalIgnoreCase)))
                        .Key ?? string.Empty;
                    var reason = string.IsNullOrWhiteSpace(family)
                        ? "Frame sequence is not in the current planning blueprint."
                        : "Discovered frame exists outside the current planned checklist.";

                    PlanningUnmappedEntries.Add(new PlanningUnmappedEntryItemViewModel(
                        relativePath,
                        speciesValue,
                        ageValue,
                        genderValue,
                        colorValue,
                        family,
                        sequenceId,
                        reason));
                }
            }
        }

        var mappedCount = PlanningAdoptionEntries.Count(entry => entry.StatusLabel.Equals("Complete", StringComparison.OrdinalIgnoreCase));
        var conflictingCount = PlanningAdoptionEntries.Count(entry =>
            entry.ExtraFrames > 0 ||
            (entry.ExistingFrames > 0 && entry.ExistingFrames < entry.ExpectedFrames));
        var extraCount = PlanningAdoptionEntries.Count(entry => entry.ExtraFrames > 0);
        var unmappedCount = PlanningUnmappedEntries.Count;

        PlanningDiscoveryCategories.Add(new PlanningDiscoveryCategoryItemViewModel(
            "Mapped",
            mappedCount,
            "Discovered sequences fully align with the current planning checklist."));
        PlanningDiscoveryCategories.Add(new PlanningDiscoveryCategoryItemViewModel(
            "Unmapped",
            unmappedCount,
            "Discovered PNGs are visible on disk but do not currently belong to the planned checklist."));
        PlanningDiscoveryCategories.Add(new PlanningDiscoveryCategoryItemViewModel(
            "Conflicting",
            conflictingCount,
            "These sequences exist but do not match the planned frame contract cleanly yet."));
        PlanningDiscoveryCategories.Add(new PlanningDiscoveryCategoryItemViewModel(
            "Extra",
            extraCount,
            "These planned sequence targets contain extra discovered frame files beyond the expected count."));

        RefreshProjectReadiness();
        OnPropertyChanged(nameof(PlanningAdoptionEntrySummary));
        OnPropertyChanged(nameof(PlanningDiscoveryCategorySummary));
        OnPropertyChanged(nameof(PlanningUnmappedEntrySummary));
    }

    private void RefreshProjectReadiness()
    {
        ProjectReadinessItems.Clear();
        var trustedSnapshot = BuildTrustedExportSnapshot();
        var hasDefaultProvider = AiProviders.Any(provider => provider.IsDefault);
        var hasValidationReport = ValidationReports.Count > 0;
        var hasTrustedExportHistory = TrustedExportHistoryItems.Count > 0;
        var discoveryNeedsAttention = PlanningUnmappedEntries.Count > 0 ||
                                      PlanningAdoptionEntries.Any(entry =>
                                          !entry.StatusLabel.Equals("Complete", StringComparison.OrdinalIgnoreCase));

        ProjectReadinessItems.Add(new ProjectReadinessItemViewModel(
            "AI Provider Setup",
            hasDefaultProvider ? "ready" : "needs_attention",
            hasDefaultProvider
                ? DefaultAiProviderSummary
                : "Add at least one provider adapter so requests, attempts, and candidates stay provider-agnostic."));
        ProjectReadinessItems.Add(new ProjectReadinessItemViewModel(
            "Review Approval",
            trustedSnapshot.FlaggedFrameCount == 0 && trustedSnapshot.ApprovedFrameCount > 0 ? "ready" : "needs_attention",
            TrustedExportSummary));
        ProjectReadinessItems.Add(new ProjectReadinessItemViewModel(
            "Planning Adoption",
            discoveryNeedsAttention ? "needs_attention" : "ready",
            PlanningDiscoveryCategorySummary));
        ProjectReadinessItems.Add(new ProjectReadinessItemViewModel(
            "Validation",
            hasValidationReport ? "ready" : "needs_attention",
            hasValidationReport
                ? SelectedValidationReportSummary
                : "Run validation to produce a reusable portability and project-health report."));
        ProjectReadinessItems.Add(new ProjectReadinessItemViewModel(
            "Export Readiness",
            hasTrustedExportHistory ? "ready" : "needs_attention",
            hasTrustedExportHistory
                ? TrustedExportHistorySummary
                : "Create a trusted export bundle once the approved set is ready to ship or hand off."));

        OnPropertyChanged(nameof(ProjectReadinessSummary));
    }

    private static string BuildAxisDeltaSummary(string missingLabel, IEnumerable<string> missingValues, string extraLabel, IEnumerable<string> extraValues)
    {
        var missingList = missingValues.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var extraList = extraValues.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var missingSummary = missingList.Count == 0 ? $"{missingLabel}: none" : $"{missingLabel}: {string.Join(", ", missingList)}";
        var extraSummary = extraList.Count == 0 ? $"{extraLabel}: none" : $"{extraLabel}: {string.Join(", ", extraList)}";
        return $"{missingSummary}  |  {extraSummary}";
    }

    private void RefreshTrustedExportBlockers()
    {
        TrustedExportBlockers.Clear();

        foreach (var row in _allBaseVariants
                     .Where(item => !item.ReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(item => item.Species)
                     .ThenBy(item => item.Age)
                     .ThenBy(item => item.Gender))
        {
            TrustedExportBlockers.Add(new TrustedExportBlockerItemViewModel(
                "row",
                row.Species,
                row.Age,
                row.Gender,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                string.Empty,
                $"Row review is {row.ReviewStatusLabel}."));
        }

        foreach (var frame in FrameReviewQueueItems
                     .OrderBy(item => item.Species)
                     .ThenBy(item => item.Age)
                     .ThenBy(item => item.Gender)
                     .ThenBy(item => item.Color)
                     .ThenBy(item => item.Family)
                     .ThenBy(item => item.SequenceId)
                     .ThenBy(item => item.FrameIndex))
        {
            TrustedExportBlockers.Add(new TrustedExportBlockerItemViewModel(
                "frame",
                frame.Species,
                frame.Age,
                frame.Gender,
                frame.Color,
                frame.Family,
                frame.SequenceId,
                frame.FrameIndex,
                frame.FrameId,
                $"{frame.StatusLabel}: {frame.NotePreview}"));
        }

        SelectedTrustedExportBlockerItem = TrustedExportBlockers.FirstOrDefault(item =>
            item.DisplayName.Equals(SelectedTrustedExportBlockerItem?.DisplayName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            ?? TrustedExportBlockers.FirstOrDefault();

        OnPropertyChanged(nameof(TrustedExportBlockerListSummary));
    }

    private void NavigateToTrustedExportBlocker(TrustedExportBlockerItemViewModel blocker)
    {
        var variant = _allBaseVariants.FirstOrDefault(
            row => row.Species.Equals(blocker.Species, StringComparison.OrdinalIgnoreCase) &&
                   row.Age.Equals(blocker.Age, StringComparison.OrdinalIgnoreCase) &&
                   row.Gender.Equals(blocker.Gender, StringComparison.OrdinalIgnoreCase));
        if (variant is null)
        {
            TrustedExportMessage = "Could not find the blocker row in the current project.";
            return;
        }

        SelectedWorkspaceTabIndex = 2;
        SelectedBaseVariant = variant;

        if (!string.IsNullOrWhiteSpace(blocker.Color) && ViewerColorOptions.Contains(blocker.Color))
        {
            SelectedViewerColor = blocker.Color;
        }

        if (!string.IsNullOrWhiteSpace(blocker.Family) && ViewerFamilyOptions.Contains(blocker.Family))
        {
            SelectedViewerFamily = blocker.Family;
        }

        if (!string.IsNullOrWhiteSpace(blocker.SequenceId) && ViewerSequenceOptions.Contains(blocker.SequenceId))
        {
            SelectedViewerSequenceId = blocker.SequenceId;
        }

        if (blocker.FrameIndex is int frameIndex)
        {
            _currentFrameIndex = frameIndex;
            _playbackFrameIndex = frameIndex;
            UpdateViewer();
        }

        TrustedExportMessage = $"Jumped to blocker {blocker.DisplayName}.";
        AddActivity("review", TrustedExportMessage);
    }

    private void RebuildPlanningChecklist()
    {
        PlanningChecklist.Clear();

        foreach (var (family, sequences) in ParsePlanningBlueprint(PlanningFamilyBlueprintText))
        {
            if (string.IsNullOrWhiteSpace(family))
            {
                continue;
            }

            var sequenceSummary = sequences.Count == 0
                ? "No sequences yet"
                : string.Join(", ", sequences.Select(sequence => $"{sequence.SequenceId} x{sequence.FrameCount}"));

            PlanningChecklist.Add(new PlanningChecklistItemViewModel(
                family,
                sequenceSummary,
                sequences.Count,
                sequences.Sum(sequence => sequence.FrameCount),
                PlanningBaseRowCount,
                PlanningVariantTargetCount));
        }
    }

    private static string BuildFamilyBlueprintText(IReadOnlyDictionary<string, AnimationSequenceConfig[]> families)
    {
        if (families.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            families
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}: {string.Join(", ", pair.Value.Select(sequence => $"{sequence.SequenceId} x{sequence.FrameCount}"))}"));
    }

    private static string BuildFamilyBlueprintText(IEnumerable<(string Family, List<(string SequenceId, int FrameCount)> Sequences)> blueprint)
    {
        return string.Join(
            Environment.NewLine,
            blueprint
                .Where(item => !string.IsNullOrWhiteSpace(item.Family))
                .OrderBy(item => item.Family, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.Family}: {string.Join(", ", item.Sequences.OrderBy(sequence => sequence.SequenceId, StringComparer.OrdinalIgnoreCase).Select(sequence => $"{sequence.SequenceId} x{Math.Max(1, sequence.FrameCount)}"))}"));
    }

    private int DiscoverFrameCountForSequence(string relativePath, string sequenceId)
    {
        if (string.IsNullOrWhiteSpace(_authoredSpriteRoot) || string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(sequenceId))
        {
            return 1;
        }

        var fullPath = Path.Combine(_authoredSpriteRoot, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return 1;
        }

        var matchingIndices = Directory.EnumerateFiles(directory, "*.png", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name =>
            {
                var underscoreIndex = name!.LastIndexOf('_');
                if (underscoreIndex <= 0)
                {
                    return (Matches: false, Index: -1);
                }

                var currentSequence = name[..underscoreIndex];
                return currentSequence.Equals(sequenceId, StringComparison.OrdinalIgnoreCase) && int.TryParse(name[(underscoreIndex + 1)..], out var frameIndex)
                    ? (Matches: true, Index: frameIndex)
                    : (Matches: false, Index: -1);
            })
            .Where(item => item.Matches)
            .Select(item => item.Index)
            .ToList();

        return matchingIndices.Count == 0 ? 1 : matchingIndices.Max() + 1;
    }

    private DiscoveredPlanningBlueprint DiscoverPlanningBlueprintFromAuthoredAssets()
    {
        var species = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var genders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var colors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var familyMap = _familyOrder.ToDictionary(family => family, _ => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(_authoredSpriteRoot) || !Directory.Exists(_authoredSpriteRoot))
        {
            return new DiscoveredPlanningBlueprint([], [], [], [], string.Empty);
        }

        foreach (var filePath in Directory.EnumerateFiles(_authoredSpriteRoot, "*.png", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(_authoredSpriteRoot, filePath);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Length < 5)
            {
                continue;
            }

            species.Add(parts[0]);
            ages.Add(parts[1]);
            genders.Add(parts[2]);
            colors.Add(parts[3]);

            var frameName = Path.GetFileNameWithoutExtension(parts[^1]);
            var underscoreIndex = frameName.LastIndexOf('_');
            if (underscoreIndex <= 0 || !int.TryParse(frameName[(underscoreIndex + 1)..], out var frameIndex))
            {
                continue;
            }

            var sequenceId = frameName[..underscoreIndex];
            foreach (var family in _familyOrder)
            {
                if (!_familySequences.TryGetValue(family, out var sequences))
                {
                    continue;
                }

                var matchingSequence = sequences.FirstOrDefault(sequence => sequence.SequenceId.Equals(sequenceId, StringComparison.OrdinalIgnoreCase));
                if (matchingSequence is null)
                {
                    continue;
                }

                if (!familyMap.TryGetValue(family, out var sequenceMap))
                {
                    sequenceMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    familyMap[family] = sequenceMap;
                }

                sequenceMap[matchingSequence.SequenceId] = Math.Max(sequenceMap.GetValueOrDefault(matchingSequence.SequenceId), frameIndex + 1);
                break;
            }
        }

        var blueprintLines = familyMap
            .Where(pair => pair.Value.Count > 0)
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key}: {string.Join(", ", pair.Value.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Select(item => $"{item.Key} x{item.Value}"))}")
            .ToList();

        return new DiscoveredPlanningBlueprint(
            species.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            ages.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            genders.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            colors.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
            string.Join(Environment.NewLine, blueprintLines));
    }

    private static List<string> ParsePlanningList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split([',', '\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<(string Family, List<(string SequenceId, int FrameCount)> Sequences)> ParsePlanningBlueprint(string? blueprintText)
    {
        var parsed = new List<(string Family, List<(string SequenceId, int FrameCount)> Sequences)>();
        if (string.IsNullOrWhiteSpace(blueprintText))
        {
            return parsed;
        }

        foreach (var rawLine in blueprintText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = rawLine.IndexOf(':');
            var family = separatorIndex >= 0 ? rawLine[..separatorIndex].Trim() : rawLine.Trim();
            if (string.IsNullOrWhiteSpace(family))
            {
                continue;
            }

            var sequenceText = separatorIndex >= 0 ? rawLine[(separatorIndex + 1)..] : string.Empty;
            var sequences = new List<(string SequenceId, int FrameCount)>();

            foreach (var token in sequenceText.Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parsedSequence = ParseSequenceToken(token);
                if (!string.IsNullOrWhiteSpace(parsedSequence.SequenceId))
                {
                    sequences.Add(parsedSequence);
                }
            }

            parsed.Add((family, sequences));
        }

        return parsed;
    }

    private static (string SequenceId, int FrameCount) ParseSequenceToken(string rawToken)
    {
        var token = rawToken.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return (string.Empty, 0);
        }

        var pieces = token.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pieces.Length == 0)
        {
            return (string.Empty, 0);
        }

        var sequenceId = token;
        var frameCount = 1;

        var lastPiece = pieces[^1];
        if (lastPiece.StartsWith('x') && int.TryParse(lastPiece[1..], out var xCount))
        {
            frameCount = xCount;
            sequenceId = string.Join(" ", pieces[..^1]);
        }
        else if (int.TryParse(lastPiece, out var numericCount))
        {
            frameCount = numericCount;
            sequenceId = string.Join(" ", pieces[..^1]);
        }
        else
        {
            var xIndex = token.LastIndexOf('x');
            if (xIndex > 0 && int.TryParse(token[(xIndex + 1)..].Trim(), out var compactCount))
            {
                frameCount = compactCount;
                sequenceId = token[..xIndex].Trim().TrimEnd('-', 'x', 'X');
            }
        }

        return (sequenceId.Trim(), Math.Max(1, frameCount));
    }

    private static string FormatPathDiscovery(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "not configured";
        }

        if (Directory.Exists(path))
        {
            return "found";
        }

        return File.Exists(path) ? "file found" : "missing";
    }

    private void PersistWorkspaceState(bool includeEditorDraft = false)
    {
        if (_isRestoringWorkspaceState || string.IsNullOrWhiteSpace(_workspaceStatePath))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_workspaceStatePath) ?? string.Empty);

            string? editorDraftPath = null;
            if (includeEditorDraft && IsEditorDirty && IsEditorFrameLoaded)
            {
                editorDraftPath = SaveEditorDraftSnapshot();
            }

            var state = new WorkspaceState(
                ControlMode,
                SelectedWorkspaceTabIndex,
                SelectedStudioTabIndex,
                PlanningProjectId,
                PlanningDisplayName,
                PlanningRootPath,
                PlanningExportPath,
                PlanningSpeciesText,
                PlanningAgeText,
                PlanningGenderText,
                PlanningColorText,
                PlanningFamilyBlueprintText,
                SelectedBaseVariant?.Species,
                SelectedBaseVariant?.Age,
                SelectedBaseVariant?.Gender,
                SelectedViewerColor,
                SelectedViewerFamily,
                SelectedViewerSequenceId,
                _currentFrameIndex,
                _editorSaveFramePath,
                editorDraftPath,
                IsEditorDirty);

            File.WriteAllText(
                _workspaceStatePath,
                JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Keep the main workflow running even if persistence fails.
        }
    }

    private void TryRestoreWorkspaceState()
    {
        if (string.IsNullOrWhiteSpace(_workspaceStatePath) || !File.Exists(_workspaceStatePath))
        {
            return;
        }

        try
        {
            var state = JsonSerializer.Deserialize<WorkspaceState>(File.ReadAllText(_workspaceStatePath));
            if (state is null)
            {
                return;
            }

            _isRestoringWorkspaceState = true;

            if (!string.IsNullOrWhiteSpace(state.ControlMode))
            {
                ControlMode = state.ControlMode;
            }

            PlanningSpeciesText = state.PlanningSpeciesText ?? _seedPlanningSpeciesText;
            PlanningAgeText = state.PlanningAgeText ?? _seedPlanningAgeText;
            PlanningGenderText = state.PlanningGenderText ?? _seedPlanningGenderText;
            PlanningColorText = state.PlanningColorText ?? _seedPlanningColorText;
            PlanningFamilyBlueprintText = state.PlanningFamilyBlueprintText ?? _seedPlanningFamilyBlueprintText;
            PlanningProjectId = string.IsNullOrWhiteSpace(state.PlanningProjectId) ? PlanningProjectId : state.PlanningProjectId;
            PlanningDisplayName = string.IsNullOrWhiteSpace(state.PlanningDisplayName) ? PlanningDisplayName : state.PlanningDisplayName;
            PlanningRootPath = string.IsNullOrWhiteSpace(state.PlanningRootPath) ? PlanningRootPath : state.PlanningRootPath;
            PlanningExportPath = string.IsNullOrWhiteSpace(state.PlanningExportPath) ? PlanningExportPath : state.PlanningExportPath;
            RebuildPlanningChecklist();

            var restoredVariant = _allBaseVariants.FirstOrDefault(row =>
                row.Species.Equals(state.Species ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                row.Age.Equals(state.Age ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                row.Gender.Equals(state.Gender ?? string.Empty, StringComparison.OrdinalIgnoreCase));

            if (restoredVariant is not null)
            {
                SelectedBaseVariant = restoredVariant;
            }

            if (!string.IsNullOrWhiteSpace(state.ViewerColor) && ViewerColorOptions.Contains(state.ViewerColor))
            {
                SelectedViewerColor = state.ViewerColor;
            }

            if (!string.IsNullOrWhiteSpace(state.ViewerFamily) && ViewerFamilyOptions.Contains(state.ViewerFamily))
            {
                SelectedViewerFamily = state.ViewerFamily;
            }

            UpdateSequenceOptions(true);

            if (!string.IsNullOrWhiteSpace(state.ViewerSequenceId) && ViewerSequenceOptions.Contains(state.ViewerSequenceId))
            {
                SelectedViewerSequenceId = state.ViewerSequenceId;
            }

            var sequence = GetSelectedSequence();
            if (sequence is not null)
            {
                _currentFrameIndex = Math.Clamp(state.FrameIndex, 0, Math.Max(0, sequence.FrameCount - 1));
                _playbackFrameIndex = _currentFrameIndex;
            }

            SelectedWorkspaceTabIndex = Math.Clamp(state.WorkspaceTabIndex, 0, 5);
            SelectedStudioTabIndex = Math.Clamp(state.StudioTabIndex, 0, 1);
            UpdateViewer();

            if (!string.IsNullOrWhiteSpace(state.EditorDraftPath) &&
                File.Exists(state.EditorDraftPath) &&
                !string.IsNullOrWhiteSpace(state.EditorSaveTargetPath))
            {
                LoadEditorFrame(state.EditorDraftPath, false, state.EditorSaveTargetPath);
                IsEditorDirty = state.EditorWasDirty;
                EditorStatusMessage = $"Restored in-progress draft for {Path.GetFileName(state.EditorSaveTargetPath)}.";
            }

            AddActivity("session", "Restored the last workspace state.");
        }
        catch (Exception ex)
        {
            AddActivity("session", $"Could not restore the last workspace state: {ex.Message}");
        }
        finally
        {
            _isRestoringWorkspaceState = false;
            NotifyLoopStateChanged();
        }
    }

    private string? SaveEditorDraftSnapshot()
    {
        if (_editorPixels.Length == 0 || string.IsNullOrWhiteSpace(_workspaceStatePath))
        {
            return null;
        }

        var sessionDirectory = Path.Combine(Path.GetDirectoryName(_workspaceStatePath) ?? string.Empty, "editor-session");
        Directory.CreateDirectory(sessionDirectory);
        var draftPath = Path.Combine(sessionDirectory, "current-editor-draft.png");
        SaveEditorPixelsToPath(draftPath);
        return draftPath;
    }

    private void AddActivity(string area, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ActivityLog.Insert(0, new ActivityLogItemViewModel(DateTimeOffset.Now, area, message));
        while (ActivityLog.Count > 40)
        {
            ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }

        OnPropertyChanged(nameof(ActivitySummary));
    }

    private void NotifyCurrentTaskChanged()
    {
        OnPropertyChanged(nameof(HasCurrentTask));
        OnPropertyChanged(nameof(CurrentTaskTitle));
        OnPropertyChanged(nameof(CurrentTaskSummary));
        OnPropertyChanged(nameof(CurrentTaskTargetSummary));
        OnPropertyChanged(nameof(CurrentTaskWorkspaceHint));
        OnPropertyChanged(nameof(LiveWorkMonitorSummary));
        OnPropertyChanged(nameof(LiveWorkVisualHint));
    }

    private void NotifyLoopStateChanged()
    {
        OnPropertyChanged(nameof(CurrentWorkspaceLabel));
        OnPropertyChanged(nameof(LoopGuideSummary));
        OnPropertyChanged(nameof(ReviewStepSummary));
        OnPropertyChanged(nameof(AnimateStepSummary));
        OnPropertyChanged(nameof(PaintStepSummary));
        OnPropertyChanged(nameof(SaveReplayStepSummary));
        OnPropertyChanged(nameof(PaintEditTargetTitle));
        OnPropertyChanged(nameof(PaintEditTargetSummary));
        OnPropertyChanged(nameof(PaintEditStateSummary));
        OnPropertyChanged(nameof(PaintNextStepSummary));
        OnPropertyChanged(nameof(PaintMonitorSummary));
        OnPropertyChanged(nameof(CoreLoopSummary));
        OnPropertyChanged(nameof(HasCurrentFrameBitmap));
        OnPropertyChanged(nameof(IsCurrentFrameBitmapMissing));
        OnPropertyChanged(nameof(HasRuntimeFrameBitmap));
        OnPropertyChanged(nameof(IsRuntimeFrameBitmapMissing));
        OnPropertyChanged(nameof(HasOnionSkinBitmap));
        NotifyCurrentTaskChanged();
    }

    private void ApplyReviewData(ProjectReviewData? reviewData)
    {
        var reviewLookup = (reviewData?.BaseVariantReviews ?? [])
            .ToDictionary(review => BuildVariantKey(review.Species, review.Age, review.Gender), review => review, StringComparer.OrdinalIgnoreCase);

        foreach (var row in _allBaseVariants)
        {
            reviewLookup.TryGetValue(BuildVariantKey(row.Species, row.Age, row.Gender), out var review);
            row.ReviewStatus = review?.Status ?? "unreviewed";
            row.ReviewNote = review?.Note ?? string.Empty;
            row.ReviewUpdatedUtc = review?.UpdatedUtc;
        }

        _frameReviewLookup.Clear();
        foreach (var frameReview in reviewData?.FrameReviews ?? [])
        {
            _frameReviewLookup[BuildFrameReviewKey(
                frameReview.Species,
                frameReview.Age,
                frameReview.Gender,
                frameReview.Color,
                frameReview.Family,
                frameReview.SequenceId,
                frameReview.FrameIndex)] = frameReview;
        }

        RefreshRepairQueue();
        RefreshFrameReviewQueue();
        RefreshVariantFrameQuality();
        RefreshTrustedExportBlockers();
        OnPropertyChanged(nameof(SelectedBaseVariantReviewSummary));
        OnPropertyChanged(nameof(SelectedBaseVariantFrameQualitySummary));
        OnPropertyChanged(nameof(SelectedBaseVariantFrameIssueSummary));
        OnPropertyChanged(nameof(NeedsReviewSummary));
    }

    private void ApplyRequestData(ProjectRequestData? requestData)
    {
        var selectedRequestId = SelectedRequestItem?.RequestId ?? string.Empty;

        Requests.Clear();
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
                BuildRequestHealthSummary(request.TargetScope, request.Title, request.Details, request.SourceNote),
                (request.History ?? [])
                    .OrderByDescending(entry => entry.UpdatedUtc)
                    .Select(entry => new RequestHistoryItemViewModel(entry.EventType, entry.Message, entry.UpdatedUtc))
                    .ToList(),
                request.UpdatedUtc));
        }

        SelectedRequestItem = string.IsNullOrWhiteSpace(selectedRequestId)
            ? Requests.FirstOrDefault()
            : Requests.FirstOrDefault(request => request.RequestId.Equals(selectedRequestId, StringComparison.OrdinalIgnoreCase))
                ?? Requests.FirstOrDefault();

        RefreshAutomationQueue();
        OnPropertyChanged(nameof(RequestSummary));
        OnPropertyChanged(nameof(SelectedRequestHealthSummary));
        OnPropertyChanged(nameof(SelectedRequestHistorySummary));
        OnPropertyChanged(nameof(DraftRequestPreview));
        OnPropertyChanged(nameof(StudioAiHandoffSummary));
    }

    private void ApplyCandidateData(ProjectCandidateData? candidateData)
    {
        var selectedCandidateId = SelectedCandidateItem?.CandidateId ?? string.Empty;

        Candidates.Clear();
        foreach (var candidate in (candidateData?.Candidates ?? []).OrderByDescending(candidate => candidate.UpdatedUtc))
        {
            Candidates.Add(new CandidateItemViewModel(
                candidate.CandidateId,
                candidate.Title,
                candidate.TargetScope,
                candidate.SourceType,
                candidate.Status,
                candidate.RequestId,
                candidate.CandidateImagePath,
                candidate.ReferenceImagePath,
                candidate.TargetFramePath,
                candidate.ImportBackupPath,
                candidate.Note,
                candidate.UpdatedUtc));
        }

        SelectedCandidateItem = string.IsNullOrWhiteSpace(selectedCandidateId)
            ? Candidates.FirstOrDefault()
            : Candidates.FirstOrDefault(candidate => candidate.CandidateId.Equals(selectedCandidateId, StringComparison.OrdinalIgnoreCase))
                ?? Candidates.FirstOrDefault();

        RefreshAutomationQueue();
        OnPropertyChanged(nameof(CandidateSummary));
        OnPropertyChanged(nameof(SelectedCandidateSummary));
        OnPropertyChanged(nameof(SelectedCandidateTargetSummary));
        OnPropertyChanged(nameof(StudioCandidateHandoffSummary));
    }

    private void FocusCurrentTask(bool surfaceWorkspace, bool logActivity)
    {
        var frameTask = SelectedFrameReviewQueueItem ?? FrameReviewQueueItems.FirstOrDefault();
        if (frameTask is not null)
        {
            if (!ReferenceEquals(SelectedFrameReviewQueueItem, frameTask))
            {
                SelectedFrameReviewQueueItem = frameTask;
            }
            else if (surfaceWorkspace)
            {
                SurfaceAnimateWorkspace(false);
            }

            if (logActivity)
            {
                AddActivity("review", $"Focused current task: {frameTask.FrameId}.");
            }

            return;
        }

        var automationTask = SelectedAutomationTaskItem ?? AutomationQueueItems.FirstOrDefault();
        if (automationTask is not null)
        {
            if (!ReferenceEquals(SelectedAutomationTaskItem, automationTask))
            {
                SelectedAutomationTaskItem = automationTask;
            }

            if (surfaceWorkspace)
            {
                TryNavigateToTargetScope(automationTask.TargetScope);
            }
            else
            {
                TryNavigateToTargetScope(automationTask.TargetScope);
            }

            if (logActivity)
            {
                AddActivity("automation", $"Focused current task: {automationTask.DisplayName}.");
            }

            return;
        }

        var requestTask = SelectedRequestItem ?? Requests.FirstOrDefault(request =>
            !request.Status.Equals("approved", StringComparison.OrdinalIgnoreCase) &&
            !request.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) &&
            !request.Status.Equals("rejected", StringComparison.OrdinalIgnoreCase));
        if (requestTask is not null)
        {
            if (!Equals(SelectedRequestItem, requestTask))
            {
                SelectedRequestItem = requestTask;
            }
            else if (surfaceWorkspace)
            {
                SurfaceRequestsWorkspace(false);
                TryNavigateToTargetScope(requestTask.TargetScope);
            }

            if (logActivity)
            {
                AddActivity("request", $"Focused current task: {requestTask.Title}.");
            }

            return;
        }

        var candidateTask = SelectedCandidateItem ?? Candidates.FirstOrDefault(candidate =>
            !candidate.Status.Equals("approved", StringComparison.OrdinalIgnoreCase) &&
            !candidate.Status.Equals("rejected", StringComparison.OrdinalIgnoreCase));
        if (candidateTask is not null)
        {
            if (!ReferenceEquals(SelectedCandidateItem, candidateTask))
            {
                SelectedCandidateItem = candidateTask;
            }
            else if (surfaceWorkspace)
            {
                SurfaceRequestsWorkspace(false);
            }

            if (logActivity)
            {
                AddActivity("candidate", $"Focused current task: {candidateTask.Title}.");
            }
        }
    }

    private CurrentTaskDescriptor BuildCurrentTaskDescriptor()
    {
        var frameTask = SelectedFrameReviewQueueItem ?? FrameReviewQueueItems.FirstOrDefault();
        if (frameTask is not null)
        {
            return new CurrentTaskDescriptor(
                $"Flagged Frame: {frameTask.FrameId}",
                $"{frameTask.StatusLabel}  |  {frameTask.DisplayName}  |  {frameTask.IssueTagSummary}",
                $"{frameTask.Family}/{frameTask.SequenceId}  |  {frameTask.Color}",
                "Studio / Animate is the live focus. Pause on this frame, edit it in Paint, then save and replay.");
        }

        var automationTask = SelectedAutomationTaskItem ?? AutomationQueueItems.FirstOrDefault();
        if (automationTask is not null)
        {
            return new CurrentTaskDescriptor(
                $"AI Task: {automationTask.DisplayName}",
                $"{automationTask.StatusLabel}  |  {automationTask.TypeLabel}  |  {automationTask.LatestActivitySummary}",
                string.IsNullOrWhiteSpace(automationTask.TargetScope) ? "No target scope recorded yet." : automationTask.TargetScope,
                "An AI task is active, but the visible work surface should stay on the target frame in Studio whenever a concrete frame needs repair.");
        }

        var requestTask = SelectedRequestItem ?? Requests.FirstOrDefault(request =>
            !request.Status.Equals("approved", StringComparison.OrdinalIgnoreCase) &&
            !request.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) &&
            !request.Status.Equals("rejected", StringComparison.OrdinalIgnoreCase));
        if (requestTask is not null)
        {
            return new CurrentTaskDescriptor(
                $"Saved Request: {requestTask.Title}",
                $"{requestTask.StatusLabel}  |  {requestTask.TypeLabel}  |  {requestTask.HealthSummary}",
                string.IsNullOrWhiteSpace(requestTask.TargetScope) ? "No target scope recorded yet." : requestTask.TargetScope,
                "Requests is the live focus. Review the target, refine the request, then queue or hand it off visibly.");
        }

        var candidateTask = SelectedCandidateItem ?? Candidates.FirstOrDefault();
        if (candidateTask is not null)
        {
            return new CurrentTaskDescriptor(
                $"Candidate: {candidateTask.Title}",
                $"{candidateTask.StatusLabel}  |  {candidateTask.SourceTypeLabel}  |  {candidateTask.NotePreview}",
                string.IsNullOrWhiteSpace(candidateTask.TargetScope) ? "No target scope recorded yet." : candidateTask.TargetScope,
                "Requests is the live focus. Compare, load into Paint, apply, or reject this candidate visibly.");
        }

        return new CurrentTaskDescriptor(
            "No active task",
            "The project is loaded, but there is no queued automation, flagged frame, pending request, or staged candidate in focus.",
            "Pick a row in Review or Queue to start visible work.",
            "Review is usually the right place to begin a new audit or repair pass.");
    }

    private void ExecuteLiveOperation(LiveOperationRecord operation)
    {
        var op = operation.Op.Trim().ToLowerInvariant();
        switch (op)
        {
            case "focus_request":
                if (!string.IsNullOrWhiteSpace(operation.RequestId))
                {
                    var request = Requests.FirstOrDefault(item => item.RequestId.Equals(operation.RequestId, StringComparison.OrdinalIgnoreCase));
                    if (request is not null)
                    {
                        SelectedRequestItem = request;
                        LiveOperationStatusMessage = $"Focused request {request.RequestId}.";
                        AddActivity("session", $"Visible op focused request {request.RequestId}.");
                    }
                }
                break;
            case "focus_frame":
                if (TryFocusLiveOperationFrame(operation))
                {
                    LiveOperationStatusMessage = $"Focused frame {ViewerSelectionSummary} for visible work.";
                    AddActivity("session", $"Visible op focused frame {ViewerSelectionSummary}.");
                }
                break;
            case "open_animate":
                OpenAnimateWorkspace();
                LiveOperationStatusMessage = "Opened Animate in the visible work surface.";
                AddActivity("session", "Visible op opened Animate.");
                break;
            case "open_paint":
                OpenPaintWorkspace();
                LiveOperationStatusMessage = "Opened Paint in the visible work surface.";
                AddActivity("session", "Visible op opened Paint.");
                break;
            case "load_current_frame":
                LoadCurrentFrameIntoEditor();
                LiveOperationStatusMessage = "Loaded the current authored frame into Paint.";
                AddActivity("session", "Visible op loaded the current authored frame into Paint.");
                break;
            case "load_runtime_template":
                LoadRuntimeFrameIntoEditor();
                LiveOperationStatusMessage = "Loaded the runtime template into Paint.";
                AddActivity("session", "Visible op loaded the runtime template into Paint.");
                break;
            case "play_preview":
                PlayPreview();
                LiveOperationStatusMessage = $"Started playback on {PlaybackSelectionSummary}.";
                AddActivity("session", "Visible op started playback.");
                break;
            case "pause_preview":
                PausePreview();
                LiveOperationStatusMessage = $"Paused playback on {ViewerSelectionSummary}.";
                AddActivity("session", "Visible op paused playback.");
                break;
            case "stage_authored_candidate":
                StageCurrentAuthoredCandidate();
                LiveOperationStatusMessage = "Staged the current authored frame as a candidate.";
                AddActivity("session", "Visible op staged the authored frame as a candidate.");
                break;
            case "stage_runtime_candidate":
                StageRuntimeCandidate();
                LiveOperationStatusMessage = "Staged the current runtime frame as a candidate.";
                AddActivity("session", "Visible op staged the runtime frame as a candidate.");
                break;
            case "set_tool":
                if (!string.IsNullOrWhiteSpace(operation.Tool))
                {
                    SelectedEditorTool = operation.Tool.Trim().ToLowerInvariant();
                    LiveOperationStatusMessage = $"Set the editor tool to {SelectedEditorTool}.";
                    AddActivity("session", $"Visible op set the tool to {SelectedEditorTool}.");
                }
                break;
            case "set_color":
                if (!string.IsNullOrWhiteSpace(operation.ColorHex))
                {
                    SelectedEditorColorHex = operation.ColorHex;
                    LiveOperationStatusMessage = $"Set the editor color to {SelectedEditorColorHex}.";
                    AddActivity("session", $"Visible op set the color to {SelectedEditorColorHex}.");
                }
                break;
            case "set_brush_size":
                if (operation.BrushSize is { } brushSize)
                {
                    SelectedEditorBrushSize = Math.Clamp(brushSize, 1, 8);
                    LiveOperationStatusMessage = $"Set the brush size to {SelectedEditorBrushSize}px.";
                    AddActivity("session", $"Visible op set the brush size to {SelectedEditorBrushSize}px.");
                }
                break;
            case "paint_pixel":
                ApplyLivePixelOperation(operation, erase: false);
                break;
            case "erase_pixel":
                ApplyLivePixelOperation(operation, erase: true);
                break;
            case "save_frame":
                SaveEditedFrame();
                LiveOperationStatusMessage = "Saved the current Paint frame.";
                AddActivity("session", "Visible op saved the current frame.");
                break;
            case "save_and_replay":
                SaveAndReturnToAnimate();
                LiveOperationStatusMessage = "Saved the frame and returned to Animate.";
                AddActivity("session", "Visible op saved and returned to Animate.");
                break;
            default:
                LiveOperationStatusMessage = $"Skipped unknown live operation '{operation.Op}'.";
                AddActivity("session", $"Skipped unknown visible op '{operation.Op}'.");
                break;
        }
    }

    private bool TryFocusLiveOperationFrame(LiveOperationRecord operation)
    {
        if (string.IsNullOrWhiteSpace(operation.Species) ||
            string.IsNullOrWhiteSpace(operation.Age) ||
            string.IsNullOrWhiteSpace(operation.Gender))
        {
            return false;
        }

        var variant = _allBaseVariants.FirstOrDefault(row =>
            row.Species.Equals(operation.Species, StringComparison.OrdinalIgnoreCase) &&
            row.Age.Equals(operation.Age, StringComparison.OrdinalIgnoreCase) &&
            row.Gender.Equals(operation.Gender, StringComparison.OrdinalIgnoreCase));
        if (variant is null)
        {
            return false;
        }

        SelectedBaseVariant = variant;
        if (!string.IsNullOrWhiteSpace(operation.Color) && ViewerColorOptions.Contains(operation.Color))
        {
            SelectedViewerColor = operation.Color;
        }

        if (!string.IsNullOrWhiteSpace(operation.Family) && ViewerFamilyOptions.Contains(operation.Family))
        {
            SelectedViewerFamily = operation.Family;
        }

        if (!string.IsNullOrWhiteSpace(operation.SequenceId))
        {
            UpdateSequenceOptions(false);
            if (ViewerSequenceOptions.Contains(operation.SequenceId))
            {
                SelectedViewerSequenceId = operation.SequenceId;
            }
        }

        if (operation.FrameIndex is { } frameIndex)
        {
            _currentFrameIndex = Math.Max(0, frameIndex);
            _playbackFrameIndex = Math.Max(0, frameIndex);
        }

        SurfaceAnimateWorkspace(false);
        UpdateViewer();
        return true;
    }

    private void ApplyLivePixelOperation(LiveOperationRecord operation, bool erase)
    {
        if (operation.X is null || operation.Y is null || EditorPixels.Count == 0)
        {
            AddActivity("session", "Visible pixel op skipped because Paint has no loaded frame.");
            return;
        }

        var pixel = EditorPixels.FirstOrDefault(item => item.X == operation.X.Value && item.Y == operation.Y.Value);
        if (pixel is null)
        {
            AddActivity("session", $"Visible pixel op skipped because {operation.X},{operation.Y} is outside the current canvas.");
            return;
        }

        if (!erase && !string.IsNullOrWhiteSpace(operation.ColorHex))
        {
            SelectedEditorColorHex = operation.ColorHex;
        }

        SelectedEditorTool = erase ? "erase" : "brush";
        ApplyEditorTool(pixel);
        LiveOperationStatusMessage = erase
            ? $"Erased pixel {pixel.X},{pixel.Y} on the live canvas."
            : $"Painted pixel {pixel.X},{pixel.Y} on the live canvas.";
        AddActivity("session", erase
            ? $"Visible op erased pixel {pixel.X},{pixel.Y}."
            : $"Visible op painted pixel {pixel.X},{pixel.Y}.");
    }

    private readonly record struct CurrentTaskDescriptor(string Title, string Summary, string TargetSummary, string WorkspaceHint);
    private sealed class LiveOperationRecord
    {
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("action")]
        public string Op { get; set; } = string.Empty;
        [JsonPropertyName("request")]
        public string RequestId { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public string Age { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
        [JsonPropertyName("sequence")]
        public string SequenceId { get; set; } = string.Empty;
        [JsonPropertyName("frame_index")]
        public int? FrameIndex { get; set; }
        [JsonPropertyName("frame")]
        public string FrameId { get; set; } = string.Empty;
        public int? X { get; set; }
        public int? Y { get; set; }
        public string ColorHex { get; set; } = string.Empty;
        public string Tool { get; set; } = string.Empty;
        [JsonPropertyName("brush_size")]
        public int? BrushSize { get; set; }
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

    private static string BuildFrameReviewKey(string species, string age, string gender, string color, string family, string sequenceId, int frameIndex)
        => $"{species}|{age}|{gender}|{color}|{family}|{sequenceId}|{frameIndex}";

    private string BuildRequestHealthSummary(string targetScope, string title, string details, string sourceNote)
    {
        var parts = (targetScope ?? string.Empty)
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return "Custom or non-row target. Review it manually before queueing AI work.";
        }

        var species = parts[0];
        var age = parts[1];
        var gender = parts[2];
        var variant = _allBaseVariants.FirstOrDefault(row =>
            row.Species.Equals(species, StringComparison.OrdinalIgnoreCase) &&
            row.Age.Equals(age, StringComparison.OrdinalIgnoreCase) &&
            row.Gender.Equals(gender, StringComparison.OrdinalIgnoreCase));
        if (variant is null)
        {
            return "Target row is not currently mapped in this project.";
        }

        var inferredFamily = InferRequestFamily(title, details, sourceNote);
        var relevantReviews = _frameReviewLookup.Values.Where(record =>
            record.Species.Equals(species, StringComparison.OrdinalIgnoreCase) &&
            record.Age.Equals(age, StringComparison.OrdinalIgnoreCase) &&
            record.Gender.Equals(gender, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(inferredFamily) || record.Family.Equals(inferredFamily, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var flaggedCount = relevantReviews.Count(record =>
            record.Status.Equals("needs_review", StringComparison.OrdinalIgnoreCase) ||
            record.Status.Equals("to_be_repaired", StringComparison.OrdinalIgnoreCase) ||
            record.Status.Equals("do_not_use", StringComparison.OrdinalIgnoreCase));
        var templateCount = relevantReviews.Count(record =>
            record.Status.Equals("template_only", StringComparison.OrdinalIgnoreCase));

        if (flaggedCount > 0)
        {
            return inferredFamily is null
                ? $"{flaggedCount} flagged frame(s) still exist for this row."
                : $"{flaggedCount} flagged frame(s) still exist in {inferredFamily}.";
        }

        if (variant.ReviewStatus.Equals("needs_review", StringComparison.OrdinalIgnoreCase) ||
            variant.ReviewStatus.Equals("to_be_repaired", StringComparison.OrdinalIgnoreCase) ||
            variant.ReviewStatus.Equals("do_not_use", StringComparison.OrdinalIgnoreCase))
        {
            return $"Row is still marked {FormatStatusLabel(variant.ReviewStatus)}.";
        }

        if (variant.ReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase))
        {
            return templateCount > 0
                ? $"Likely stale: row is approved and only {templateCount} template-only frame(s) remain."
                : "Likely stale: row is approved and no flagged frames are currently recorded.";
        }

        return relevantReviews.Count == 0
            ? "No active frame blockers are recorded yet. Recheck visually before queueing AI work."
            : $"No flagged frames are currently recorded{(string.IsNullOrWhiteSpace(inferredFamily) ? string.Empty : $" in {inferredFamily}")}.";
    }

    private string? InferRequestFamily(string title, string details, string sourceNote)
    {
        var combined = string.Join(" ", [title ?? string.Empty, details ?? string.Empty, sourceNote ?? string.Empty]);

        foreach (var family in _familyOrder)
        {
            if (combined.IndexOf(family, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return family;
            }
        }

        return null;
    }

    private void SurfaceAutomationWorkspace(bool logActivity)
    {
        SelectedWorkspaceTabIndex = 0;
        if (logActivity)
        {
            AddActivity("automation", "Opened Automation workspace.");
        }
    }

    private void SurfaceReviewWorkspace(bool logActivity)
    {
        SelectedWorkspaceTabIndex = 1;
        if (logActivity)
        {
            AddActivity("review", "Opened Review workspace.");
        }
    }

    private void SurfaceQueueWorkspace(bool logActivity)
    {
        SelectedWorkspaceTabIndex = 2;
        if (logActivity)
        {
            AddActivity("review", "Opened Queue workspace.");
        }
    }

    private void SurfaceRequestsWorkspace(bool logActivity)
    {
        SelectedWorkspaceTabIndex = 3;
        if (logActivity)
        {
            AddActivity("request", "Opened Requests workspace.");
        }
    }

    private void SurfaceAnimateWorkspace(bool logActivity)
    {
        SelectedWorkspaceTabIndex = 4;
        SelectedStudioTabIndex = 0;
        if (logActivity)
        {
            AddActivity("viewer", "Opened Animate workspace.");
        }
    }

    private void SurfacePaintWorkspace(bool logActivity)
    {
        SelectedWorkspaceTabIndex = 4;
        SelectedStudioTabIndex = 1;
        if (logActivity)
        {
            AddActivity("editor", "Opened Paint workspace.");
        }
    }

    private void SurfacePlanningWorkspace(bool logActivity)
    {
        SelectedWorkspaceTabIndex = 5;
        if (logActivity)
        {
            AddActivity("planning", "Opened Planning workspace.");
        }
    }

    private void PrefillRequestDraft(
        string requestType,
        string titlePrefix,
        string targetScope,
        string details,
        string mustPreserve,
        string mustAvoid,
        string sourceNote)
    {
        DraftRequestType = requestType;
        DraftRequestStatus = "draft";
        DraftRequestTitle = $"{titlePrefix} {requestType.Replace('_', ' ')}".Trim();
        DraftRequestTargetScope = targetScope;
        DraftRequestDetails = details;
        DraftRequestMustPreserve = mustPreserve;
        DraftRequestMustAvoid = mustAvoid;
        DraftRequestSourceNote = sourceNote;
        OnPropertyChanged(nameof(DraftRequestPreview));
    }

    private string BuildCurrentFrameReviewKey()
    {
        if (SelectedBaseVariant is null ||
            string.IsNullOrWhiteSpace(SelectedViewerColor) ||
            string.IsNullOrWhiteSpace(SelectedViewerFamily) ||
            string.IsNullOrWhiteSpace(SelectedViewerSequenceId))
        {
            return string.Empty;
        }

        return BuildFrameReviewKey(
            SelectedBaseVariant.Species,
            SelectedBaseVariant.Age,
            SelectedBaseVariant.Gender,
            SelectedViewerColor,
            SelectedViewerFamily,
            SelectedViewerSequenceId,
            _currentFrameIndex);
    }

    private bool TryGetCurrentFrameReviewContext(out string frameId)
    {
        frameId = string.Empty;
        var sequence = GetSelectedSequence();
        if (SelectedBaseVariant is null ||
            string.IsNullOrWhiteSpace(SelectedViewerColor) ||
            string.IsNullOrWhiteSpace(SelectedViewerFamily) ||
            sequence is null ||
            sequence.FrameCount <= 0)
        {
            return false;
        }

        var currentSlot = ((_currentFrameIndex % sequence.FrameCount) + sequence.FrameCount) % sequence.FrameCount;
        frameId = $"{sequence.SequenceId}_{currentSlot:00}";
        return true;
    }

    private string BuildFrameReviewNotePreview()
    {
        if (string.IsNullOrWhiteSpace(SelectedFrameReviewNote))
        {
            return "No frame note yet.";
        }

        return SelectedFrameReviewNote.Length <= 88 ? SelectedFrameReviewNote : $"{SelectedFrameReviewNote[..85]}...";
    }

    private string BuildFrameIssueTagsSummary()
    {
        var tags = ParseFrameIssueTags(SelectedFrameIssueTagsText).ToList();
        return tags.Count == 0 ? "No tags" : string.Join(", ", tags);
    }

    private static IReadOnlyList<string> ParseFrameIssueTags(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string SlugifyPlanningValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var collapsed = new string(chars);
        while (collapsed.Contains("--", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        }

        return collapsed.Trim('-');
    }

    private static string FormatStatusLabel(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Unreviewed";
        }

        var spaced = status.Replace('_', ' ');
        return string.Join(" ", spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

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

    private static string SummarizeWorkflowOutput(string output, bool wasStopped)
    {
        if (wasStopped)
        {
            return "Process was stopped before it completed.";
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            return "Completed without terminal output.";
        }

        var lines = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        if (lines.Count == 0)
        {
            return "Completed without terminal output.";
        }

        return string.Join(Environment.NewLine, lines.TakeLast(6));
    }

    private static string ToHexColor(Rgba32 color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";

    private static bool TryParseHexColor(string hexColor, out Rgba32 color)
    {
        try
        {
            var parsed = Avalonia.Media.Color.Parse(hexColor);
            color = new Rgba32(parsed.R, parsed.G, parsed.B, parsed.A);
            return true;
        }
        catch
        {
            color = default;
            return false;
        }
    }

    private static SolidColorBrush CreateBrush(Rgba32 color) => new(new Avalonia.Media.Color(color.A, color.R, color.G, color.B));

    private static string ResolvePath(string rootPath, string relativeOrAbsolutePath)
    {
        return Path.IsPathRooted(relativeOrAbsolutePath)
            ? relativeOrAbsolutePath
            : Path.GetFullPath(Path.Combine(rootPath, relativeOrAbsolutePath));
    }

    private static string BuildPlanningChecklistMarkdown(ProjectConfig config, IReadOnlyList<PlanningAssetChecklistEntry> entries)
    {
        var lines = new List<string>
        {
            $"# {config.DisplayName} Starter Checklist",
            string.Empty,
            $"Project Id: {config.ProjectId}",
            $"Root Path: {config.RootPath}",
            $"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}",
            string.Empty,
        };

        foreach (var entry in entries
                     .OrderBy(item => item.Species, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Age, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Gender, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Color, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Family, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.SequenceId, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"## {entry.Species} / {entry.Age} / {entry.Gender} / {entry.Color}");
            lines.Add($"- family: {entry.Family}");
            lines.Add($"- sequence: {entry.SequenceId}");
            lines.Add($"- frames: {entry.FrameCount}");
            lines.Add($"- folder: {entry.VariantDirectory}");
            lines.Add("- frame files:");
            lines.AddRange(entry.FrameFiles.Select(file => $"  - {file}"));
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static List<PlanningAssetChecklistEntry> BuildPlanningAssetChecklistEntries(ProjectConfig config, string authoredRoot)
    {
        var species = config.VariantAxes.Species.Length == 0 ? ["default"] : config.VariantAxes.Species;
        var ages = config.VariantAxes.Age.Length == 0 ? ["default"] : config.VariantAxes.Age;
        var genders = config.VariantAxes.Gender.Length == 0 ? ["default"] : config.VariantAxes.Gender;
        var colors = config.VariantAxes.Color.Length == 0 ? ["base"] : config.VariantAxes.Color;
        var entries = new List<PlanningAssetChecklistEntry>();

        foreach (var speciesValue in species)
        {
            foreach (var ageValue in ages)
            {
                foreach (var genderValue in genders)
                {
                    foreach (var colorValue in colors)
                    {
                        var variantDirectory = Path.Combine(authoredRoot, speciesValue, ageValue, genderValue, colorValue);
                        foreach (var family in config.Families.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                        {
                            foreach (var sequence in family.Value)
                            {
                                var frameFiles = Enumerable.Range(0, Math.Max(0, sequence.FrameCount))
                                    .Select(index => $"{sequence.SequenceId}_{index:00}.png")
                                    .ToArray();

                                entries.Add(new PlanningAssetChecklistEntry(
                                    speciesValue,
                                    ageValue,
                                    genderValue,
                                    colorValue,
                                    family.Key,
                                    sequence.SequenceId,
                                    sequence.FrameCount,
                                    variantDirectory,
                                    frameFiles));
                            }
                        }
                    }
                }
            }
        }

        return entries;
    }

    private void LoadPlanningTemplates()
    {
        PlanningTemplates.Clear();
        foreach (var template in LoadPlanningTemplateRecords()
                     .OrderByDescending(item => item.UpdatedUtc))
        {
            PlanningTemplates.Add(new PlanningTemplateItemViewModel(
                template.TemplateId,
                template.Name,
                template.SpeciesText,
                template.AgeText,
                template.GenderText,
                template.ColorText,
                template.FamilyBlueprintText,
                template.UpdatedUtc));
        }

        if (SelectedPlanningTemplateItem is not null)
        {
            SelectedPlanningTemplateItem = PlanningTemplates.FirstOrDefault(item =>
                item.TemplateId.Equals(SelectedPlanningTemplateItem.TemplateId, StringComparison.OrdinalIgnoreCase));
        }

        OnPropertyChanged(nameof(PlanningTemplateSummary));
    }

    private void LoadProjectPalettes()
    {
        ProjectPalettes.Clear();

        foreach (var palette in LoadProjectPaletteRecords()
                     .OrderByDescending(item => item.UpdatedUtc))
        {
            ProjectPalettes.Add(new ProjectPaletteItemViewModel(
                palette.PaletteId,
                palette.Name,
                string.IsNullOrWhiteSpace(palette.ScopeKind) ? "project" : palette.ScopeKind,
                palette.ScopeKey ?? string.Empty,
                palette.Colors ?? [],
                palette.UpdatedUtc));
        }

        SelectedProjectPaletteItem = ProjectPalettes.FirstOrDefault(item =>
            item.PaletteId.Equals(SelectedProjectPaletteItem?.PaletteId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            ?? ProjectPalettes.FirstOrDefault();
        OnPropertyChanged(nameof(ProjectPaletteSummary));
        OnPropertyChanged(nameof(SelectedProjectPaletteSummary));
    }

    private void LoadTrustedExportHistory()
    {
        TrustedExportHistoryItems.Clear();
        foreach (var record in LoadTrustedExportHistoryRecords()
                     .OrderByDescending(item => item.ExportedUtc))
        {
            TrustedExportHistoryItems.Add(new TrustedExportHistoryItemViewModel(
                record.ExportId,
                record.ExportDirectory,
                record.ApprovedFrameCount,
                record.ApprovedRowCount,
                record.FlaggedFrameCount,
                record.ExportedUtc));
        }

        SelectedTrustedExportHistoryItem = TrustedExportHistoryItems.FirstOrDefault(item =>
            item.ExportId.Equals(SelectedTrustedExportHistoryItem?.ExportId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            ?? TrustedExportHistoryItems.FirstOrDefault();
        OnPropertyChanged(nameof(TrustedExportHistorySummary));
        RefreshProjectReadiness();
    }

    private void LoadValidationReports()
    {
        ValidationReports.Clear();
        if (string.IsNullOrWhiteSpace(_projectValidationReportDirectory) || !Directory.Exists(_projectValidationReportDirectory))
        {
            OnPropertyChanged(nameof(ValidationReportSummary));
            OnPropertyChanged(nameof(SelectedValidationReportSummary));
            RefreshProjectReadiness();
            return;
        }

        foreach (var markdownPath in Directory.EnumerateFiles(_projectValidationReportDirectory, "*.md")
                     .OrderByDescending(path => File.GetLastWriteTimeUtc(path)))
        {
            var reportId = Path.GetFileNameWithoutExtension(markdownPath);
            var jsonPath = Path.Combine(_projectValidationReportDirectory, $"{reportId}.json");
            ValidationReports.Add(new ValidationReportItemViewModel(
                reportId,
                markdownPath,
                File.Exists(jsonPath) ? jsonPath : string.Empty,
                BuildValidationReportSummary(reportId),
                File.GetLastWriteTimeUtc(markdownPath)));
        }

        SelectedValidationReportItem = ValidationReports.FirstOrDefault(item =>
            item.ReportId.Equals(SelectedValidationReportItem?.ReportId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            ?? ValidationReports.FirstOrDefault();
        OnPropertyChanged(nameof(ValidationReportSummary));
        OnPropertyChanged(nameof(SelectedValidationReportSummary));
        RefreshProjectReadiness();
    }

    private List<TrustedExportHistoryRecord> LoadTrustedExportHistoryRecords()
    {
        if (string.IsNullOrWhiteSpace(_trustedExportHistoryPath) || !File.Exists(_trustedExportHistoryPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<TrustedExportHistoryRecord>>(File.ReadAllText(_trustedExportHistoryPath))
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void AppendTrustedExportHistory(TrustedExportHistoryItemViewModel item)
    {
        TrustedExportHistoryItems.Insert(0, item);
        while (TrustedExportHistoryItems.Count > 20)
        {
            TrustedExportHistoryItems.RemoveAt(TrustedExportHistoryItems.Count - 1);
        }

        SelectedTrustedExportHistoryItem = item;
        PersistTrustedExportHistory();
        OnPropertyChanged(nameof(TrustedExportHistorySummary));
    }

    private void PersistTrustedExportHistory()
    {
        if (string.IsNullOrWhiteSpace(_trustedExportHistoryPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_trustedExportHistoryPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            _trustedExportHistoryPath,
            JsonSerializer.Serialize(
                TrustedExportHistoryItems
                    .Select(item => new TrustedExportHistoryRecord(
                        item.ExportId,
                        item.ExportDirectory,
                        item.ApprovedFrameCount,
                        item.ApprovedRowCount,
                        item.FlaggedFrameCount,
                        item.ExportedUtc))
                    .ToList(),
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private ProjectValidationReport BuildProjectValidationReport()
    {
        var rootPath = PlanningRootPath.Trim();
        var authoredRoot = string.IsNullOrWhiteSpace(_authoredSpriteRoot)
            ? ResolvePlanningWorkspacePath(rootPath, "sprites_authored")
            : _authoredSpriteRoot;
        var runtimeRoot = string.IsNullOrWhiteSpace(_runtimeSpriteRoot)
            ? ResolvePlanningWorkspacePath(rootPath, "sprites_runtime")
            : _runtimeSpriteRoot;
        var workflowRoot = string.IsNullOrWhiteSpace(_reviewDataPath)
            ? Path.Combine(rootPath, ".sprite-workflow")
            : Path.GetDirectoryName(_reviewDataPath) ?? Path.Combine(rootPath, ".sprite-workflow");

        var findings = new List<ProjectValidationFinding>
        {
            BuildPathFinding("project_root", rootPath, Directory.Exists(rootPath), "Project root is available for onboarding and exports.", "Project root is missing."),
            BuildPathFinding("authored_root", authoredRoot, Directory.Exists(authoredRoot), "Authored sprite root is present.", "Authored sprite root is missing."),
            BuildPathFinding("runtime_root", runtimeRoot, Directory.Exists(runtimeRoot), "Runtime/template sprite root is present.", "Runtime/template sprite root is missing."),
            BuildPathFinding("workflow_data", workflowRoot, Directory.Exists(workflowRoot), "Workflow data folder is present.", "Workflow data folder is missing."),
            new ProjectValidationFinding(
                "request_count",
                Requests.Count > 0 ? "ok" : "warning",
                $"{Requests.Count} saved request(s)",
                "Requests can drive AI, manual, and approval workflows.",
                string.Empty),
            new ProjectValidationFinding(
                "candidate_count",
                Candidates.Count > 0 ? "ok" : "warning",
                $"{Candidates.Count} staged candidate(s)",
                "Candidates are available for compare/apply workflows.",
                string.Empty),
            new ProjectValidationFinding(
                "ai_providers",
                AiProviders.Any(provider => provider.IsDefault) ? "ok" : "warning",
                AiProviderSummary,
                "A default provider adapter is configured for visible manual or AI-assisted work.",
                "No default provider adapter is configured yet."),
            new ProjectValidationFinding(
                "frame_review_queue",
                FrameReviewQueueItems.Count == 0 ? "ok" : "warning",
                $"{FrameReviewQueueItems.Count} flagged frame review item(s)",
                "Flagged frames are under control.",
                "Frame review queue still has flagged items."),
            new ProjectValidationFinding(
                "trusted_export_blockers",
                BuildTrustedExportSnapshot().FlaggedFrameCount == 0 ? "ok" : "warning",
                TrustedExportBlockerSummary,
                "Trusted export is not blocked by frame review state.",
                TrustedExportBlockerSummary),
            new ProjectValidationFinding(
                "planning_adoption",
                PlanningAdoptionEntries.Count == 0 || PlanningAdoptionEntries.All(entry => entry.StatusLabel.Equals("Complete", StringComparison.OrdinalIgnoreCase)) ? "ok" : "warning",
                PlanningAdoptionEntrySummary,
                "Planned sequences align with discovered authored assets.",
                "Planned sequence adoption still has partial or missing entries."),
            new ProjectValidationFinding(
                "project_readiness",
                ProjectReadinessItems.All(item => item.Status.Equals("ready", StringComparison.OrdinalIgnoreCase)) ? "ok" : "warning",
                ProjectReadinessSummary,
                "Readiness checks are clear for release-minded export and portability.",
                "One or more readiness checks still need attention.")
        };

        var warningCount = findings.Count(item => !item.Status.Equals("ok", StringComparison.OrdinalIgnoreCase));
        var summary = warningCount == 0
            ? "Validation clean: paths, workflow stores, and planning adoption look reusable."
            : $"Validation found {warningCount} warning(s) to review before calling this project fully portable.";

        return new ProjectValidationReport(
            SelectedProjectName,
            rootPath,
            authoredRoot,
            runtimeRoot,
            workflowRoot,
            DateTimeOffset.UtcNow,
            summary,
            findings,
            Requests.Count,
            Candidates.Count,
            FrameReviewQueueItems.Count,
            TrustedExportHistoryItems.Count,
            PlanningChecklist.Count,
            PlanningAdoptionEntries.Count);
    }

    private static ProjectValidationFinding BuildPathFinding(string id, string path, bool exists, string okMessage, string warningMessage)
    {
        return new ProjectValidationFinding(
            id,
            exists ? "ok" : "warning",
            string.IsNullOrWhiteSpace(path) ? "(not configured)" : path,
            okMessage,
            warningMessage);
    }

    private string BuildProjectValidationMarkdown(ProjectValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Project Validation Report");
        builder.AppendLine();
        builder.AppendLine($"- Project: {report.ProjectName}");
        builder.AppendLine($"- Generated: {report.GeneratedUtc.ToLocalTime():yyyy-MM-dd h:mm:ss tt}");
        builder.AppendLine($"- Root: {report.RootPath}");
        builder.AppendLine($"- Authored: {report.AuthoredRoot}");
        builder.AppendLine($"- Runtime: {report.RuntimeRoot}");
        builder.AppendLine($"- Workflow Data: {report.WorkflowRoot}");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine(report.Summary);
        builder.AppendLine();
        builder.AppendLine("## Counts");
        builder.AppendLine();
        builder.AppendLine($"- Requests: {report.RequestCount}");
        builder.AppendLine($"- Candidates: {report.CandidateCount}");
        builder.AppendLine($"- Flagged Frames: {report.FlaggedFrameCount}");
        builder.AppendLine($"- Trusted Exports: {report.TrustedExportCount}");
        builder.AppendLine($"- Planning Checklist Entries: {report.PlanningChecklistCount}");
        builder.AppendLine($"- Planning Adoption Entries: {report.PlanningAdoptionCount}");
        builder.AppendLine();
        builder.AppendLine("## Findings");
        builder.AppendLine();

        foreach (var finding in report.Findings)
        {
            builder.AppendLine($"### {finding.Id}");
            builder.AppendLine();
            builder.AppendLine($"- Status: {finding.Status}");
            builder.AppendLine($"- Summary: {finding.Summary}");
            builder.AppendLine($"- Healthy State: {finding.OkMessage}");
            if (!string.IsNullOrWhiteSpace(finding.WarningMessage))
            {
                builder.AppendLine($"- Warning: {finding.WarningMessage}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildValidationReportSummary(string reportId)
    {
        var jsonPath = Path.Combine(_projectValidationReportDirectory, $"{reportId}.json");
        if (!File.Exists(jsonPath))
        {
            return "Markdown report available.";
        }

        try
        {
            var report = JsonSerializer.Deserialize<ProjectValidationReport>(File.ReadAllText(jsonPath));
            if (report is null)
            {
                return "Markdown and JSON reports available.";
            }

            var warningCount = report.Findings.Count(item => !item.Status.Equals("ok", StringComparison.OrdinalIgnoreCase));
            return warningCount == 0 ? "Clean validation run." : $"{warningCount} warning(s) recorded.";
        }
        catch
        {
            return "Markdown and JSON reports available.";
        }
    }

    private static void CopyIfExists(string? sourcePath, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(sourcePath, targetPath, true);
    }

    private static ProjectConfig CloneProjectConfigForRoot(ProjectConfig source, string newRootPath)
    {
        return new ProjectConfig
        {
            SchemaVersion = source.SchemaVersion,
            ProjectId = source.ProjectId,
            DisplayName = source.DisplayName,
            RootPath = newRootPath,
            RuntimeSpriteRoot = source.RuntimeSpriteRoot,
            AuthoredSpriteRoot = source.AuthoredSpriteRoot,
            IncomingHandoffRoot = source.IncomingHandoffRoot,
            ArtifactRoot = source.ArtifactRoot,
            ReviewDataPath = source.ReviewDataPath,
            RequestDataPath = source.RequestDataPath,
            CandidateDataPath = source.CandidateDataPath,
            DefaultAiProviderId = source.DefaultAiProviderId,
            AiProviders = source.AiProviders.Select(provider => new AiProviderConfig
            {
                ProviderId = provider.ProviderId,
                DisplayName = provider.DisplayName,
                ProviderKind = provider.ProviderKind,
                ExecutionMode = provider.ExecutionMode,
                SupportsAutomation = provider.SupportsAutomation,
                Notes = provider.Notes,
            }).ToArray(),
            VariantAxes = new VariantAxesConfig
            {
                Species = source.VariantAxes.Species.ToArray(),
                Age = source.VariantAxes.Age.ToArray(),
                Gender = source.VariantAxes.Gender.ToArray(),
                Color = source.VariantAxes.Color.ToArray(),
            },
            Families = source.Families.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Select(sequence => new AnimationSequenceConfig
                {
                    SequenceId = sequence.SequenceId,
                    FrameCount = sequence.FrameCount,
                }).ToArray(),
                StringComparer.OrdinalIgnoreCase),
            WorkflowActions = source.WorkflowActions.Select(action => new WorkflowActionConfig
            {
                ActionId = action.ActionId,
                DisplayName = action.DisplayName,
                Description = action.Description,
                ExecutionMode = action.ExecutionMode,
                Command = action.Command,
                Arguments = action.Arguments.ToArray(),
                WorkingDirectory = action.WorkingDirectory,
            }).ToArray(),
        };
    }

    private string BuildProjectKitReadme(ProjectConfig config, string configTargetPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Project Kit");
        builder.AppendLine();
        builder.AppendLine($"- Project: {config.DisplayName}");
        builder.AppendLine($"- Project Id: {config.ProjectId}");
        builder.AppendLine($"- Root Path: {config.RootPath}");
        builder.AppendLine($"- Exported: {DateTimeOffset.Now:yyyy-MM-dd h:mm:ss tt}");
        builder.AppendLine();
        builder.AppendLine("## Included");
        builder.AppendLine();
        builder.AppendLine($"- Project profile: {Path.GetFileName(configTargetPath)}");
        builder.AppendLine("- Review/request/candidate stores when available");
        builder.AppendLine("- Latest validation report when available");
        builder.AppendLine("- Planning checklist when available");
        builder.AppendLine("- Project palettes and trusted export history when available");
        builder.AppendLine();
        builder.AppendLine("## Use");
        builder.AppendLine();
        builder.AppendLine("1. Open the project profile in the Sprite Workflow App.");
        builder.AppendLine("2. Review the latest validation report.");
        builder.AppendLine("3. Use Planning and Review to continue onboarding or production work.");
        return builder.ToString();
    }

    private List<ProjectPaletteRecord> LoadProjectPaletteRecords()
    {
        if (string.IsNullOrWhiteSpace(_projectPaletteStorePath) || !File.Exists(_projectPaletteStorePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ProjectPaletteRecord>>(File.ReadAllText(_projectPaletteStorePath))
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void PersistProjectPalettes()
    {
        if (string.IsNullOrWhiteSpace(_projectPaletteStorePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_projectPaletteStorePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            _projectPaletteStorePath,
            JsonSerializer.Serialize(
                ProjectPalettes
                    .Select(item => new ProjectPaletteRecord(
                        item.PaletteId,
                        item.Name,
                        item.ScopeKind,
                        item.ScopeKey,
                        item.Colors.ToList(),
                        item.UpdatedUtc))
                    .ToList(),
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private string BuildProjectPaletteName(string scopeKind)
    {
        if (scopeKind.Equals("species", StringComparison.OrdinalIgnoreCase) && SelectedBaseVariant is not null)
        {
            return $"{SelectedBaseVariant.Species}-palette";
        }

        if (SelectedBaseVariant is null)
        {
            return $"project-palette-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        }

        return $"{SelectedBaseVariant.Species}-{SelectedBaseVariant.Age}-{SelectedBaseVariant.Gender}-{SelectedViewerFamily}-palette";
    }

    private string BuildProjectPaletteScopeKey(string scopeKind)
    {
        if (scopeKind.Equals("species", StringComparison.OrdinalIgnoreCase))
        {
            return SelectedBaseVariant?.Species ?? string.Empty;
        }

        return !string.IsNullOrWhiteSpace(_projectConfigPath)
            ? Path.GetFileNameWithoutExtension(_projectConfigPath)
            : "project";
    }

    private List<string> ExtractPaletteFromCurrentContext()
    {
        if (_editorPixels.Length > 0)
        {
            return ExtractPaletteHexes(_editorPixels);
        }

        if (!string.IsNullOrWhiteSpace(ViewerFramePath) && File.Exists(ViewerFramePath))
        {
            return ExtractPaletteHexes(LoadPixelsFromImage(ViewerFramePath));
        }

        if (!string.IsNullOrWhiteSpace(RuntimeViewerFramePath) && File.Exists(RuntimeViewerFramePath))
        {
            return ExtractPaletteHexes(LoadPixelsFromImage(RuntimeViewerFramePath));
        }

        return [];
    }

    private static List<string> ExtractPaletteHexes(IReadOnlyList<Rgba32> pixels)
    {
        return pixels
            .Where(pixel => pixel.A > 0)
            .GroupBy(pixel => ToHexColor(pixel), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .Select(group => group.Key)
            .ToList();
    }

    private static Rgba32[] LoadPixelsFromImage(string imagePath)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imagePath);
        var pixels = new Rgba32[image.Width * image.Height];
        image.CopyPixelDataTo(pixels);
        return pixels;
    }

    private void ReplaceSavedPalette(IEnumerable<string> colors)
    {
        SavedEditorPalette.Clear();
        foreach (var color in colors
                     .Where(color => !string.IsNullOrWhiteSpace(color))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Take(32))
        {
            var parsedColor = TryParseHexColor(color, out var rgba) ? rgba : new Rgba32(255, 255, 255, 255);
            SavedEditorPalette.Add(new EditorPaletteColorItemViewModel(color, CreateBrush(parsedColor), color.Equals(SelectedEditorColorHex, StringComparison.OrdinalIgnoreCase)));
        }

        UpdatePaletteSelection();
        OnPropertyChanged(nameof(SavedPaletteSummary));
    }

    private List<PlanningTemplateRecord> LoadPlanningTemplateRecords()
    {
        if (string.IsNullOrWhiteSpace(_planningTemplateStorePath) || !File.Exists(_planningTemplateStorePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PlanningTemplateRecord>>(File.ReadAllText(_planningTemplateStorePath))
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SavePlanningTemplateRecords(List<PlanningTemplateRecord> templates)
    {
        if (string.IsNullOrWhiteSpace(_planningTemplateStorePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_planningTemplateStorePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            _planningTemplateStorePath,
            JsonSerializer.Serialize(templates, new JsonSerializerOptions { WriteIndented = true }));
    }

    private TrustedExportSnapshot BuildTrustedExportSnapshot()
    {
        var approvedEntries = new List<TrustedExportEntry>();
        var approvedFrameCount = 0;
        var templateOnlyFrameCount = 0;
        var flaggedFrameCount = 0;
        var approvedRowCount = _allBaseVariants.Count(row => row.ReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase));

        foreach (var row in _allBaseVariants)
        {
            foreach (var color in ViewerColorOptions)
            {
                foreach (var family in _familyOrder)
                {
                    if (!_familySequences.TryGetValue(family, out var sequences))
                    {
                        continue;
                    }

                    foreach (var sequence in sequences)
                    {
                        for (var frameIndex = 0; frameIndex < sequence.FrameCount; frameIndex++)
                        {
                            var frameId = $"{sequence.SequenceId}_{frameIndex:00}";
                            var framePath = BuildAuthoredFramePath(row.Species, row.Age, row.Gender, color, frameId);
                            var frameKey = BuildFrameReviewKey(row.Species, row.Age, row.Gender, color, family, sequence.SequenceId, frameIndex);
                            var frameReview = _frameReviewLookup.TryGetValue(frameKey, out var review) ? review : null;
                            var statusValue = string.IsNullOrWhiteSpace(frameReview?.Status) ? row.ReviewStatus : frameReview!.Status;
                            if (string.IsNullOrWhiteSpace(statusValue))
                            {
                                statusValue = "unreviewed";
                            }

                            if (statusValue.Equals("approved", StringComparison.OrdinalIgnoreCase) && File.Exists(framePath))
                            {
                                approvedFrameCount++;
                                approvedEntries.Add(new TrustedExportEntry(
                                    row.Species,
                                    row.Age,
                                    row.Gender,
                                    color,
                                    family,
                                    sequence.SequenceId,
                                    frameIndex,
                                    frameId,
                                    framePath,
                                    statusValue,
                                    frameReview is null ? "row_review" : "frame_review"));
                            }
                            else if (statusValue.Equals("template_only", StringComparison.OrdinalIgnoreCase))
                            {
                                templateOnlyFrameCount++;
                            }
                            else if (!statusValue.Equals("unreviewed", StringComparison.OrdinalIgnoreCase))
                            {
                                flaggedFrameCount++;
                            }
                        }
                    }
                }
            }
        }

        return new TrustedExportSnapshot(approvedFrameCount, templateOnlyFrameCount, flaggedFrameCount, approvedRowCount, approvedEntries);
    }

    private string BuildTrustedExportBlockerSummary()
    {
        var blockers = new List<string>();
        foreach (var row in _allBaseVariants.OrderBy(item => item.Species).ThenBy(item => item.Age).ThenBy(item => item.Gender))
        {
            var reasons = new List<string>();
            if (!row.ReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add($"row is {row.ReviewStatusLabel.ToLowerInvariant()}");
            }

            if (row.FrameReviewState.Equals("flagged", StringComparison.OrdinalIgnoreCase) ||
                row.FrameReviewState.Equals("mixed", StringComparison.OrdinalIgnoreCase) ||
                row.FrameReviewState.Equals("unreviewed", StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add($"frames are {row.FrameReviewStateLabel.ToLowerInvariant()}");
            }

            if (reasons.Count > 0)
            {
                blockers.Add($"{row.DisplayName}: {string.Join("; ", reasons)}");
            }
        }

        if (blockers.Count == 0)
        {
            return "No blockers. Trusted export is clear to package the current approved set.";
        }

        var preview = string.Join("  |  ", blockers.Take(4));
        return blockers.Count > 4 ? $"{preview}  |  +{blockers.Count - 4} more blocker(s)" : preview;
    }

    private string BuildTrustedExportMarkdown(TrustedExportSnapshot snapshot, int exportedFileCount, string framesDirectory)
    {
        return string.Join(
            Environment.NewLine,
            [
                "# Trusted Export",
                string.Empty,
                $"- Project: {SelectedProjectName}",
                $"- Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}",
                $"- Approved frames: {snapshot.ApprovedFrameCount}",
                $"- Template-only frames: {snapshot.TemplateOnlyFrameCount}",
                $"- Flagged frames: {snapshot.FlaggedFrameCount}",
                $"- Approved rows: {snapshot.ApprovedRowCount}",
                $"- Mirrored PNG files: {exportedFileCount}",
                $"- Frames directory: {framesDirectory}",
                string.Empty,
                "Use this bundle as the trusted implementation source instead of the raw working tree.",
            ]);
    }

    private string GetTrustedExportRelativePath(string framePath)
    {
        if (!string.IsNullOrWhiteSpace(_authoredSpriteRoot) &&
            framePath.StartsWith(_authoredSpriteRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(_authoredSpriteRoot, framePath);
        }

        return Path.GetFileName(framePath);
    }

    private string BuildAuthoredFramePath(string species, string age, string gender, string color, string frameId)
    {
        return Path.Combine(_authoredSpriteRoot, species, age, gender, color, $"{frameId}.png");
    }

    private string BuildRequestPreview()
        => BuildRequestPreview(
            DraftRequestType,
            DraftRequestStatus,
            DraftRequestTargetScope,
            DraftRequestTitle,
            DraftRequestDetails,
            DraftRequestMustPreserve,
            DraftRequestMustAvoid,
            DraftRequestSourceNote);

    private string BuildRequestPreview(RequestItemViewModel request)
        => BuildRequestPreview(
            request.RequestType,
            request.Status,
            request.TargetScope,
            request.Title,
            request.Details,
            request.MustPreserve,
            request.MustAvoid,
            request.SourceNote);

    private static string BuildRequestPreview(
        string requestType,
        string requestStatus,
        string targetScope,
        string title,
        string details,
        string mustPreserve,
        string mustAvoid,
        string sourceNote)
    {
        var lines = new List<string>
        {
            $"type: {requestType}",
            $"status: {requestStatus}",
            $"target: {targetScope}",
            $"title: {title}",
        };

        if (!string.IsNullOrWhiteSpace(details))
        {
            lines.Add($"details: {details}");
        }

        if (!string.IsNullOrWhiteSpace(mustPreserve))
        {
            lines.Add($"must_preserve: {mustPreserve}");
        }

        if (!string.IsNullOrWhiteSpace(mustAvoid))
        {
            lines.Add($"must_avoid: {mustAvoid}");
        }

        if (!string.IsNullOrWhiteSpace(sourceNote))
        {
            lines.Add($"source_note: {sourceNote}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void TryNavigateToTargetScope(string? targetScope)
    {
        if (string.IsNullOrWhiteSpace(targetScope))
        {
            return;
        }

        var parts = targetScope
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return;
        }

        var species = parts[0];
        var age = parts[1];
        var gender = parts[2];
        var variant = _allBaseVariants.FirstOrDefault(row =>
            row.Species.Equals(species, StringComparison.OrdinalIgnoreCase) &&
            row.Age.Equals(age, StringComparison.OrdinalIgnoreCase) &&
            row.Gender.Equals(gender, StringComparison.OrdinalIgnoreCase));
        if (variant is null)
        {
            return;
        }

        SelectedBaseVariant = variant;

        if (parts.Length >= 4 && ViewerColorOptions.Contains(parts[3]))
        {
            SelectedViewerColor = parts[3];
        }

        if (parts.Length >= 5 && ViewerFamilyOptions.Contains(parts[4]))
        {
            SelectedViewerFamily = parts[4];
        }

        if (parts.Length >= 6)
        {
            var frameId = parts[5];
            var underscoreIndex = frameId.LastIndexOf('_');
            if (underscoreIndex > 0)
            {
                var sequenceId = frameId[..underscoreIndex];
                if (ViewerSequenceOptions.Contains(sequenceId))
                {
                    SelectedViewerSequenceId = sequenceId;
                }

                if (int.TryParse(frameId[(underscoreIndex + 1)..], out var frameIndex))
                {
                    _currentFrameIndex = frameIndex;
                    _playbackFrameIndex = frameIndex;
                    UpdateViewer();
                }
            }
        }
    }

    private void RefreshAutomationQueue()
    {
        var selectedRequestId = SelectedAutomationTaskItem?.RequestId;
        AutomationQueueItems.Clear();

        foreach (var request in Requests
                     .Where(request =>
                         request.Status.Equals("queued", StringComparison.OrdinalIgnoreCase) ||
                         request.Status.Equals("ready_for_ai", StringComparison.OrdinalIgnoreCase) ||
                         request.Status.Equals("running", StringComparison.OrdinalIgnoreCase) ||
                         request.Status.Equals("paused", StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(request => request.UpdatedUtc))
        {
            AutomationQueueItems.Add(new AutomationTaskItemViewModel(
                request.RequestId,
                request.Title,
                request.TargetScope,
                request.Status,
                request.RequestType,
                BuildRequestPreview(request),
                request.History.FirstOrDefault()?.Message ?? string.Empty,
                Candidates.Count(candidate => candidate.RequestId.Equals(request.RequestId, StringComparison.OrdinalIgnoreCase)),
                request.UpdatedUtc));
        }

        SelectedAutomationTaskItem = string.IsNullOrWhiteSpace(selectedRequestId)
            ? AutomationQueueItems.FirstOrDefault()
            : AutomationQueueItems.FirstOrDefault(task => task.RequestId.Equals(selectedRequestId, StringComparison.OrdinalIgnoreCase))
                ?? AutomationQueueItems.FirstOrDefault();

        OnPropertyChanged(nameof(AutomationQueueSummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskSummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskPromptSummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskActivitySummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskCandidateSummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskHistorySummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskCandidates));
        OnPropertyChanged(nameof(SelectedAutomationTaskLinkedCandidatesSummary));
        OnPropertyChanged(nameof(AutomationQueueHint));
        NotifyCurrentTaskChanged();
    }

    private bool UpdateRequestStatus(string requestId, string newStatus)
    {
        var index = Requests
            .Select((request, idx) => new { request, idx })
            .FirstOrDefault(entry => entry.request.RequestId.Equals(requestId, StringComparison.OrdinalIgnoreCase));
        if (index is null)
        {
            return false;
        }

        var existing = index.request;
        var updated = existing with
        {
            Status = newStatus,
            UpdatedUtc = DateTimeOffset.UtcNow,
        };

        Requests[index.idx] = updated;
        SelectedRequestItem = updated;
        PersistRequests();
        RefreshAutomationQueue();
        OnPropertyChanged(nameof(RequestSummary));
        OnPropertyChanged(nameof(DraftRequestPreview));
        return true;
    }

    private void RecordSelectedAutomationAttempt(string eventType, string message, string? newStatus)
    {
        if (SelectedAutomationTaskItem is null)
        {
            RequestSaveMessage = "Select an automation task first.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(newStatus))
        {
            UpdateRequestStatus(SelectedAutomationTaskItem.RequestId, newStatus);
        }

        AppendRequestHistory(SelectedAutomationTaskItem.RequestId, eventType, message);
        RequestSaveMessage = $"{SelectedAutomationTaskItem.DisplayName}: {message}";
        AddActivity("automation", $"{SelectedAutomationTaskItem.DisplayName}: {message}");
    }

    private void AppendRequestHistory(string requestId, string eventType, string message)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return;
        }

        var index = Requests
            .Select((request, idx) => new { request, idx })
            .FirstOrDefault(entry => entry.request.RequestId.Equals(requestId, StringComparison.OrdinalIgnoreCase));
        if (index is null)
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var existing = index.request;
        var history = existing.History
            .Prepend(new RequestHistoryItemViewModel(eventType, message, timestamp))
            .Take(32)
            .ToList();
        var updated = existing with
        {
            History = history,
            UpdatedUtc = timestamp,
        };

        Requests[index.idx] = updated;
        if (SelectedRequestItem?.RequestId.Equals(requestId, StringComparison.OrdinalIgnoreCase) == true)
        {
            SelectedRequestItem = updated;
        }

        PersistRequests();
        RefreshAutomationQueue();
        OnPropertyChanged(nameof(RequestSummary));
        OnPropertyChanged(nameof(SelectedRequestHistorySummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskHistorySummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskCandidates));
        OnPropertyChanged(nameof(SelectedAutomationTaskLinkedCandidatesSummary));
        OnPropertyChanged(nameof(DraftRequestPreview));
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
                            History = request.History
                                .Select(entry => new ProjectRequestHistoryRecord
                                {
                                    EventType = entry.EventType,
                                    Message = entry.Message,
                                    UpdatedUtc = entry.UpdatedUtc,
                                })
                                .ToList(),
                            UpdatedUtc = request.UpdatedUtc,
                        })
                      .ToList(),
              });
    }

    private void PersistCandidates()
    {
        if (_saveCandidateData is null)
        {
            return;
        }

        _saveCandidateData(
            new ProjectCandidateData
            {
                Candidates = Candidates
                    .Select(
                        candidate => new ProjectCandidateRecord
                        {
                            CandidateId = candidate.CandidateId,
                            Title = candidate.Title,
                            TargetScope = candidate.TargetScope,
                            SourceType = candidate.SourceType,
                            Status = candidate.Status,
                            RequestId = candidate.RequestId,
                            CandidateImagePath = candidate.CandidateImagePath,
                            ReferenceImagePath = candidate.ReferenceImagePath,
                            TargetFramePath = candidate.TargetFramePath,
                            ImportBackupPath = candidate.ImportBackupPath,
                            Note = candidate.Note,
                            UpdatedUtc = candidate.UpdatedUtc,
                        })
                    .ToList(),
            });
        RefreshAutomationQueue();
        OnPropertyChanged(nameof(CandidateSummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskCandidateSummary));
        OnPropertyChanged(nameof(SelectedAutomationTaskCandidates));
        OnPropertyChanged(nameof(SelectedAutomationTaskLinkedCandidatesSummary));
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
            FrameReviews = _frameReviewLookup.Values
                .Where(record => !string.IsNullOrWhiteSpace(record.Species))
                .OrderBy(record => record.Species)
                .ThenBy(record => record.Age)
                .ThenBy(record => record.Gender)
                .ThenBy(record => record.Color)
                .ThenBy(record => record.Family)
                .ThenBy(record => record.SequenceId)
                .ThenBy(record => record.FrameIndex)
                .Select(
                    record =>
                    {
                        record.IssueTags = ParseFrameIssueTags(string.Join(", ", record.IssueTags ?? [])).ToArray();
                        return record;
                    })
                .ToList(),
        };
    }

    private void OnPreviewTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaybackEnabled)
        {
            return;
        }

        AdvancePlaybackFrame(1);
    }

    private void OnCompareBlinkTimerTick(object? sender, EventArgs e)
    {
        if (!IsBlinkCompareEnabled || !HasCurrentFrameBitmap || !HasRuntimeFrameBitmap)
        {
            return;
        }

        _showRuntimeBlinkFrame = !_showRuntimeBlinkFrame;
        OnPropertyChanged(nameof(BlinkCompareBitmap));
        OnPropertyChanged(nameof(BlinkCompareSummary));
    }

    private void OnEditorBlinkTimerTick(object? sender, EventArgs e)
    {
        if (!IsEditorBlinkCompareEnabled || !HasEditorBaselineBitmap || !IsEditorFrameLoaded)
        {
            return;
        }

        _showEditorBaselineBlinkFrame = !_showEditorBaselineBlinkFrame;
        OnPropertyChanged(nameof(EditorBlinkCompareBitmap));
        OnPropertyChanged(nameof(EditorBlinkCompareSummary));
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
            UpdateAuditProgressSummary();
            RefreshRepairQueue();
            RefreshFrameReviewQueue();
            return;
        }

        SelectedBaseVariant = FilteredBaseVariants.FirstOrDefault();
        OnPropertyChanged(nameof(AssetBrowserSummary));
        UpdateAuditProgressSummary();
        RefreshRepairQueue();
        RefreshFrameReviewQueue();
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
        NotifyCurrentTaskChanged();
    }

    private void RefreshFrameReviewQueue()
    {
        var queueItems = _frameReviewLookup.Values
            .Where(
                record => !record.Status.Equals("approved", StringComparison.OrdinalIgnoreCase) &&
                          (!record.Status.Equals("unreviewed", StringComparison.OrdinalIgnoreCase) ||
                           !string.IsNullOrWhiteSpace(record.Note) ||
                           (record.IssueTags?.Length ?? 0) > 0))
            .OrderBy(record => record.Species)
            .ThenBy(record => record.Age)
            .ThenBy(record => record.Gender)
            .ThenBy(record => record.Color)
            .ThenBy(record => record.Family)
            .ThenBy(record => record.SequenceId)
            .ThenBy(record => record.FrameIndex)
            .Select(
                record => new FrameReviewQueueItemViewModel(
                    record.Species,
                    record.Age,
                    record.Gender,
                    record.Color,
                    record.Family,
                    record.SequenceId,
                    record.FrameIndex,
                    record.FrameId,
                    record.Status,
                    record.Note,
                    record.IssueTags ?? [],
                    record.UpdatedUtc))
            .ToList();

        FrameReviewQueueItems.Clear();
        foreach (var item in queueItems)
        {
            FrameReviewQueueItems.Add(item);
        }

        if (SelectedFrameReviewQueueItem is not null &&
            !queueItems.Any(
                item => item.Species.Equals(SelectedFrameReviewQueueItem.Species, StringComparison.OrdinalIgnoreCase) &&
                        item.Age.Equals(SelectedFrameReviewQueueItem.Age, StringComparison.OrdinalIgnoreCase) &&
                        item.Gender.Equals(SelectedFrameReviewQueueItem.Gender, StringComparison.OrdinalIgnoreCase) &&
                        item.Color.Equals(SelectedFrameReviewQueueItem.Color, StringComparison.OrdinalIgnoreCase) &&
                        item.Family.Equals(SelectedFrameReviewQueueItem.Family, StringComparison.OrdinalIgnoreCase) &&
                        item.SequenceId.Equals(SelectedFrameReviewQueueItem.SequenceId, StringComparison.OrdinalIgnoreCase) &&
                        item.FrameIndex == SelectedFrameReviewQueueItem.FrameIndex))
        {
            _isSynchronizingFrameQueueSelection = true;
            SelectedFrameReviewQueueItem = FrameReviewQueueItems.FirstOrDefault();
            _isSynchronizingFrameQueueSelection = false;
        }

        OnPropertyChanged(nameof(FrameReviewQueueSummary));
        RefreshVariantFrameQuality();
        RefreshTrustedExportBlockers();
        NotifyCurrentTaskChanged();
    }

    private void RefreshVariantFrameQuality()
    {
        var groupedReviews = _frameReviewLookup.Values
            .GroupBy(record => BuildVariantKey(record.Species, record.Age, record.Gender), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var variant in _allBaseVariants)
        {
            if (!groupedReviews.TryGetValue(BuildVariantKey(variant.Species, variant.Age, variant.Gender), out var reviews) || reviews.Count == 0)
            {
                variant.UpdateFrameQuality("unreviewed", 0, 0, 0, 0, "No frame tags yet.");
                continue;
            }

            var reviewedCount = reviews.Count(review =>
                !review.Status.Equals("unreviewed", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(review.Note) ||
                (review.IssueTags?.Length ?? 0) > 0);
            var approvedCount = reviews.Count(review => review.Status.Equals("approved", StringComparison.OrdinalIgnoreCase));
            var templateCount = reviews.Count(review => review.Status.Equals("template_only", StringComparison.OrdinalIgnoreCase));
            var flaggedCount = reviews.Count(review =>
                !review.Status.Equals("approved", StringComparison.OrdinalIgnoreCase) &&
                !review.Status.Equals("template_only", StringComparison.OrdinalIgnoreCase) &&
                (!review.Status.Equals("unreviewed", StringComparison.OrdinalIgnoreCase) ||
                 !string.IsNullOrWhiteSpace(review.Note) ||
                 (review.IssueTags?.Length ?? 0) > 0));
            var issueSummary = string.Join(
                ", ",
                reviews
                    .SelectMany(review => review.IssueTags ?? [])
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .Select(group => $"{group.Key} x{group.Count()}"));

            variant.UpdateFrameQuality(
                BuildFrameReviewState(reviewedCount, approvedCount, flaggedCount, templateCount),
                reviewedCount,
                approvedCount,
                flaggedCount,
                templateCount,
                string.IsNullOrWhiteSpace(issueSummary) ? "No frame tags yet." : issueSummary);
        }

        OnPropertyChanged(nameof(SelectedBaseVariantFrameQualitySummary));
        OnPropertyChanged(nameof(SelectedBaseVariantFrameIssueSummary));
    }

    private static string BuildFrameReviewState(int reviewedCount, int approvedCount, int flaggedCount, int templateCount)
    {
        if (reviewedCount == 0)
        {
            return "unreviewed";
        }

        if (flaggedCount > 0 && (approvedCount > 0 || templateCount > 0))
        {
            return "mixed";
        }

        if (flaggedCount > 0)
        {
            return "flagged";
        }

        if (templateCount > 0 && approvedCount == 0)
        {
            return "template_only";
        }

        return approvedCount > 0 ? "approved" : "unreviewed";
    }

    private void SetSelectedReviewStatus(string status)
    {
        if (SelectedBaseVariant is null)
        {
            return;
        }

        SelectedBaseVariant.ReviewStatus = status;
        AddActivity("review", $"Marked {SelectedBaseVariant.DisplayName} as {status.Replace('_', ' ')}.");
    }

    private void SetCurrentFrameReviewStatus(string status)
    {
        if (!TryGetCurrentFrameReviewContext(out _))
        {
            FrameReviewSaveMessage = "No current frame is selected.";
            return;
        }

        SelectedFrameReviewStatus = status;
        AddActivity("review", $"Marked {ViewerSelectionSummary} as {status.Replace('_', ' ')}.");
    }

    private void LoadCurrentFrameReview()
    {
        var key = BuildCurrentFrameReviewKey();
        _isLoadingFrameReview = true;

        if (string.IsNullOrWhiteSpace(key) || !_frameReviewLookup.TryGetValue(key, out var review))
        {
            SelectedFrameReviewStatus = "unreviewed";
            SelectedFrameReviewNote = string.Empty;
            SelectedFrameIssueTagsText = string.Empty;
            SelectedFrameReviewUpdatedUtc = null;
            FrameReviewSaveMessage = "Frame review changes are saved per project.";
            _isLoadingFrameReview = false;
            OnPropertyChanged(nameof(CurrentFrameReviewSummary));
            OnPropertyChanged(nameof(CurrentFrameReviewUpdatedLabel));
            OnPropertyChanged(nameof(CurrentFrameReviewTargetSummary));
            return;
        }

        SelectedFrameReviewStatus = string.IsNullOrWhiteSpace(review.Status) ? "unreviewed" : review.Status;
        SelectedFrameReviewNote = review.Note ?? string.Empty;
        SelectedFrameIssueTagsText = string.Join(", ", review.IssueTags ?? []);
        SelectedFrameReviewUpdatedUtc = review.UpdatedUtc;
        FrameReviewSaveMessage = "Frame review changes are saved per project.";
        _isLoadingFrameReview = false;
        OnPropertyChanged(nameof(CurrentFrameReviewSummary));
        OnPropertyChanged(nameof(CurrentFrameReviewUpdatedLabel));
        OnPropertyChanged(nameof(CurrentFrameReviewTargetSummary));
    }

    private void ClearCurrentFrameReviewSelection()
    {
        _isLoadingFrameReview = true;
        SelectedFrameReviewStatus = "unreviewed";
        SelectedFrameReviewNote = string.Empty;
        SelectedFrameIssueTagsText = string.Empty;
        SelectedFrameReviewUpdatedUtc = null;
        FrameReviewSaveMessage = "Frame review changes are saved per project.";
        _isLoadingFrameReview = false;
        OnPropertyChanged(nameof(CurrentFrameReviewSummary));
        OnPropertyChanged(nameof(CurrentFrameReviewUpdatedLabel));
        OnPropertyChanged(nameof(CurrentFrameReviewTargetSummary));
    }

    private void StageCandidateFromCurrentFrame(bool useRuntimeTemplate)
    {
        if (_saveCandidateData is null)
        {
            CandidateSaveMessage = "No candidate store configured.";
            return;
        }

        var sourcePath = useRuntimeTemplate ? RuntimeViewerFramePath : ViewerFramePath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath) || !TryGetCurrentFrameReviewContext(out var frameId))
        {
            CandidateSaveMessage = useRuntimeTemplate
                ? "No runtime frame is available to stage."
                : "No authored frame is available to stage.";
            return;
        }

        try
        {
            Directory.CreateDirectory(_candidateAssetDirectory);
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            var rowSlug = SelectedBaseVariant is null
                ? "unscoped"
                : $"{SelectedBaseVariant.Species}-{SelectedBaseVariant.Age}-{SelectedBaseVariant.Gender}".ToLowerInvariant();
            var candidateId = $"{timestamp}-{rowSlug}-{SelectedViewerFamily}-{SelectedViewerSequenceId}-{_currentFrameIndex:00}";
            var candidateDirectory = Path.Combine(_candidateAssetDirectory, candidateId);
            Directory.CreateDirectory(candidateDirectory);

            var candidateImagePath = Path.Combine(candidateDirectory, "candidate.png");
            File.Copy(sourcePath, candidateImagePath, true);

            var referenceSourcePath = useRuntimeTemplate ? ViewerFramePath : RuntimeViewerFramePath;
            var referenceImagePath = CopyReferenceIntoBundle(referenceSourcePath, candidateDirectory, "reference");

            var title = $"{ToTitleCase(SelectedBaseVariant!.Species)} {ToTitleCase(SelectedBaseVariant.Age)} {ToTitleCase(SelectedBaseVariant.Gender)} {SelectedViewerFamily} {frameId}";
            var targetScope = $"{SelectedBaseVariant.Species} | {SelectedBaseVariant.Age} | {SelectedBaseVariant.Gender} | {SelectedViewerColor} | {SelectedViewerFamily} | {frameId}";
            var candidate = new CandidateItemViewModel(
                candidateId,
                title,
                targetScope,
                useRuntimeTemplate ? "runtime_template" : "authored_snapshot",
                "staged",
                SelectedRequestItem?.RequestId ?? string.Empty,
                candidateImagePath,
                referenceImagePath,
                ViewerFramePath,
                string.Empty,
                useRuntimeTemplate
                    ? "Staged from the runtime template for side-by-side review before manual edits or import."
                    : "Staged from the authored slot for compare/review before approval or replacement.",
                DateTimeOffset.UtcNow);

            Candidates.Insert(0, candidate);
            SelectedCandidateItem = candidate;
            PersistCandidates();
            CandidateSaveMessage = $"Staged candidate '{candidate.Title}'.";
            OnPropertyChanged(nameof(CandidateSummary));
            AddActivity("candidate", $"Staged {candidate.SourceTypeLabel} for {frameId}.");
        }
        catch (Exception ex)
        {
            CandidateSaveMessage = $"Unable to stage candidate: {ex.Message}";
            AddActivity("candidate", $"Candidate staging failed: {ex.Message}");
        }
    }

    private void SetSelectedCandidateStatus(string status)
    {
        if (SelectedCandidateItem is null)
        {
            CandidateSaveMessage = "Select a candidate first.";
            return;
        }

        SelectedCandidateItem.Status = status;
        SelectedCandidateItem.UpdatedUtc = DateTimeOffset.UtcNow;
        PersistCandidates();
        if (!string.IsNullOrWhiteSpace(SelectedCandidateItem.RequestId))
        {
            AppendRequestHistory(SelectedCandidateItem.RequestId, "candidate_status", $"Marked candidate '{SelectedCandidateItem.Title}' as {status.Replace('_', ' ')}.");
        }
        CandidateSaveMessage = $"Marked candidate '{SelectedCandidateItem.Title}' as {status.Replace('_', ' ')}.";
        OnPropertyChanged(nameof(CandidateSummary));
        OnPropertyChanged(nameof(SelectedCandidateSummary));
        AddActivity("candidate", $"Marked '{SelectedCandidateItem.Title}' as {status.Replace('_', ' ')}.");
    }

    private void LoadSelectedCandidatePreview(CandidateItemViewModel? candidate)
    {
        SelectedCandidateBitmap = LoadBitmapSafe(candidate?.CandidateImagePath);
        SelectedCandidateReferenceBitmap = LoadBitmapSafe(candidate?.ReferenceImagePath);
        OnPropertyChanged(nameof(HasSelectedCandidateBitmap));
        OnPropertyChanged(nameof(IsSelectedCandidateBitmapMissing));
        OnPropertyChanged(nameof(HasSelectedCandidateReferenceBitmap));
        OnPropertyChanged(nameof(IsSelectedCandidateReferenceBitmapMissing));
    }

    private Bitmap? LoadBitmapSafe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
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
            _playbackFrameIndex = 0;
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

    private void AdvancePlaybackFrame(int delta)
    {
        var sequence = GetSelectedSequence();
        if (sequence is null || sequence.FrameCount <= 0)
        {
            return;
        }

        _playbackFrameIndex = (_playbackFrameIndex + delta) % sequence.FrameCount;
        if (_playbackFrameIndex < 0)
        {
            _playbackFrameIndex += sequence.FrameCount;
        }

        UpdatePlaybackPreview();
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

    private bool EnsureCurrentFrameReadyForTransfer(string currentPath)
    {
        if (!IsEditorDirty)
        {
            return true;
        }

        if (!_editorSaveFramePath.Equals(currentPath, StringComparison.OrdinalIgnoreCase))
        {
            EditorStatusMessage = "Save or revert the currently edited frame before using frame helpers.";
            return false;
        }

        SaveEditedFrame();
        return !IsEditorDirty;
    }

    private bool TryGetCurrentAuthoredFrameTargetPath(out string path)
    {
        return TryGetAuthoredFramePathForOffset(0, out path);
    }

    private (int Width, int Height) GetPreferredCanvasSizeForCurrentSlot()
    {
        if (_editorWidth > 0 && _editorHeight > 0)
        {
            return (_editorWidth, _editorHeight);
        }

        foreach (var candidatePath in EnumerateCanvasSizeCandidates())
        {
            if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
            {
                continue;
            }

            try
            {
                var imageInfo = SixLabors.ImageSharp.Image.Identify(candidatePath);
                if (imageInfo is not null && imageInfo.Width > 0 && imageInfo.Height > 0)
                {
                    return (imageInfo.Width, imageInfo.Height);
                }
            }
            catch
            {
                // Ignore invalid candidates and keep searching.
            }
        }

        return (64, 64);
    }

    private IEnumerable<string> EnumerateCanvasSizeCandidates()
    {
        if (!string.IsNullOrWhiteSpace(RuntimeViewerFramePath))
        {
            yield return RuntimeViewerFramePath;
        }

        if (!string.IsNullOrWhiteSpace(ViewerFramePath))
        {
            yield return ViewerFramePath;
        }

        if (!string.IsNullOrWhiteSpace(_editorSaveFramePath))
        {
            yield return _editorSaveFramePath;
        }

        if (SelectedBaseVariant is null || string.IsNullOrWhiteSpace(SelectedViewerColor))
        {
            yield break;
        }

        var authoredDir = Path.Combine(_authoredSpriteRoot, SelectedBaseVariant.Species, SelectedBaseVariant.Age, SelectedBaseVariant.Gender, SelectedViewerColor);
        var runtimeDir = Path.Combine(_runtimeSpriteRoot, SelectedBaseVariant.Species, SelectedBaseVariant.Age, SelectedBaseVariant.Gender, SelectedViewerColor);

        if (Directory.Exists(authoredDir))
        {
            foreach (var path in Directory.EnumerateFiles(authoredDir, "*.png").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }

        if (Directory.Exists(runtimeDir))
        {
            foreach (var path in Directory.EnumerateFiles(runtimeDir, "*.png").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }
    }

    private static void WriteBlankFrame(string outputPath, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
        using var image = new Image<Rgba32>(Math.Max(1, width), Math.Max(1, height));
        image.Save(outputPath);
    }

    private bool TryGetAuthoredFramePathForOffset(int offset, out string path)
    {
        path = string.Empty;
        var sequence = GetSelectedSequence();
        if (SelectedBaseVariant is null ||
            sequence is null ||
            string.IsNullOrWhiteSpace(SelectedViewerColor) ||
            sequence.FrameCount <= 0)
        {
            return false;
        }

        var slot = ((_currentFrameIndex + offset) % sequence.FrameCount + sequence.FrameCount) % sequence.FrameCount;
        var frameId = $"{sequence.SequenceId}_{slot:00}";
        path = Path.Combine(
            _authoredSpriteRoot,
            SelectedBaseVariant.Species,
            SelectedBaseVariant.Age,
            SelectedBaseVariant.Gender,
            SelectedViewerColor,
            $"{frameId}.png");
        return true;
    }

    private void UpdateViewer()
    {
        DisposeViewerFrames();
        ViewerFrames.Clear();

        if (SelectedBaseVariant is null)
        {
            CurrentFrameBitmap = null;
            OnionSkinBitmap = null;
            RuntimeFrameBitmap = null;
            PreviousFrameReferenceBitmap = null;
            NextFrameReferenceBitmap = null;
            PlaybackFrameBitmap = null;
            RuntimePlaybackFrameBitmap = null;
            ClearCurrentFrameReviewSelection();
            ViewerStatusMessage = "Select a row to preview authored animation frames.";
            ViewerFrameSummary = "No frame loaded.";
            ViewerMissingSummary = string.Empty;
            ViewerFramePath = string.Empty;
            RuntimeViewerFrameSummary = "No runtime frame loaded.";
            RuntimeViewerFramePath = string.Empty;
            OnPropertyChanged(nameof(PlaybackSelectionSummary));
            OnPropertyChanged(nameof(LivePlaybackSummary));
            OnPropertyChanged(nameof(LivePlaybackActionSummary));
            OnPropertyChanged(nameof(RuntimePlaybackSummary));
            OnPropertyChanged(nameof(PreviousFrameReferenceSummary));
            OnPropertyChanged(nameof(NextFrameReferenceSummary));
            RefreshBlinkCompareState();
            return;
        }

        var sequence = GetSelectedSequence();
        if (sequence is null)
        {
            CurrentFrameBitmap = null;
            OnionSkinBitmap = null;
            RuntimeFrameBitmap = null;
            PreviousFrameReferenceBitmap = null;
            NextFrameReferenceBitmap = null;
            PlaybackFrameBitmap = null;
            RuntimePlaybackFrameBitmap = null;
            ClearCurrentFrameReviewSelection();
            ViewerStatusMessage = "Choose a family and sequence to inspect this row.";
            ViewerFrameSummary = "No frame loaded.";
            ViewerMissingSummary = string.Empty;
            ViewerFramePath = string.Empty;
            RuntimeViewerFrameSummary = "No runtime frame loaded.";
            RuntimeViewerFramePath = string.Empty;
            OnPropertyChanged(nameof(PlaybackSelectionSummary));
            OnPropertyChanged(nameof(LivePlaybackSummary));
            OnPropertyChanged(nameof(LivePlaybackActionSummary));
            OnPropertyChanged(nameof(RuntimePlaybackSummary));
            OnPropertyChanged(nameof(PreviousFrameReferenceSummary));
            OnPropertyChanged(nameof(NextFrameReferenceSummary));
            RefreshBlinkCompareState();
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedViewerColor))
        {
            CurrentFrameBitmap = null;
            OnionSkinBitmap = null;
            RuntimeFrameBitmap = null;
            PreviousFrameReferenceBitmap = null;
            NextFrameReferenceBitmap = null;
            PlaybackFrameBitmap = null;
            RuntimePlaybackFrameBitmap = null;
            ClearCurrentFrameReviewSelection();
            ViewerStatusMessage = "Choose a color variant to preview.";
            ViewerFrameSummary = "No frame loaded.";
            ViewerMissingSummary = string.Empty;
            ViewerFramePath = string.Empty;
            RuntimeViewerFrameSummary = "No runtime frame loaded.";
            RuntimeViewerFramePath = string.Empty;
            OnPropertyChanged(nameof(PlaybackSelectionSummary));
            OnPropertyChanged(nameof(LivePlaybackSummary));
            OnPropertyChanged(nameof(LivePlaybackActionSummary));
            OnPropertyChanged(nameof(RuntimePlaybackSummary));
            OnPropertyChanged(nameof(PreviousFrameReferenceSummary));
            OnPropertyChanged(nameof(NextFrameReferenceSummary));
            RefreshBlinkCompareState();
            return;
        }

        var authoredDir = Path.Combine(_authoredSpriteRoot, SelectedBaseVariant.Species, SelectedBaseVariant.Age, SelectedBaseVariant.Gender, SelectedViewerColor);
        var runtimeDir = Path.Combine(_runtimeSpriteRoot, SelectedBaseVariant.Species, SelectedBaseVariant.Age, SelectedBaseVariant.Gender, SelectedViewerColor);
        var currentSlot = ((_currentFrameIndex % sequence.FrameCount) + sequence.FrameCount) % sequence.FrameCount;
        var authoredCount = 0;
        var missingNames = new List<string>();
        string? selectedAuthoredPath = null;
        string? previousAuthoredPath = null;
        string? nextAuthoredPath = null;
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
            else if (index == ((currentSlot - 1 + sequence.FrameCount) % sequence.FrameCount))
            {
                previousAuthoredPath = authoredPath;
            }
            else if (index == ((currentSlot + 1) % sequence.FrameCount))
            {
                nextAuthoredPath = authoredPath;
            }

            Bitmap? thumbnailBitmap = null;
            var thumbnailPath = authoredExists ? authoredPath : runtimeExists ? runtimePath : null;
            if (!string.IsNullOrWhiteSpace(thumbnailPath))
            {
                try
                {
                    thumbnailBitmap = new Bitmap(thumbnailPath);
                }
                catch
                {
                    thumbnailBitmap = null;
                }
            }

            var frameReviewKey = BuildFrameReviewKey(
                SelectedBaseVariant.Species,
                SelectedBaseVariant.Age,
                SelectedBaseVariant.Gender,
                SelectedViewerColor,
                SelectedViewerFamily,
                sequence.SequenceId,
                index);
            _frameReviewLookup.TryGetValue(frameReviewKey, out var frameReview);
            var issueTagSummary = string.Join(", ", frameReview?.IssueTags ?? []);

            ViewerFrames.Add(new ViewerFrameItemViewModel(
                frameId,
                index,
                authoredExists,
                runtimeExists,
                index == currentSlot,
                index == _playbackFrameIndex,
                frameReview?.Status ?? "unreviewed",
                issueTagSummary,
                thumbnailBitmap));
        }

        ViewerStatusMessage = $"{ToTitleCase(SelectedBaseVariant.Species)} - {SelectedViewerColor} - {SelectedViewerFamily}/{sequence.SequenceId}";
        ViewerFrameSummary = currentFrameExists ? $"Showing authored {selectedFrameId} ({currentSlot + 1}/{sequence.FrameCount})" : $"Authored frame {selectedFrameId} is missing ({currentSlot + 1}/{sequence.FrameCount})";
        ViewerMissingSummary = missingNames.Count == 0 ? $"All {sequence.FrameCount} authored frames are present." : $"Present {authoredCount}/{sequence.FrameCount}. Missing: {string.Join(", ", missingNames.Take(4))}{(missingNames.Count > 4 ? ", ..." : string.Empty)}";
        ViewerFramePath = selectedAuthoredPath ?? string.Empty;
        OnPropertyChanged(nameof(ViewerSelectionSummary));

        var selectedRuntimePath = Path.Combine(runtimeDir, $"{selectedFrameId}.png");
        RuntimeViewerFramePath = selectedRuntimePath;
        RuntimeViewerFrameSummary = File.Exists(selectedRuntimePath) ? $"Runtime {selectedFrameId} is available." : $"Runtime {selectedFrameId} is missing.";

        if (!currentFrameExists || string.IsNullOrWhiteSpace(selectedAuthoredPath))
        {
            CurrentFrameBitmap = null;
            OnionSkinBitmap = null;
        }
        else
        {
            try
            {
                CurrentFrameBitmap = new Bitmap(selectedAuthoredPath);
                if (IsOnionSkinEnabled && !string.IsNullOrWhiteSpace(previousAuthoredPath) && File.Exists(previousAuthoredPath))
                {
                    OnionSkinBitmap = new Bitmap(previousAuthoredPath);
                }
                else
                {
                    OnionSkinBitmap = null;
                }
            }
            catch
            {
                CurrentFrameBitmap = null;
                OnionSkinBitmap = null;
                ViewerFrameSummary = $"Unable to load authored {selectedFrameId}.";
            }
        }

        if (!string.IsNullOrWhiteSpace(previousAuthoredPath) && File.Exists(previousAuthoredPath))
        {
            try
            {
                PreviousFrameReferenceBitmap = new Bitmap(previousAuthoredPath);
            }
            catch
            {
                PreviousFrameReferenceBitmap = null;
            }
        }
        else
        {
            PreviousFrameReferenceBitmap = null;
        }

        if (!string.IsNullOrWhiteSpace(nextAuthoredPath) && File.Exists(nextAuthoredPath))
        {
            try
            {
                NextFrameReferenceBitmap = new Bitmap(nextAuthoredPath);
            }
            catch
            {
                NextFrameReferenceBitmap = null;
            }
        }
        else
        {
            NextFrameReferenceBitmap = null;
        }

        if (!File.Exists(selectedRuntimePath))
        {
            RuntimeFrameBitmap = null;
            OnPropertyChanged(nameof(PreviousFrameReferenceSummary));
            OnPropertyChanged(nameof(NextFrameReferenceSummary));
            LoadCurrentFrameReview();
            RefreshBlinkCompareState();
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

        OnPropertyChanged(nameof(PreviousFrameReferenceSummary));
        OnPropertyChanged(nameof(NextFrameReferenceSummary));
        UpdatePlaybackPreview();
        LoadCurrentFrameReview();
        RefreshBlinkCompareState();
    }

    private void RefreshBlinkCompareState()
    {
        if (!(HasCurrentFrameBitmap && HasRuntimeFrameBitmap))
        {
            _compareBlinkTimer.Stop();
            _showRuntimeBlinkFrame = false;
        }
        else if (IsBlinkCompareEnabled)
        {
            _compareBlinkTimer.Start();
        }

        OnPropertyChanged(nameof(BlinkCompareBitmap));
        OnPropertyChanged(nameof(BlinkCompareSummary));
    }

    private void UpdatePlaybackPreview()
    {
        var sequence = GetSelectedSequence();
        if (SelectedBaseVariant is null ||
            sequence is null ||
            string.IsNullOrWhiteSpace(SelectedViewerColor))
        {
            PlaybackFrameBitmap = null;
            RuntimePlaybackFrameBitmap = null;
            OnPropertyChanged(nameof(PlaybackSelectionSummary));
            OnPropertyChanged(nameof(LivePlaybackSummary));
            OnPropertyChanged(nameof(LivePlaybackActionSummary));
            OnPropertyChanged(nameof(RuntimePlaybackSummary));
            OnPropertyChanged(nameof(HasPlaybackFrameBitmap));
            OnPropertyChanged(nameof(IsPlaybackFrameBitmapMissing));
            OnPropertyChanged(nameof(HasRuntimePlaybackFrameBitmap));
            OnPropertyChanged(nameof(IsRuntimePlaybackFrameBitmapMissing));
            return;
        }

        _playbackFrameIndex = ((_playbackFrameIndex % sequence.FrameCount) + sequence.FrameCount) % sequence.FrameCount;
        var playbackFrameId = $"{sequence.SequenceId}_{_playbackFrameIndex:00}";
        var authoredPath = Path.Combine(_authoredSpriteRoot, SelectedBaseVariant.Species, SelectedBaseVariant.Age, SelectedBaseVariant.Gender, SelectedViewerColor, $"{playbackFrameId}.png");
        var runtimePath = Path.Combine(_runtimeSpriteRoot, SelectedBaseVariant.Species, SelectedBaseVariant.Age, SelectedBaseVariant.Gender, SelectedViewerColor, $"{playbackFrameId}.png");

        if (File.Exists(authoredPath))
        {
            try
            {
                PlaybackFrameBitmap = new Bitmap(authoredPath);
            }
            catch
            {
                PlaybackFrameBitmap = null;
            }
        }
        else
        {
            PlaybackFrameBitmap = null;
        }

        if (File.Exists(runtimePath))
        {
            try
            {
                RuntimePlaybackFrameBitmap = new Bitmap(runtimePath);
            }
            catch
            {
                RuntimePlaybackFrameBitmap = null;
            }
        }
        else
        {
            RuntimePlaybackFrameBitmap = null;
        }

        OnPropertyChanged(nameof(PlaybackSelectionSummary));
        OnPropertyChanged(nameof(LivePlaybackSummary));
        OnPropertyChanged(nameof(LivePlaybackActionSummary));
        OnPropertyChanged(nameof(RuntimePlaybackSummary));
        OnPropertyChanged(nameof(HasPlaybackFrameBitmap));
        OnPropertyChanged(nameof(IsPlaybackFrameBitmapMissing));
        OnPropertyChanged(nameof(HasRuntimePlaybackFrameBitmap));
        OnPropertyChanged(nameof(IsRuntimePlaybackFrameBitmapMissing));
    }

    private void DisposeViewerFrames()
    {
        foreach (var frame in ViewerFrames)
        {
            frame.Dispose();
        }
    }
}

file sealed record WorkspaceState(
    string ControlMode,
    int WorkspaceTabIndex,
    int StudioTabIndex,
    string PlanningProjectId,
    string PlanningDisplayName,
    string PlanningRootPath,
    string PlanningExportPath,
    string PlanningSpeciesText,
    string PlanningAgeText,
    string PlanningGenderText,
    string PlanningColorText,
    string PlanningFamilyBlueprintText,
    string? Species,
    string? Age,
    string? Gender,
    string ViewerColor,
    string ViewerFamily,
    string ViewerSequenceId,
    int FrameIndex,
    string EditorSaveTargetPath,
    string? EditorDraftPath,
    bool EditorWasDirty);

internal sealed record PlanningAssetChecklistEntry(
    string Species,
    string Age,
    string Gender,
    string Color,
    string Family,
    string SequenceId,
    int FrameCount,
    string VariantDirectory,
    IReadOnlyList<string> FrameFiles);

internal sealed record TrustedExportEntry(
    string Species,
    string Age,
    string Gender,
    string Color,
    string Family,
    string SequenceId,
    int FrameIndex,
    string FrameId,
    string FramePath,
    string Status,
    string Source);

internal sealed record TrustedExportSnapshot(
    int ApprovedFrameCount,
    int TemplateOnlyFrameCount,
    int FlaggedFrameCount,
    int ApprovedRowCount,
    IReadOnlyList<TrustedExportEntry> ApprovedEntries);

internal sealed record PlanningTemplateRecord(
    string TemplateId,
    string Name,
    string SpeciesText,
    string AgeText,
    string GenderText,
    string ColorText,
    string FamilyBlueprintText,
    DateTimeOffset? UpdatedUtc);

internal sealed record ProjectPaletteRecord(
    string PaletteId,
    string Name,
    string? ScopeKind,
    string? ScopeKey,
    List<string> Colors,
    DateTimeOffset? UpdatedUtc);

internal sealed record TrustedExportHistoryRecord(
    string ExportId,
    string ExportDirectory,
    int ApprovedFrameCount,
    int ApprovedRowCount,
    int FlaggedFrameCount,
    DateTimeOffset? ExportedUtc);

internal sealed record ProjectValidationFinding(
    string Id,
    string Status,
    string Summary,
    string OkMessage,
    string WarningMessage);

internal sealed record ProjectValidationReport(
    string ProjectName,
    string RootPath,
    string AuthoredRoot,
    string RuntimeRoot,
    string WorkflowRoot,
    DateTimeOffset GeneratedUtc,
    string Summary,
    IReadOnlyList<ProjectValidationFinding> Findings,
    int RequestCount,
    int CandidateCount,
    int FlaggedFrameCount,
    int TrustedExportCount,
    int PlanningChecklistCount,
    int PlanningAdoptionCount);

internal sealed record DiscoveredPlanningBlueprint(
    IReadOnlyList<string> Species,
    IReadOnlyList<string> Ages,
    IReadOnlyList<string> Genders,
    IReadOnlyList<string> Colors,
    string FamilyBlueprintText);
