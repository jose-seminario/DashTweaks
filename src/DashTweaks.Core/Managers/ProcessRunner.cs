using System.Diagnostics;

namespace DashTweaks.Core.Managers;

public sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr, bool TimedOut);

/// <summary>
/// Ejecuta procesos ocultos leyendo stdout/stderr en paralelo (evita deadlocks de pipe)
/// y aplicando un timeout real: si se agota, el proceso se termina.
/// </summary>
public static class ProcessRunner
{
    public static ProcessRunResult Run(ProcessStartInfo psi, int timeoutMs)
    {
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"No se pudo iniciar {psi.FileName}.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new ProcessRunResult(-1, string.Empty, string.Empty, TimedOut: true);
        }

        Task.WaitAll(new Task[] { stdoutTask, stderrTask }, 3000);
        string stdout = stdoutTask.IsCompletedSuccessfully ? stdoutTask.GetAwaiter().GetResult() : string.Empty;
        string stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.GetAwaiter().GetResult() : string.Empty;

        return new ProcessRunResult(process.ExitCode, stdout, stderr, TimedOut: false);
    }
}
