using System.Diagnostics;
using OllamaSharp;

var systemPrompt =
"""
You are Fabs, a senior software engineer. Your role is to review code before it is pushed to a repository. Focus on identifying:  
1. Potential security issues (e.g., buffer overflows, SQL/XSS injections, etc.).  
2. Logic and correctness issues.  
3. Code quality concerns (readability, maintainability, best practices).  
Provide a summary table clear, actionable feedback with examples when possible. If there are no errors or possible issues then simply state there aren't any.
""";

var ollama = new OllamaApiClient(new OllamaApiClient.Configuration
{
    Uri = new Uri("http://localhost:11434"),
    Model = "qwen2.5-coder:3b",
});

static string GetGitDiff()
{
    // Try staged diff first, fall back to unstaged
    foreach (var args in new[] { "diff --cached", "diff" })
    {
        var psi = new ProcessStartInfo("git", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        using var process = Process.Start(psi) ?? throw new Exception("Failed to start git process");
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new Exception($"Git error: {process.StandardError.ReadToEnd()}");

        if (!string.IsNullOrWhiteSpace(output))
            return output;
    }

    return string.Empty;
}

static IEnumerable<string> GetProjectFiles(string workingDirectory)
{
    // Check if this is actually a git repo first
    if (!Directory.Exists(Path.Combine(workingDirectory, ".git")))
    {
        // Fall back to all files if not a git repo
        return Directory.EnumerateFiles(workingDirectory, "*", SearchOption.AllDirectories);
    }
    var psi = new ProcessStartInfo("git", "ls-files --cached --others --exclude-standard -z")
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    using var process = Process.Start(psi);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
    using var reader = process.StandardOutput;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

    // -z flag uses null terminator for paths with spaces/newlines
    var output = reader.ReadToEnd();
    process.WaitForExit();
    return output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
        .Select(file => Path.Combine(workingDirectory, file))
        .Where(File.Exists); // Safety check
}


var command = args.ElementAtOrDefault(0);

if (string.IsNullOrWhiteSpace(command))
{
    Console.Error.WriteLine("""
    Expected command a command:
        init - creates a context memory of the project
        review - reviews the current diff for errors, logic issues, etc.
    """);
}
else if (command == "init")
{
    Console.WriteLine("This process may take some time");
    var files = GetProjectFiles(Directory.GetCurrentDirectory()).ToList();
    var contents = files.Select(File.ReadAllText).ToList();

    var chat = new Chat(ollama, "You will analyze the file contents and create a project overview and dependency map. Your output will be regarded as a text file so do not make commentary or suggesstions. Treat your output as documentation.");

    await foreach (var token in chat.SendAsync(string.Join("\n", contents)))
    {
        Console.Write(token);
    }
}
else if (command == "review")
{
    string diff = GetGitDiff();

    if (string.IsNullOrWhiteSpace(diff))
    {
        Console.WriteLine("No git diff found (nothing staged or modified).");
        return;
    }

    var chat = new Chat(ollama, systemPrompt);

    await foreach (var token in chat.SendAsync(diff))
        Console.Write(token);
}
else
{
    Console.Error.WriteLine($"""
    Unexpected command '{command}':
        init - creates a context memory of the project
        review - reviews the current diff for errors, logic issues, etc.
    """);
}