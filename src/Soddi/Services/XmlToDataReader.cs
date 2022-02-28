using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace Soddi.Services;

public class XmlToDataReader<TClass> : IDataReader
{
    private readonly XmlReader _xmlReader;
    private readonly Action<(int postId, string tags)>? _onTagFound;
    private readonly Lazy<bool> _isPost;
    private readonly Lazy<(int PostIdOrdinal, int TagOrdinal)> _postTagColumnOrdinals;

    private readonly PropertyInfo[] _typeMapping = typeof(TClass).GetProperties();
    private readonly Dictionary<string, int> _ordinalMapping = new();
    private readonly Dictionary<int, XName> _ordinalToNameMapping = new();

    private XElement? _currentRowElement;

    public XmlToDataReader(XmlReader xmlReader, Action<(int postId, string tags)>? onTagFound = null)
    {
        _isPost = new Lazy<bool>(() =>
        {
            try
            {
                GetOrdinal("Tags");
                return true;
            }
            catch
            {
                return false;
            }
        });

        _postTagColumnOrdinals =
            new Lazy<(int PostIdOrdinal, int TagOrdinal)>(() => (GetOrdinal("Id"), GetOrdinal("Tags")));

        _xmlReader = xmlReader;
        _onTagFound = onTagFound;
        _xmlReader.MoveToContent();
    }

    public bool GetBoolean(int i) => throw new NotImplementedException();
    public byte GetByte(int i) => throw new NotImplementedException();

    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) =>
        throw new NotImplementedException();

    public char GetChar(int i) => throw new NotImplementedException();

    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) =>
        throw new NotImplementedException();

    public IDataReader GetData(int i) => throw new NotImplementedException();

    public DateTime GetDateTime(int i) => throw new NotImplementedException();
    public decimal GetDecimal(int i) => throw new NotImplementedException();
    public double GetDouble(int i) => throw new NotImplementedException();
    public float GetFloat(int i) => throw new NotImplementedException();
    public Guid GetGuid(int i) => throw new NotImplementedException();
    public short GetInt16(int i) => throw new NotImplementedException();
    public long GetInt64(int i) => throw new NotImplementedException();
    public int GetValues(object[] values) => throw new NotImplementedException();
    public object this[int i] => throw new NotImplementedException();
    public object this[string name] => throw new NotImplementedException();
    public DataTable GetSchemaTable() => throw new NotImplementedException();
    public bool NextResult() => false;


    // these are the things needed by SqlBulkCopy
    public string GetString(int i) => (string)GetValue(i);
    public int GetInt32(int i) => (int.Parse((string)GetValue(i)));
    public string GetName(int i) => _typeMapping[i].Name;
    public string GetDataTypeName(int i) => _typeMapping[i].PropertyType.Name;
    public bool IsDBNull(int i) => GetAttribute(i) == null;
    public Type GetFieldType(int i) => _typeMapping[i].PropertyType;

    public object GetValue(int i) => GetAttribute(i)?.Value ??
                                     throw new Exception("No element to read");


    public int FieldCount => _typeMapping.Length;

    private XAttribute? GetAttribute(int i)
    {
        if (_currentRowElement == null)
        {
            throw new Exception("No element to read");
        }

        // we can cache the XName at least
        if (_ordinalToNameMapping.TryGetValue(i, out var xName))
        {
            return _currentRowElement?.Attribute(xName);
        }

        var newXName = XName.Get(_typeMapping[i].Name);
        _ordinalToNameMapping.Add(i, newXName);
        return _currentRowElement.Attribute(newXName) ?? null;
    }

    public int GetOrdinal(string name)
    {
        if (_ordinalMapping.TryGetValue(name, out var value))
        {
            return value;
        }

        for (var i = 0; i < _typeMapping.Length; i++)
        {
            if (!_typeMapping[i].Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            _ordinalMapping.Add(name, i);
            return i;
        }

        throw new Exception("Invalid column name");
    }


    public bool Read()
    {
        // loop until we find a row or we hit the end of the records
        do
        {
            var result = _xmlReader.Read();
            if (result == false)
            {
                return false;
            }
        } while (_xmlReader.NodeType != XmlNodeType.Element && _xmlReader.Name != "row");

        // make sure the current node is an XElement and if so set the current row to it
        if (XNode.ReadFrom(_xmlReader) is not XElement el) return false;

        _currentRowElement = el;
        if (RecordsAffected == 0)
        {
            // on the first row we read let's check the columns in to the type and see if it jives.
            foreach (var attribute in _currentRowElement.Attributes())
            {
                if (!_typeMapping.Select(i => i.Name)
                        .Any(i => i.Equals(attribute.Name.LocalName, StringComparison.InvariantCultureIgnoreCase)))
                {
                    Log.Write(LogLevel.Trace, $"{attribute.Name.LocalName} not found for table {typeof(TClass).Name}");
                }
            }
        }

        RecordsAffected++;

        // if we aren't a post record or have nothing to publish then we are done and can return
        // true to indicate there is a row ready to be read
        if (_onTagFound == null || !IsPost())
        {
            return true;
        }

        // but we are reading a post. publish the tag we found for processing
        var (idOrdinal, tagOrdinal) = _postTagColumnOrdinals.Value;
        if (IsDBNull(tagOrdinal) == false)
        {
            _onTagFound.Invoke((GetInt32(idOrdinal), GetString(tagOrdinal)));
        }

        return true;
    }

    private bool IsPost() => _isPost.Value;
    public int Depth => 0;
    public bool IsClosed => false;

    public int RecordsAffected
    {
        get;
        private set;
    }

    public void Dispose()
    {
    }

    public void Close()
    {
    }
}
