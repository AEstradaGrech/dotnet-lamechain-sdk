
namespace Dotnet.OllamaSharp.LameChain.SDK.Infrastructure.Models.Enums
{
    public enum EPythonOutputKind
    {
        Value = 0,          // The execution was successful and returned a value.
        File = 1,           // The execution was successful and returned a file.
        ValueAndFile = 2,   // The execution was successful and returned both a value and a file.
    }
}
