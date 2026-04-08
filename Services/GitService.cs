using System.Diagnostics;

namespace FabsReview;

internal sealed class GitService
{
    private readonly string _workingDirectory;
    public GitService(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
        if (!Directory.Exists(Path.Combine(workingDirectory, ".git")))
        {
            throw new NotSupportedException($"[GitServices] failed to detect an existing repository in: {workingDirectory}");
        }
    }
    private async Task<string> RunGit(string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            Arguments = string.Join(" ", args),
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Git process");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var waitTask = process.WaitForExitAsync(cts.Token);
            try
            {
                await Task.WhenAll(outputTask, errorTask, waitTask);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Process timed out.");
            }

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"[GitService] failed to run `git {string.Join(" ", args)}`: {error}");
            }

            return output;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            throw; // let this panic... no point of this tool if we don't got git
        }

        throw new UnreachableException("[GitService] RunGit has somehow reached here?");
    }
}