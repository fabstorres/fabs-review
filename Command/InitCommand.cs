using OllamaSharp;

namespace FabsReview.Command;

internal static class InitCommand
{
    private const string Prompt =
        """
        You will analyze the file contents and create a project overview and dependency map. Your output will be regarded as a text file so do not make commentary or suggesstions. Treat your output as documentation.
        The project overview should include:
        - The name of the project
        - The description of the project
        - The technologies used in the project
        - The dependencies of the project
        - The files in the project
        - The directories in the project
        """;

    public static async Task RunAsync(GitService git, OllamaApiClient ollama)
    {
        Console.WriteLine("This process may take some time");
        var files = await git.GetProjectFilesAsync();
        var contents = files.Select(File.ReadAllText).ToList();

        var chat = new Chat(ollama, Prompt);

        await foreach (var token in chat.SendAsync(string.Join("\n", contents)))
        {
            Console.Write(token);
        }
    }
}
