using FabsReview;
using FabsReview.Command;
using OllamaSharp;

var workingDirectory = Directory.GetCurrentDirectory();
var git = new GitService(workingDirectory);

var ollama = new OllamaApiClient(new OllamaApiClient.Configuration
{
    Uri = new Uri("http://localhost:11434"),
    Model = "qwen2.5-coder:3b",
});

var command = args.ElementAtOrDefault(0);

if (string.IsNullOrWhiteSpace(command))
{
    Console.Error.WriteLine("""
    Expected a command:
        init - creates a context memory of the project
        review - reviews the current diff for errors, logic issues, etc.
    """);
}
else if (command == "init")
{
    await InitCommand.RunAsync(git, ollama, workingDirectory);
}
else if (command == "review")
{
    await ReviewCommand.RunAsync(git, ollama, workingDirectory);
}
else
{
    Console.Error.WriteLine($"""
    Unexpected command '{command}':
        init - creates a context memory of the project
        review - reviews the current diff for errors, logic issues, etc.
    """);
}
