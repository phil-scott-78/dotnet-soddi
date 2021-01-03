using System;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace Soddi.Services
{
    public class XmlToDataReader<TClass> : IDataReader
    {
        private readonly XmlReader _xmlReader;

        // ReSharper disable StaticMemberInGenericType
        private static readonly PropertyInfo[] s_typeMapping = typeof(TClass).GetProperties();

        private static readonly ConcurrentDictionary<string, int> s_ordinalMapping =
            new ConcurrentDictionary<string, int>();
        // ReSharper restore StaticMemberInGenericType

        private XElement? _element;

        public XmlToDataReader(XmlReader xmlReader)
        {
            _xmlReader = xmlReader;
            _xmlReader.MoveToContent();
        }

        public bool GetBoolean(int i) => throw new NotImplementedException();
        public byte GetByte(int i) => throw new NotImplementedException();

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) =>
            throw new NotImplementedException();

        public char GetChar(int i) => throw new NotImplementedException();

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) =>
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
        public string GetName(int i) => throw new NotImplementedException();


        public string GetString(int i) => throw new NotImplementedException();
        public int GetValues(object[] values) => throw new NotImplementedException();
        public object this[int i] => throw new NotImplementedException();
        public object this[string name] => throw new NotImplementedException();
        public DataTable GetSchemaTable() => throw new NotImplementedException();
        public bool NextResult() => false;

        // these are the things needed by SqlBulkCopy
        public string GetDataTypeName(int i)
        {
            return s_typeMapping[i].Name;
        }

        public bool IsDBNull(int i)
        {
            return _element?.Attribute(s_typeMapping[i].Name) == null;
        }

        public int GetOrdinal(string name)
        {
            return s_ordinalMapping.GetOrAdd(name, n =>
            {
                for (var i = 0; i < s_typeMapping.Length; i++)
                {
                    if (s_typeMapping[i].Name == n)
                    {
                        return i;
                    }
                }

                throw new Exception("Invalid column name");
            });
        }

        public Type GetFieldType(int i)
        {
            return s_typeMapping[i].PropertyType;
        }

        public object GetValue(int i)
        {
            return _element?.Attribute(s_typeMapping[i].Name)?.Value ?? throw new Exception("No element to read");
        }

        public int FieldCount => s_typeMapping.Length;

        public bool Read()
        {
            do
            {
                var result = _xmlReader.Read();
                if (result == false)
                {
                    return false;
                }
            } while (_xmlReader.NodeType != XmlNodeType.Element && _xmlReader.Name != "row");


            if (!(XNode.ReadFrom(_xmlReader) is XElement el))
            {
                return false;
            }

            _element = el;

            return true;
        }

        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;

        public void Dispose()
        {
        }

        public void Close()
        {
        }
    }
}
