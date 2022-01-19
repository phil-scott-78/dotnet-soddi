using System.Text.RegularExpressions;

namespace Soddi.Services;

/// <summary>
/// Specialized DataReader specifically for allowing one thread to push tags
/// and another thread to bulk insert
/// </summary>
public class PubSubPostTagDataReader : IDataReader
{
    private readonly Regex _rx = new(@"\<([^>]+)\>", RegexOptions.Compiled);

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
    public int GetInt32(int i) => throw new NotImplementedException();
    public long GetInt64(int i) => throw new NotImplementedException();
    public string GetString(int i) => throw new NotImplementedException();
    public int GetValues(object[] values) => throw new NotImplementedException();
    public object this[int i] => throw new NotImplementedException();
    public object this[string name] => throw new NotImplementedException();
    public DataTable GetSchemaTable() => throw new NotImplementedException();
    public bool NextResult() => false;
    public string GetDataTypeName(int i) => throw new NotImplementedException();
    public Type GetFieldType(int i) => throw new NotImplementedException();

    // these are the things needed by SqlBulkCopy
    public string GetName(int i)
    {
        return i switch
        {
            0 => "PostId",
            1 => "Tag",
            _ => throw new IndexOutOfRangeException("invalid column index - " + i)
        };
    }

    public bool IsDBNull(int i)
    {
        return false;
    }

    public int GetOrdinal(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "postid" => 0,
            "tag" => 1,
            _ => throw new IndexOutOfRangeException("invalid column - " + name)
        };
    }


    private (int PostId, string Tag)? _currentValue;
    private readonly ConcurrentQueue<(int PostId, string Tag)> _tags = new();

    public void Push(int postId, string tags)
    {
        var distinctTags = _rx.Matches(tags)
            .Select(m => m.Groups[1].Value)
            .Distinct();

        foreach (var tag in distinctTags)
        {
            _tags.Enqueue((postId, tag));
        }
    }

    public object GetValue(int i)
    {
        if (_currentValue == null)
        {
            throw new InvalidOperationException("No current row to read");
        }

        return i switch
        {
            0 => _currentValue.Value.PostId,
            1 => _currentValue.Value.Tag,
            _ => throw new IndexOutOfRangeException("invalid column index - " + i)
        };
    }

    public int FieldCount => 2;

    public bool Read()
    {
        var spin = new SpinWait();

        while (IsClosed == false)
        {
            if (_tags.TryDequeue(out var v))
            {
                _currentValue = v;
                return true;
            }

            spin.SpinOnce();
        }

        return false;
    }

    public int Depth => 0;
    public bool IsClosed { get; private set; }

    public int RecordsAffected => 0;

    private bool _disposed;

    ~PubSubPostTagDataReader() => Dispose(false);

    public void Dispose()
    {
        IsClosed = true;
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            IsClosed = true;
        }

        _disposed = true;
    }

    public void Close()
    {
        Dispose();
    }
}
