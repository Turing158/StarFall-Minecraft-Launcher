using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace StarFallMC.Util;

/// <summary>
/// Starts owned child processes. Each invocation creates a separate
/// <see cref="ProcessRun"/>; output is never shared between commands.
/// </summary>
public sealed class ProcessRunner
{
    public ProcessRunner(int outputCapacity = 1000, int maxLineLength = 16 * 1024)
    {
        if (outputCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outputCapacity));
        }

        if (maxLineLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLineLength));
        }

        OutputCapacity = outputCapacity;
        MaxLineLength = maxLineLength;
    }

    public int OutputCapacity { get; }
    public int MaxLineLength { get; }

    public ProcessRun Start(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        var run = new ProcessRun(startInfo, OutputCapacity, MaxLineLength);
        try
        {
            run.Start(cancellationToken);
            return run;
        }
        catch
        {
            run.Dispose();
            throw;
        }
    }

    public ProcessRun Start(
        string fileName,
        string? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            Arguments = arguments ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        return Start(startInfo, cancellationToken);
    }
}

/// <summary>
/// Owns one process, its output readers, cancellation registration and
/// completion task. The output snapshot is bounded and thread-safe.
/// </summary>
public sealed class ProcessRun : IDisposable, IAsyncDisposable
{
    private static readonly Regex SensitiveValuePattern = new(
        @"(?i)(access[_-]?token|refresh[_-]?token|authorization)(\s*[:=]\s*|\s+)([^\s]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly object sync = new();
    private readonly ProcessStartInfo startInfo;
    private readonly string[] outputRing;
    private readonly int maxLineLength;
    private readonly TaskCompletionSource<ProcessResult> completionSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration cancellationRegistration;
    private Process? process;
    private int nextOutputIndex;
    private int outputCount;
    private int completionStarted;
    private int disposed;
    private bool readersStarted;

    internal ProcessRun(ProcessStartInfo startInfo, int outputCapacity, int maxLineLength)
    {
        this.startInfo = CloneStartInfo(startInfo);
        outputRing = new string[outputCapacity];
        this.maxLineLength = maxLineLength;
    }

    public Process Process => process ?? throw new InvalidOperationException("The process has not started.");
    public Task<ProcessResult> Completion => completionSource.Task;
    public bool HasExited => process?.HasExited ?? true;

    public IReadOnlyList<string> OutputSnapshot
    {
        get
        {
            lock (sync)
            {
                var snapshot = new string[outputCount];
                int start = (nextOutputIndex - outputCount + outputRing.Length) % outputRing.Length;
                for (int i = 0; i < outputCount; i++)
                {
                    snapshot[i] = outputRing[(start + i) % outputRing.Length];
                }

                return new ReadOnlyCollection<string>(snapshot);
            }
        }
    }

    public void Start(CancellationToken cancellationToken = default)
    {
        if (process != null)
        {
            throw new InvalidOperationException("A process run can only be started once.");
        }

        var createdProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        createdProcess.OutputDataReceived += OnOutputDataReceived;
        createdProcess.ErrorDataReceived += OnErrorDataReceived;
        createdProcess.Exited += OnExited;
        process = createdProcess;

        try
        {
            if (!createdProcess.Start())
            {
                throw new InvalidOperationException("The process did not start.");
            }

            cancellationRegistration = cancellationToken.Register(static state => ((ProcessRun)state!).Cancel(), this);
            if (startInfo.RedirectStandardOutput || startInfo.RedirectStandardError)
            {
                readersStarted = true;
                if (startInfo.RedirectStandardOutput)
                {
                    createdProcess.BeginOutputReadLine();
                }

                if (startInfo.RedirectStandardError)
                {
                    createdProcess.BeginErrorReadLine();
                }
            }

            if (createdProcess.HasExited)
            {
                _ = CompleteAfterExitAsync();
            }
        }
        catch (Exception exception)
        {
            CompleteWithError(exception);
            throw;
        }
    }

    public async Task<ProcessResult> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        if (process == null)
        {
            throw new InvalidOperationException("The process has not started.");
        }

        try
        {
            return await Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Cancel();
            try
            {
                await Completion.ConfigureAwait(false);
            }
            catch
            {
                // The caller's cancellation remains the observable outcome.
            }
            throw;
        }
    }

    public void Cancel()
    {
        var current = process;
        if (current == null)
        {
            return;
        }

        try
        {
            if (!current.HasExited)
            {
                current.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between HasExited and Kill.
        }
        catch (NotSupportedException)
        {
            // Some platforms do not support process-tree termination. The
            // process is still awaited and disposed below.
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        cancellationRegistration.Dispose();
        var current = process;
        if (current != null)
        {
            try
            {
                if (!current.HasExited)
                {
                    current.Kill(entireProcessTree: true);
                    current.WaitForExit();
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (NotSupportedException)
            {
            }

            DetachProcessHandlers(current);
            current.Dispose();
        }

        if (!completionSource.Task.IsCompleted)
        {
            completionSource.TrySetCanceled();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        cancellationRegistration.Dispose();
        Cancel();
        try
        {
            if (process != null)
            {
                await Completion.ConfigureAwait(false);
            }
        }
        catch
        {
            // Disposal must release the process even after a failed start/read.
        }

        var current = process;
        if (current != null)
        {
            DetachProcessHandlers(current);
            current.Dispose();
        }
    }

    private void OnOutputDataReceived(object? sender, DataReceivedEventArgs e) => AddOutput(e.Data);
    private void OnErrorDataReceived(object? sender, DataReceivedEventArgs e) => AddOutput(e.Data);

    private void AddOutput(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        line = SensitiveValuePattern.Replace(line, "$1$2[redacted]");

        if (line.Length > maxLineLength)
        {
            line = line[..maxLineLength] + "… [truncated]";
        }

        lock (sync)
        {
            outputRing[nextOutputIndex] = line;
            nextOutputIndex = (nextOutputIndex + 1) % outputRing.Length;
            outputCount = Math.Min(outputCount + 1, outputRing.Length);
        }
    }

    private void OnExited(object? sender, EventArgs e) => _ = CompleteAfterExitAsync();

    private async Task CompleteAfterExitAsync()
    {
        if (Interlocked.Exchange(ref completionStarted, 1) != 0)
        {
            return;
        }

        try
        {
            var current = process;
            if (current != null)
            {
                // WaitForExit() also waits for asynchronous output events to drain.
                await Task.Run(current.WaitForExit).ConfigureAwait(false);
                if (readersStarted)
                {
                    try
                    {
                        current.CancelOutputRead();
                        current.CancelErrorRead();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                completionSource.TrySetResult(new ProcessResult(current.ExitCode, OutputSnapshot));
            }
            else
            {
                completionSource.TrySetException(new InvalidOperationException("The process was not created."));
            }
        }
        catch (Exception exception)
        {
            CompleteWithError(exception);
        }
    }

    private void CompleteWithError(Exception exception)
    {
        if (Interlocked.Exchange(ref completionStarted, 1) == 0)
        {
            completionSource.TrySetException(exception);
        }
    }

    private void DetachProcessHandlers(Process current)
    {
        current.OutputDataReceived -= OnOutputDataReceived;
        current.ErrorDataReceived -= OnErrorDataReceived;
        current.Exited -= OnExited;
    }

    private static ProcessStartInfo CloneStartInfo(ProcessStartInfo source)
    {
        var clone = new ProcessStartInfo
        {
            FileName = source.FileName,
            Arguments = source.Arguments,
            WorkingDirectory = source.WorkingDirectory,
            UseShellExecute = source.UseShellExecute,
            CreateNoWindow = source.CreateNoWindow,
            WindowStyle = source.WindowStyle,
            RedirectStandardInput = source.RedirectStandardInput,
            RedirectStandardOutput = source.RedirectStandardOutput,
            RedirectStandardError = source.RedirectStandardError,
            StandardOutputEncoding = source.StandardOutputEncoding,
            StandardErrorEncoding = source.StandardErrorEncoding
        };

        foreach (var variable in source.Environment)
        {
            clone.Environment[variable.Key] = variable.Value;
        }

        return clone;
    }
}

public sealed record ProcessResult(int ExitCode, IReadOnlyList<string> Output);
