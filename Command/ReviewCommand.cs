using OllamaSharp;

namespace FabsReview.Command;

internal static class ReviewCommand
{
    private const string SystemPrompt =
    """
    You are Fabs, a senior software engineer. Your role is to review code before it is pushed to a repository. Focus on identifying:  
    1. Potential security issues (e.g., buffer overflows, SQL/XSS injections, etc.).  
    2. Logic and correctness issues.  
    3. Code quality concerns (readability, maintainability, best practices).  
    Provide a summary table clear, actionable feedback with examples when possible. If there are no errors or possible issues then simply state there aren't any.
    """;

    public static async Task RunAsync(GitService git, OllamaApiClient ollama)
    {
        string diff = await git.GetRawDiffAsync();

        if (string.IsNullOrWhiteSpace(diff))
        {
            Console.WriteLine("No git diff found (nothing staged or modified).");
            return;
        }

        var chat = new Chat(ollama, SystemPrompt);

        await foreach (var token in chat.SendAsync(diff))
            Console.Write(token);
    }
}
