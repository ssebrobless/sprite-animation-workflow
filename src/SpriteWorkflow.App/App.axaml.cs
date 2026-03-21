using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpriteWorkflow.App.Services;
using SpriteWorkflow.App.ViewModels;
using SpriteWorkflow.App.Views;
using SpriteWorkflow.Infrastructure;

namespace SpriteWorkflow.App;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private static readonly object DiagnosticLogGate = new();
    private sealed record ProjectSession(
        MainWindowViewModel ViewModel,
        JsonProjectReviewStore ReviewStore,
        string ReviewPath,
        JsonProjectRequestStore RequestStore,
        string RequestPath,
        JsonProjectCandidateStore CandidateStore,
        string CandidatePath,
        string LiveOpsPath);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\SpriteWorkflowApp", out var createdNew);
            if (!createdNew)
            {
                desktop.Shutdown();
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                base.OnFrameworkInitializationCompleted();
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                {
                    AppendDiagnosticLog("AppDomain.CurrentDomain.UnhandledException", exception);
                }
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                AppendDiagnosticLog("TaskScheduler.UnobservedTaskException", args.Exception);
                args.SetObserved();
            };

            Dispatcher.UIThread.UnhandledException += (_, args) =>
            {
                AppendDiagnosticLog("Dispatcher.UIThread.UnhandledException", args.Exception);
                args.Handled = true;
            };

            var workflowRunner = new WorkflowProcessRunner();
            var session = CreateProjectSession(workflowRunner);
            var viewModel = session.ViewModel;
            var mainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            var watcherContext = AttachProjectFileWatchers(session, mainWindow);
            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) =>
            {
                watcherContext.Dispose();
                workflowRunner.Dispose();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ProjectSession CreateProjectSession(IWorkflowProcessRunner workflowRunner)
    {
        var configPath = string.Empty;

        try
        {
            var repoRoot = FindRepositoryRoot();
            configPath = Path.Combine(repoRoot, "sample-projects", "wevito.project.json");

            var configService = new JsonProjectConfigService();
            var indexService = new FileSystemAssetIndexService();
            var reviewStore = new JsonProjectReviewStore();
            var requestStore = new JsonProjectRequestStore();
            var candidateStore = new JsonProjectCandidateStore();
            var config = configService.Load(configPath);
            var snapshot = indexService.BuildSnapshot(config);
            var reviewPath = ResolveProjectPath(config.RootPath, config.ReviewDataPath);
            var requestPath = ResolveProjectPath(config.RootPath, config.RequestDataPath);
            var candidatePath = ResolveProjectPath(config.RootPath, config.CandidateDataPath);
            var reviewData = reviewStore.Load(reviewPath);
            var requestData = requestStore.Load(requestPath);
            var candidateData = candidateStore.Load(candidatePath);

            return new ProjectSession(
                new MainWindowViewModel(
                    config,
                    snapshot,
                    reviewData,
                    reviewPath,
                    data => reviewStore.Save(reviewPath, data),
                    requestData,
                    requestPath,
                    data => requestStore.Save(requestPath, data),
                    candidateData,
                    candidatePath,
                    data => candidateStore.Save(candidatePath, data),
                    workflowRunner,
                    configPath,
                    null),
                reviewStore,
                reviewPath,
                requestStore,
                requestPath,
                candidateStore,
                candidatePath,
                Path.Combine(Path.GetDirectoryName(reviewPath) ?? Path.Combine(config.RootPath, ".sprite-workflow"), "live-ops.jsonl"));
        }
        catch (Exception ex)
        {
            return new ProjectSession(
                new MainWindowViewModel(null, null, null, string.Empty, null, null, string.Empty, null, null, string.Empty, null, workflowRunner, configPath, ex.Message),
                new JsonProjectReviewStore(),
                string.Empty,
                new JsonProjectRequestStore(),
                string.Empty,
                new JsonProjectCandidateStore(),
                string.Empty,
                string.Empty);
        }
    }

    private static IDisposable AttachProjectFileWatchers(ProjectSession session, MainWindow mainWindow)
    {
        if (string.IsNullOrWhiteSpace(session.ReviewPath) &&
            string.IsNullOrWhiteSpace(session.RequestPath) &&
            string.IsNullOrWhiteSpace(session.CandidatePath) &&
            string.IsNullOrWhiteSpace(session.LiveOpsPath))
        {
            return DisposableAction.Empty;
        }

        var watchers = new List<FileSystemWatcher>();
        var debounceGate = new object();
        CancellationTokenSource? debounceCts = null;
        var lastLiveOpsWriteUtc = DateTime.MinValue;

        void ActivateMainWindow()
        {
            try
            {
                if (mainWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
                {
                    mainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                }

                mainWindow.Activate();
            }
            catch
            {
                // Visibility is best-effort here; do not break live processing.
            }
        }

        void ScheduleReload()
        {
            lock (debounceGate)
            {
                debounceCts?.Cancel();
                debounceCts?.Dispose();
                debounceCts = new CancellationTokenSource();
                var token = debounceCts.Token;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(325, token);
                        token.ThrowIfCancellationRequested();

                        var reviewData = string.IsNullOrWhiteSpace(session.ReviewPath)
                            ? null
                            : TryLoad(() => session.ReviewStore.Load(session.ReviewPath));
                        var requestData = string.IsNullOrWhiteSpace(session.RequestPath)
                            ? null
                            : TryLoad(() => session.RequestStore.Load(session.RequestPath));
                        var candidateData = string.IsNullOrWhiteSpace(session.CandidatePath)
                            ? null
                            : TryLoad(() => session.CandidateStore.Load(session.CandidatePath));

                        Dispatcher.UIThread.Post(() =>
                        {
                            if (reviewData is not null || !string.IsNullOrWhiteSpace(session.ReviewPath))
                            {
                                session.ViewModel.ReloadExternalReviewData(reviewData);
                            }

                            if (requestData is not null || !string.IsNullOrWhiteSpace(session.RequestPath))
                            {
                                session.ViewModel.ReloadExternalRequestData(requestData);
                            }

                            if (candidateData is not null || !string.IsNullOrWhiteSpace(session.CandidatePath))
                            {
                                session.ViewModel.ReloadExternalCandidateData(candidateData);
                            }
                        });
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        AppendDiagnosticLog("ProjectFileWatcher.ScheduleReload", ex);
                    }
                }, token);
            }
        }

        AddWatcher(session.ReviewPath, watchers, ScheduleReload);
        AddWatcher(session.RequestPath, watchers, ScheduleReload);
        AddWatcher(session.CandidatePath, watchers, ScheduleReload);

        var liveOpsPollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        liveOpsPollTimer.Tick += (_, _) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(session.LiveOpsPath) || !File.Exists(session.LiveOpsPath))
                {
                    return;
                }

                var writeUtc = File.GetLastWriteTimeUtc(session.LiveOpsPath);
                if (writeUtc <= lastLiveOpsWriteUtc)
                {
                    return;
                }

                lastLiveOpsWriteUtc = writeUtc;
                var lines = File.ReadAllLines(session.LiveOpsPath);
                ActivateMainWindow();
                session.ViewModel.ApplyLiveOperationLines(lines);
            }
            catch (Exception ex)
            {
                AppendDiagnosticLog("ProjectFileWatcher.LiveOpsPoll", ex);
            }
        };
        liveOpsPollTimer.Start();

        return new DisposableAction(() =>
        {
            lock (debounceGate)
            {
                debounceCts?.Cancel();
                debounceCts?.Dispose();
                debounceCts = null;
            }

            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }

            liveOpsPollTimer.Stop();
        });
    }

    private static T? TryLoad<T>(Func<T> loader) where T : class
    {
        try
        {
            return loader();
        }
        catch (IOException)
        {
            Thread.Sleep(100);
            return loader();
        }
    }

    private static void AddWatcher(string filePath, ICollection<FileSystemWatcher> watchers, Action scheduleReload)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        Directory.CreateDirectory(directory);

        var watcher = new FileSystemWatcher(directory, fileName)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        FileSystemEventHandler onFileChanged = (_, _) => scheduleReload();
        RenamedEventHandler onFileRenamed = (_, _) => scheduleReload();
        watcher.Changed += onFileChanged;
        watcher.Created += onFileChanged;
        watcher.Renamed += onFileRenamed;
        watchers.Add(watcher);
    }

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action? _dispose;
        public static DisposableAction Empty { get; } = new(null);

        public DisposableAction(Action? dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            _dispose?.Invoke();
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "SpriteWorkflow.sln");
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the SpriteWorkflow repository root.");
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private static string ResolveProjectPath(string rootPath, string relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            return Path.Combine(rootPath, ".sprite-workflow", "reviews.json");
        }

        return Path.IsPathRooted(relativeOrAbsolutePath)
            ? relativeOrAbsolutePath
            : Path.GetFullPath(Path.Combine(rootPath, relativeOrAbsolutePath));
    }

    public static void AppendDiagnosticLog(string context, Exception exception)
    {
        try
        {
            var repoRoot = FindRepositoryRoot();
            var logDirectory = Path.Combine(repoRoot, "artifacts", "logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "sprite-workflow-app-errors.log");
            var lines = new[]
            {
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {context}",
                exception.ToString(),
                string.Empty,
            };

            lock (DiagnosticLogGate)
            {
                File.AppendAllLines(logPath, lines);
            }
        }
        catch
        {
            // Best effort logging only.
        }
    }
}
