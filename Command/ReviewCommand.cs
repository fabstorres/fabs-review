using OllamaSharp;
namespace FabsReview.Command;
internal static class ReviewCommand
{
    private const string ReviewSystemPrompt =
    """
    You are Fabs, a senior software engineer. Your role is to review code before it is pushed to a repository.
    Focus on identifying:
    1. Potential security issues (e.g., buffer overflows, SQL/XSS injections, etc.).
    2. Logic and correctness issues.
    3. Code quality concerns (readability, maintainability, best practices).
    Use repository context when it is provided, but keep the review grounded in the supplied git diff.
    Provide a summary table and clear, actionable feedback with examples when possible.
    If there are no errors or possible issues then simply state there aren't any.
    """;

    private const string ContextUpdateSystemPrompt =
    """
    You maintain the repository context document stored in .fabs/context.md.
    Update the document to reflect the supplied git diff while preserving durable repository knowledge that is still true.
    Remove details that are no longer accurate, merge overlapping information, and return markdown only.
    The response will overwrite .fabs/context.md, so output the full document contents and nothing else.
    """;

    public static async Task RunAsync(GitService git, OllamaApiClient ollama, string workingDirectory)
    {
        string diff = await git.GetRawDiffAsync();

        if (string.IsNullOrWhiteSpace(diff))
        {
            Console.WriteLine("No git diff found (nothing staged or modified).");
            return;
        }

        var contextPath = GetContextPath(workingDirectory);
        var existingContext = await TryReadContextAsync(contextPath);
        var reviewChat = new Chat(ollama, ReviewSystemPrompt);
        var reviewPrompt = BuildReviewPrompt(diff, existingContext);

        await foreach (var token in reviewChat.SendAsync(reviewPrompt))
        {
            Console.Write(token);
        }

        if (existingContext is null)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("Updating .fabs/context.md...");

        var updateChat = new Chat(ollama, ContextUpdateSystemPrompt);
        var updatedContext = await SendPromptAsync(updateChat, BuildContextUpdatePrompt(existingContext, diff));

        if (string.IsNullOrWhiteSpace(updatedContext))
        {
            Console.Error.WriteLine("Model returned an empty context update.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(contextPath)!);
        await File.WriteAllTextAsync(contextPath, updatedContext.Trim() + Environment.NewLine);

        Console.WriteLine($"Updated context at {contextPath}");
    }

    private static string GetContextPath(string workingDirectory)
    {
        return Path.Combine(workingDirectory, ".fabs", "context.md");
    }

    private static async Task<string?> TryReadContextAsync(string contextPath)
    {
        if (!File.Exists(contextPath))
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(contextPath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Failed to read context file at {contextPath}: {ex.Message}");
            return null;
        }
    }

    private static string BuildReviewPrompt(string diff, string? existingContext)
    {
        if (existingContext is null)
        {
            return diff;
        }

        return
            $"""
            Repository context from `.fabs/context.md` is provided below. Use it to understand the project structure and affected areas, but base your findings on the git diff.

            <repository-context>
            {existingContext}
            </repository-context>

            <git-diff>
            {diff}
            </git-diff>
            """;
    }

    private static string BuildContextUpdatePrompt(string existingContext, string diff)
    {
        return
            $"""
            Update the repository context document so it stays accurate after the following changes.
            Keep the output concise, cohesive, and limited to durable repository knowledge.

            <existing-context>
            {existingContext}
            </existing-context>

            <git-diff>
            {diff}
            </git-diff>
            """;
    }

    private static async Task<string> SendPromptAsync(Chat chat, string prompt)
    {
        var response = string.Empty;

        await foreach (var token in chat.SendAsync(prompt))
        {
            response += token;
        }

        return response;
    }
}
