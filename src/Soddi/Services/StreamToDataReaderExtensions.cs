﻿using System.Xml;
using Soddi.TableTypes;

namespace Soddi.Services;

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

    private static bool HasAttribute<T>(this Type provider) where T : Attribute
    {
        return provider.IsDefined(typeof(T), true);
    }

    private static T GetAttribute<T>(this Type provider) where T : Attribute
    {
        return (T)provider.GetCustomAttributes(typeof(T), true).First();
    }

    public static IDataReader AsDataReader(this Stream entryStream, string filename,
        Action<(int postId, string tags)>? onTagFound = null)
    {
        var xmlReader = XmlReader.Create(entryStream);
        var type = s_stringToXmlReaderType[filename] ?? throw new Exception("Unknown archive file - " + filename);
        if (Activator.CreateInstance(type, xmlReader, onTagFound) is IDataReader instance)
        {
            return instance;
        }

        throw new Exception($"Could not create instance of {type}");
    }
}
