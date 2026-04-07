using System.Diagnostics;
using System.Text;
using System.Text.Json;

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

using HttpClient client = new HttpClient();

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
    Console.WriteLine("TODO: implement init command");
}
else if (command == "review")
{
    string diff = GetGitDiff();

    if (string.IsNullOrWhiteSpace(diff))
    {
        Console.WriteLine("No git diff found (nothing staged or modified).");
        return;
    }

    var payload = new
    {
        model = "qwen2.5-coder:3b",
        prompt =
            $"""
            Please review the following git diff:
            ```diff
            {diff}
            ```
            """,
        system =
            """
            You are Fabs, a senior software engineer. Your role is to review code before it is pushed to a repository. Focus on identifying:  
            1. Potential security issues (e.g., buffer overflows, SQL/XSS injections, etc.).  
            2. Logic and correctness issues.  
            3. Code quality concerns (readability, maintainability, best practices).  
            Provide a summary table clear, actionable feedback with examples when possible. If there are no errors or possible issues then simply state there aren't any.
            """,
    };

    string json = JsonSerializer.Serialize(payload);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    var response = await client.PostAsync("http://localhost:11434/api/generate", content);
    using var stream = await response.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(stream);

    string? line;
    while ((line = await reader.ReadLineAsync()) != null)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            var obj = JsonSerializer.Deserialize<JsonElement>(line);
            Console.Write(obj.GetProperty("response").GetString());
        }
    }
}
else
{
    Console.Error.WriteLine($"""
    Unexpected command '{command}':
        init - creates a context memory of the project
        review - reviews the current diff for errors, logic issues, etc.
    """);
}