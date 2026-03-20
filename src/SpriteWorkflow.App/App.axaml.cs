using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
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
            var viewModel = CreateMainWindowViewModel(workflowRunner);
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            desktop.Exit += (_, _) =>
            {
                workflowRunner.Dispose();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static MainWindowViewModel CreateMainWindowViewModel(IWorkflowProcessRunner workflowRunner)
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

            return new MainWindowViewModel(
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
                null);
        }
        catch (Exception ex)
        {
            return new MainWindowViewModel(null, null, null, string.Empty, null, null, string.Empty, null, null, string.Empty, null, workflowRunner, configPath, ex.Message);
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
