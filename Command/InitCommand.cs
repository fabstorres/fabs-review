using System.Text;
using OllamaSharp;

namespace FabsReview.Command;

internal static class InitCommand
{
    private const int BatchSize = 10;
    private const string BatchPrompt =
        """
        You will analyze a batch of repository files and produce markdown notes that will later be merged into a final context.md document.
        Focus only on the files included in the batch.
        Include:
        - the responsibilities of important files/modules in the batch
        - the directories represented in the batch
        - notable dependencies or technologies mentioned in the batch
        - relationships between files in the batch
        - implementation details that seem important for understanding the repository
        Do not add commentary or suggestions. Treat your output as documentation notes.
        """;
    private const string FinalPrompt =
        """
        You will synthesize batch summaries into a single context.md document for the repository.
        Your output will be written directly to context.md, so produce markdown only.
        Do not include batch labels such as "Batch 1", "Batch 2", or any per-batch section headers in the final document.
        Start directly with the repository context content.
        Include:
        - the project name
        - a short description of the project
        - the main technologies used
        - important dependencies
        - the key files and directories
        - the architecture or dependency map
        - notable workflows or conventions that appear across the repository
        Merge overlapping details into one cohesive document and avoid repeating the same point.
        """;

    public static async Task RunAsync(GitService git, OllamaApiClient ollama, string workingDirectory)
    {
        Console.WriteLine("Building project context...");
        var files = (await git.GetProjectFilesAsync()).ToList();

        if (files.Count == 0)
        {
            Console.WriteLine("No project files found.");
            return;
        }

        var batches = files.Chunk(BatchSize).ToList();
        var batchSummaries = new List<string>(batches.Count);

        for (var index = 0; index < batches.Count; index++)
        {
            Console.WriteLine($"Processing batch {index + 1}/{batches.Count} ({batches[index].Length} files)...");
            var batchPayload = await BuildBatchPayloadAsync(workingDirectory, batches[index]);

            if (string.IsNullOrWhiteSpace(batchPayload))
            {
                Console.WriteLine("Skipped an empty batch.");
                continue;
            }

            var batchChat = new Chat(ollama, BatchPrompt);
            var batchSummary = await SendPromptAsync(batchChat, batchPayload);

            if (string.IsNullOrWhiteSpace(batchSummary))
            {
                Console.WriteLine($"Batch {index + 1} returned no summary.");
                continue;
            }

            batchSummaries.Add(
                $"""
                <batch-summary index="{index + 1}">
                {batchSummary.Trim()}
                </batch-summary>
                """);
        }

        if (batchSummaries.Count == 0)
        {
            Console.WriteLine("No readable project files were available to summarize.");
            return;
        }

        Console.WriteLine("Synthesizing final context...");
        var finalChat = new Chat(ollama, FinalPrompt);
        var finalContext = await SendPromptAsync(finalChat, string.Join("\n\n", batchSummaries));
        finalContext = RemoveLeadingBatchHeading(finalContext);

        if (string.IsNullOrWhiteSpace(finalContext))
        {
            Console.Error.WriteLine("Model returned an empty context document.");
            return;
        }

        var fabsDirectory = Path.Combine(workingDirectory, ".fabs");
        Directory.CreateDirectory(fabsDirectory);

        var contextPath = Path.Combine(fabsDirectory, "context.md");
        await File.WriteAllTextAsync(contextPath, finalContext.Trim() + Environment.NewLine);

        Console.WriteLine($"Wrote context to {contextPath}");
    }

    private static async Task<string> BuildBatchPayloadAsync(string workingDirectory, IEnumerable<string> files)
    {
        var builder = new StringBuilder();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(workingDirectory, file);

            try
            {
                var content = await File.ReadAllTextAsync(file);

                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                builder.AppendLine($"<file path=\"{relativePath}\">");
                builder.AppendLine(content);
                builder.Append("</file>");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is DecoderFallbackException)
            {
                Console.WriteLine($"Skipping unreadable file: {relativePath}");
            }
        }

        return builder.ToString();
    }

    private static async Task<string> SendPromptAsync(Chat chat, string prompt)
    {
        var builder = new StringBuilder();

        await foreach (var token in chat.SendAsync(prompt))
        {
            builder.Append(token);
        }

        return builder.ToString();
    }

    private static string RemoveLeadingBatchHeading(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var normalized = content.TrimStart();
        var lines = normalized.Split(Environment.NewLine);

        if (lines.Length == 0)
        {
            return normalized;
        }

        if (!lines[0].StartsWith("#", StringComparison.Ordinal))
        {
            return normalized;
        }

        var heading = lines[0].Trim().TrimStart('#').Trim();
        if (!heading.StartsWith("Batch ", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var remaining = string.Join(Environment.NewLine, lines.Skip(1)).TrimStart();
        return remaining;
    }
}
