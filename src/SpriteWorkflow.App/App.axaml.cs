using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System;
using System.IO;
using System.Linq;
using SpriteWorkflow.App.ViewModels;
using SpriteWorkflow.App.Views;
using SpriteWorkflow.Infrastructure;

namespace SpriteWorkflow.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var viewModel = CreateMainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static MainWindowViewModel CreateMainWindowViewModel()
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
            var config = configService.Load(configPath);
            var snapshot = indexService.BuildSnapshot(config);
            var reviewPath = ResolveProjectPath(config.RootPath, config.ReviewDataPath);
            var requestPath = ResolveProjectPath(config.RootPath, config.RequestDataPath);
            var reviewData = reviewStore.Load(reviewPath);
            var requestData = requestStore.Load(requestPath);

            return new MainWindowViewModel(
                config,
                snapshot,
                reviewData,
                reviewPath,
                data => reviewStore.Save(reviewPath, data),
                requestData,
                requestPath,
                data => requestStore.Save(requestPath, data),
                configPath,
                null);
        }
        catch (Exception ex)
        {
            return new MainWindowViewModel(null, null, null, string.Empty, null, null, string.Empty, null, configPath, ex.Message);
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
}
