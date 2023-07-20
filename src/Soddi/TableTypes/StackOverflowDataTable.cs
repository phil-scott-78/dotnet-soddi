#nullable disable

namespace Soddi.TableTypes;

public class StackOverflowDataTable(string fileName) : Attribute
{
    public string FileName { get; } = fileName;
}