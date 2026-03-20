using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpriteWorkflow.App.Services;

public interface IWorkflowProcessRunner : IDisposable
{
    Task<WorkflowActionRunResult> RunHiddenAsync(WorkflowActionLaunchRequest request, CancellationToken cancellationToken = default);
    bool TryStop(string actionId);
    int StopAll();
}

public sealed record WorkflowActionLaunchRequest(
    string ActionId,
    string DisplayName,
    string Command,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);

public sealed record WorkflowActionRunResult(int ExitCode, string Output, bool WasStopped);

public sealed class WorkflowProcessRunner : IWorkflowProcessRunner
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Process> _runningProcesses = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDisposed;

    public async Task<WorkflowActionRunResult> RunHiddenAsync(WorkflowActionLaunchRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var process = CreateProcess(request);
        lock (_gate)
        {
            if (_runningProcesses.ContainsKey(request.ActionId))
            {
                throw new InvalidOperationException($"Action '{request.DisplayName}' is already running.");
            }

            _runningProcesses[request.ActionId] = process;
        }

        var outputBuilder = new StringBuilder();
        var stopRequested = false;

        try
        {
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var registration = cancellationToken.Register(() =>
            {
                stopRequested = true;
                SafeKill(process);
            });

            await process.WaitForExitAsync().ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                outputBuilder.AppendLine(stdout.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                if (outputBuilder.Length > 0)
                {
                    outputBuilder.AppendLine();
                }

                outputBuilder.AppendLine(stderr.TrimEnd());
            }

            return new WorkflowActionRunResult(process.ExitCode, outputBuilder.ToString().Trim(), stopRequested);
        }
        finally
        {
            lock (_gate)
            {
                _runningProcesses.Remove(request.ActionId);
            }

            process.Dispose();
        }
    }

    public bool TryStop(string actionId)
    {
        lock (_gate)
        {
            if (!_runningProcesses.TryGetValue(actionId, out var process))
            {
                return false;
            }

            SafeKill(process);
            return true;
        }
    }

    public int StopAll()
    {
        lock (_gate)
        {
            var stopped = 0;
            foreach (var process in _runningProcesses.Values)
            {
                SafeKill(process);
                stopped++;
            }

            return stopped;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        lock (_gate)
        {
            foreach (var process in _runningProcesses.Values)
            {
                SafeKill(process);
            }

            _runningProcesses.Clear();
        }
    }

    private static Process CreateProcess(WorkflowActionLaunchRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.Command,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = false,
        };
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(WorkflowProcessRunner));
        }
    }

    private static void SafeKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort shutdown for hidden workflow processes.
        }
    }
}
