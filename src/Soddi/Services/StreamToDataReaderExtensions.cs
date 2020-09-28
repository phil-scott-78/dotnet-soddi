using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml;
using LamarCodeGeneration.Util;
using Soddi.TableTypes;

namespace Soddi.Services
{
    public static class StreamToDataReaderExtensions
    {
        private static readonly Dictionary<string, Type> s_stringToXmlReaderType = typeof(StreamToDataReaderExtensions)
            .Assembly
            .GetTypes()
            .Where(i => i.HasAttribute<StackOverflowDataTable>())
            .Select(i => new { Type = i, Attribute = i.GetAttribute<StackOverflowDataTable>() })
            .ToDictionary(
                i => i.Attribute.FileName,
                i => typeof(XmlToDataReader<>).MakeGenericType(i.Type)
            );

        public static IDataReader AsDataReader(this Stream entryStream, string filename)
        {
            var xmlReader = XmlReader.Create(entryStream);
            var type = s_stringToXmlReaderType[filename] ?? throw new Exception("Unknown archive file - " + filename);
            if (Activator.CreateInstance(type, xmlReader) is IDataReader instance)
            {
                return instance;
            }

            throw new Exception($"Could not create instance of {type}");
        }
    }
}
