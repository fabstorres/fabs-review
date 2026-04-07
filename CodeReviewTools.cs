using OllamaSharp;

namespace fabs_review;

/// <summary>
/// A Toolkit for the model to able to code review
/// </summary>
public class CodeReviewTools
{
    /// <summary>
    /// Get the given list of project files from the current directory
    /// </summary>
    /// <returns> Returns a single file name as a string </returns>
    [OllamaTool]
    public static string GetProjectFiles() => "Program.cs";
}
