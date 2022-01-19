#nullable disable

namespace Soddi.TableTypes;

public class StackOverflowDataTable : Attribute
{
    public string FileName { get; }

    public StackOverflowDataTable(string fileName)
    {
        FileName = fileName;
    }
}