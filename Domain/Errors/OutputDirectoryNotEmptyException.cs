namespace ILSpy.Mcp.Domain.Errors;

public sealed class OutputDirectoryNotEmptyException : DomainException
{
    public string OutputDirectory { get; }

    public OutputDirectoryNotEmptyException(string outputDirectory)
        : base("DIRECTORY_NOT_EMPTY",
               $"Output directory is not empty: {outputDirectory}. Specify an empty or non-existent directory.")
    {
        OutputDirectory = outputDirectory;
    }
}
