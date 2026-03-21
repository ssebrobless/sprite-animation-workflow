using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SpriteWorkflow.App.ViewModels;

namespace SpriteWorkflow.App.Views;

public partial class MainWindow : Window
{
    private bool _isEditorPainting;
    private MainWindowViewModel? _observedViewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnMainWindowDataContextChanged;
    }

    private void OnMainWindowDataContextChanged(object? sender, EventArgs e)
    {
        if (_observedViewModel is not null)
        {
            _observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _observedViewModel = DataContext as MainWindowViewModel;
        if (_observedViewModel is not null)
        {
            _observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The main shell is now fixed-height and workspace-first, so there is no global scroll surface to reset.
    }

    private void OnEditorPixelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                sender is Control { Tag: EditorPixelItemViewModel pixel } &&
                DataContext is MainWindowViewModel viewModel)
            {
                if (viewModel.SelectedEditorTool.Equals("select", StringComparison.OrdinalIgnoreCase) ||
                    viewModel.SelectedEditorTool.Equals("lasso", StringComparison.OrdinalIgnoreCase))
                {
                    _isEditorPainting = true;
                    viewModel.BeginEditorSelection(pixel);
                }
                else if (viewModel.SelectedEditorTool.Equals("move", StringComparison.OrdinalIgnoreCase))
                {
                    _isEditorPainting = viewModel.BeginEditorMove(pixel);
                }
                else
                {
                    _isEditorPainting = true;
                    viewModel.BeginEditorStroke();
                    viewModel.ApplyEditorTool(pixel);
                }

                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            ReportUiException("Editor pointer press failed.", ex);
        }
    }

    private void OnEditorPixelPointerEntered(object? sender, PointerEventArgs e)
    {
        try
        {
            if (!_isEditorPainting ||
                sender is not Control { Tag: EditorPixelItemViewModel pixel } ||
                DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isEditorPainting = false;
                viewModel.EndEditorStroke();
                viewModel.EndEditorSelection();
                return;
            }

            if (viewModel.SelectedEditorTool.Equals("select", StringComparison.OrdinalIgnoreCase) ||
                viewModel.SelectedEditorTool.Equals("lasso", StringComparison.OrdinalIgnoreCase))
            {
                viewModel.UpdateEditorSelection(pixel);
            }
            else if (viewModel.SelectedEditorTool.Equals("move", StringComparison.OrdinalIgnoreCase))
            {
                viewModel.UpdateEditorMove(pixel);
            }
            else
            {
                viewModel.ApplyEditorTool(pixel);
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            ReportUiException("Editor pointer move failed.", ex);
        }
    }

    private void OnEditorPixelPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.EndEditorStroke();
                viewModel.EndEditorSelection();
                viewModel.NotifyEditorStrokeCompleted();
            }

            _isEditorPainting = false;
        }
        catch (Exception ex)
        {
            ReportUiException("Editor pointer release failed.", ex);
        }
    }

    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.EndEditorStroke();
                viewModel.EndEditorSelection();
                viewModel.NotifyEditorStrokeCompleted();
            }

            _isEditorPainting = false;
        }
        catch (Exception ex)
        {
            ReportUiException("Window pointer release failed.", ex);
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel viewModel || IsTextEditingTarget(e.Source))
            {
                return;
            }

            var modifiers = e.KeyModifiers;
            var isCtrlOnly = modifiers.HasFlag(KeyModifiers.Control) && !modifiers.HasFlag(KeyModifiers.Shift) && !modifiers.HasFlag(KeyModifiers.Alt);
            var isCtrlShift = modifiers.HasFlag(KeyModifiers.Control) && modifiers.HasFlag(KeyModifiers.Shift) && !modifiers.HasFlag(KeyModifiers.Alt);
            var isCtrlAlt = modifiers.HasFlag(KeyModifiers.Control) && modifiers.HasFlag(KeyModifiers.Alt) && !modifiers.HasFlag(KeyModifiers.Shift);
            var isAltOnly = modifiers.HasFlag(KeyModifiers.Alt) && !modifiers.HasFlag(KeyModifiers.Control) && !modifiers.HasFlag(KeyModifiers.Shift);
            var hasNoModifiers = modifiers == KeyModifiers.None;

            if (isCtrlOnly)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        viewModel.SelectPreviousVariantCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.Right:
                        viewModel.SelectNextVariantCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.D1:
                    case Key.NumPad1:
                        viewModel.MarkSelectedApprovedCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.D2:
                    case Key.NumPad2:
                        viewModel.MarkSelectedNeedsReviewCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.D3:
                    case Key.NumPad3:
                        viewModel.MarkSelectedToBeRepairedCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.C:
                        viewModel.CopyEditorSelectionCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.V:
                        viewModel.PasteEditorSelectionCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.Z:
                        viewModel.UndoEditorEditCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.Y:
                        viewModel.RedoEditorEditCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.OemPlus:
                    case Key.Add:
                        viewModel.AdjustEditorZoomShortcut(1);
                        e.Handled = true;
                        return;
                    case Key.OemMinus:
                    case Key.Subtract:
                        viewModel.AdjustEditorZoomShortcut(-1);
                        e.Handled = true;
                        return;
                }
            }

            if (isCtrlShift)
            {
                switch (e.Key)
                {
                    case Key.Right:
                        viewModel.SelectNextNeedsReviewVariantCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.Z:
                        viewModel.RedoEditorEditCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.H:
                        viewModel.FlipSelectionHorizontalCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.V:
                        viewModel.FlipSelectionVerticalCommand.Execute(null);
                        e.Handled = true;
                        return;
                }
            }

            if (!hasNoModifiers)
            {
                if (isCtrlAlt)
                {
                    switch (e.Key)
                    {
                        case Key.Left:
                            viewModel.CopyPreviousFrameIntoCurrentCommand.Execute(null);
                            e.Handled = true;
                            return;
                        case Key.Right:
                            viewModel.DuplicateCurrentFrameToNextCommand.Execute(null);
                            e.Handled = true;
                            return;
                        case Key.Up:
                            viewModel.SwapCurrentFrameWithPreviousCommand.Execute(null);
                            e.Handled = true;
                            return;
                    }
                }

                if (isAltOnly)
                {
                    switch (e.Key)
                    {
                        case Key.Left:
                            viewModel.MoveSelectionLeftCommand.Execute(null);
                            e.Handled = true;
                            return;
                        case Key.Right:
                            viewModel.MoveSelectionRightCommand.Execute(null);
                            e.Handled = true;
                            return;
                        case Key.Up:
                            viewModel.MoveSelectionUpCommand.Execute(null);
                            e.Handled = true;
                            return;
                        case Key.Down:
                            viewModel.MoveSelectionDownCommand.Execute(null);
                            e.Handled = true;
                            return;
                    }
                }

                return;
            }

            switch (e.Key)
            {
                case Key.Delete:
                case Key.Back:
                    viewModel.DeleteEditorSelectionCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.OemOpenBrackets:
                    viewModel.StepPreviousFrameCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.OemCloseBrackets:
                    viewModel.StepNextFrameCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.OemPipe:
                    viewModel.RestartPreviewCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.B:
                    viewModel.SelectEditorToolShortcut("brush");
                    e.Handled = true;
                    return;
                case Key.A:
                    viewModel.SelectEditorToolShortcut("lasso");
                    e.Handled = true;
                    return;
                case Key.E:
                    viewModel.SelectEditorToolShortcut("erase");
                    e.Handled = true;
                    return;
                case Key.I:
                    viewModel.SelectEditorToolShortcut("dropper");
                    e.Handled = true;
                    return;
                case Key.F:
                    viewModel.SelectEditorToolShortcut("fill");
                    e.Handled = true;
                    return;
                case Key.L:
                    viewModel.SelectEditorToolShortcut("line");
                    e.Handled = true;
                    return;
                case Key.R:
                    viewModel.SelectEditorToolShortcut("rectangle");
                    e.Handled = true;
                    return;
                case Key.O:
                    viewModel.SelectEditorToolShortcut("ellipse");
                    e.Handled = true;
                    return;
                case Key.H:
                    viewModel.ToggleEditorMirrorHorizontalShortcut();
                    e.Handled = true;
                    return;
                case Key.V:
                    viewModel.ToggleEditorMirrorVerticalShortcut();
                    e.Handled = true;
                    return;
                case Key.M:
                    viewModel.SelectEditorToolShortcut("select");
                    e.Handled = true;
                    return;
                case Key.G:
                    viewModel.SelectEditorToolShortcut("move");
                    e.Handled = true;
                    return;
            }
        }
        catch (Exception ex)
        {
            ReportUiException("Window key handling failed.", ex);
        }
    }

    private void ReportUiException(string context, Exception ex)
    {
        _isEditorPainting = false;
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.EditorStatusMessage = $"{context} See diagnostics log for details.";
            viewModel.ViewerStatusMessage = "The UI hit an error. Check the diagnostics log and reload the affected frame.";
        }

        App.AppendDiagnosticLog(context, ex);
    }

    private static bool IsTextEditingTarget(object? source)
    {
        var current = source as Interactive;
        while (current is not null)
        {
            if (current is TextBox or ComboBox or Slider)
            {
                return true;
            }

            current = current.Parent as Interactive;
        }

        return false;
    }
}
