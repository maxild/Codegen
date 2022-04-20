using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Codegen.Library.JsonConverters;

namespace Codegen.Library;

/// <summary>
/// Metadata wrapper used to represent a single code generated type
/// when type of records are not known --- this is the case when reading
/// and writing instances to disk --- only the dynamically invoked C#
/// expressions and Razor templates will know the type of each record at runtime.
/// </summary>
[JsonConverter(typeof(MetadataModelConverter))]
public abstract class MetadataModel : IEquatable<MetadataModel>
{
    private static readonly JsonSerializerOptions s_writeOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static string Serialize(MetadataModel metadataModel)
    {
        return JsonSerializer.Serialize(metadataModel, s_writeOptions);
    }

    public static MetadataModel Deserialize(string json)
    {
        return JsonSerializer.Deserialize<MetadataModel>(json)!;
    }

    public static MetadataModel<TRecord> Deserialize<TRecord>(string json)
    {
        return JsonSerializer.Deserialize<MetadataModel<TRecord>>(json)!;
    }

    public static object Deserialize(string json, Type recordType)
    {
        Type modelType = typeof(MetadataModel<>).MakeGenericType(recordType);
        return JsonSerializer.Deserialize(json, modelType)!;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataModel"/> type.
    /// </summary>
    /// <param name="toolVersion"></param>
    /// <param name="queryName">The name of the cssql file</param>
    /// <param name="templateName">The name of the cshtml file</param>
    /// <param name="namespace">The namespace of the generated type</param>
    /// <param name="typeName">The type name of the generated type.</param>
    /// <param name="xmlDoc">The xml-doc of the generated type.</param>
    /// <param name="identifierPrefix">The prefix added to the C# identifier.</param>
    /// <param name="domusIdentifierPrefix">The prefix used by Domus.</param>
    /// <param name="sqlText">The SQL expression that have generated the records.</param>
    /// <param name="recordTypeName">The assembly-qualified name of the record type.</param>
    /// <param name="records">The list of records.</param>
    public static MetadataModel Create(
        string toolVersion,
        string queryName,
        string templateName,
        string @namespace,
        string typeName,
        string xmlDoc,
        string identifierPrefix,
        string domusIdentifierPrefix,
        string sqlText,
        string recordTypeName,
        IReadOnlyList<object> records)
    {
        Type? rt = Type.GetType(recordTypeName);
        if (rt is null)
        {
            throw new ArgumentException($"The {recordTypeName} type cannot be found.", nameof(recordTypeName));
        }

        Type t = typeof(MetadataModel<>).MakeGenericType(rt);
        return (MetadataModel)Activator.CreateInstance(t,
            toolVersion,
            queryName,
            templateName,
            @namespace,
            typeName,
            xmlDoc,
            identifierPrefix,
            domusIdentifierPrefix,
            sqlText,
            recordTypeName,
            records)!;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataModel"/> type.
    /// </summary>
    /// <param name="toolVersion"></param>
    /// <param name="queryName">The name of the cssql file</param>
    /// <param name="templateName">The name of the cshtml file</param>
    /// <param name="namespace">The namespace of the generated type</param>
    /// <param name="typeName">The type name of the generated type.</param>
    /// <param name="xmlDoc">The xml-doc of the generated type.</param>
    /// <param name="identifierPrefix">The prefix to be added on the C# identifiers.</param>
    /// <param name="domusIdentifierPrefix">The prefix used by Domus.</param>
    /// <param name="sqlText">The SQL expression that have generated the records.</param>
    /// <param name="recordTypeName">The assembly-qualified name of the record type.</param>
    /// <param name="records">The list of records.</param>
    public static MetadataModel<TRecord> Create<TRecord>(
        string toolVersion,
        string queryName,
        string templateName,
        string @namespace,
        string typeName,
        string xmlDoc,
        string identifierPrefix,
        string domusIdentifierPrefix,
        string sqlText,
        string recordTypeName,
        IReadOnlyList<object> records)
    {
        return new(toolVersion, queryName, templateName, @namespace, typeName, xmlDoc, identifierPrefix,
            domusIdentifierPrefix, sqlText, recordTypeName, records);
    }

    protected MetadataModel(
        string toolVersion,
        string queryName,
        string templateName,
        string @namespace,
        string typeName,
        string xmlDoc,
        string identifierPrefix,
        string domusIdentifierPrefix,
        string sqlText,
        string recordTypeName,
        IReadOnlyList<object> records)
    {
        ToolVersion = toolVersion ?? throw new ArgumentNullException(nameof(toolVersion));
        QueryName = queryName ?? throw new ArgumentNullException(nameof(queryName));
        TemplateName = templateName ?? throw new ArgumentNullException(nameof(templateName));
        Namespace = @namespace ?? throw new ArgumentNullException(nameof(@namespace));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        XmlDoc = xmlDoc ?? throw new ArgumentNullException(nameof(xmlDoc));
        IdentifierPrefix = identifierPrefix;
        DomusIdentifierPrefix = domusIdentifierPrefix;
        SqlText = sqlText ?? throw new ArgumentNullException(nameof(sqlText));
        RecordTypeName = recordTypeName ?? throw new ArgumentNullException(nameof(recordTypeName));
        Records = records ??
                  throw new ArgumentNullException(nameof(records)); // TODO: ?? Enumerable.Empty<object>();
    }

    public MetadataModel WithToolVersion(string version)
    {
        // TODO: Test that ToList create a deep copy??? should it?
        return Create(version, QueryName, TemplateName, Namespace, TypeName, XmlDoc, IdentifierPrefix,
            DomusIdentifierPrefix, SqlText, RecordTypeName, Records.ToList());
    }

    public string ToolVersion { get; }

    public string QueryName { get; }

    public string TemplateName { get; }

    public string Namespace { get; }

    public string TypeName { get; }

    public string XmlDoc { get; }

    public string IdentifierPrefix { get; }

    public string DomusIdentifierPrefix { get; }

    public string SqlText { get; }

    /// <summary>
    /// The assembly-qualified name of the record type.
    /// </summary>
    public string RecordTypeName { get; }

    /// <summary>
    /// The <see cref="Type"/> of each record.
    /// </summary>
    public Type RecordType => Type.GetType(RecordTypeName) ??
                              throw new InvalidOperationException($"The {RecordTypeName} type cannot be found.");

    public IReadOnlyList<object> Records { get; }

    public bool Equals(MetadataModel? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (!SimpleEquals(other))
        {
            return false;
        }

        //for (int i = 0; i < Records.Count; i++)
        //{
        //    object thisValue = Records[i];
        //    object otherValue = other.Records[i];
        //    // We cannot use equality comparer and use Object.Equals, because of System.Object record type
        //    if (!Equals(thisValue, otherValue))
        //    {
        //        return false;
        //    }
        //}

        using (var thisIter = Records.GetEnumerator())
        using (var otherIter = other.Records.GetEnumerator())
        {
            while (true)
            {
                bool thisCanRead = thisIter.MoveNext();
                bool otherCanRead = otherIter.MoveNext();
                if (thisCanRead != otherCanRead)
                {
                    return false; // different count
                }

                if (!thisCanRead)
                {
                    break; // both sequences are finished
                }

                object thisValue = thisIter.Current;
                object otherValue = otherIter.Current;
                // We cannot use equality comparer and use Object.Equals, because of System.Object record type
                if (!Equals(thisValue, otherValue))
                {
                    return false; // different values
                }
            }
        }

        return true;
    }

    protected bool SimpleEquals(MetadataModel other)
    {
        // simple fields comparison (all fields are non-null)
        bool simple = ToolVersion.Equals(other.ToolVersion, StringComparison.OrdinalIgnoreCase) &&
                      QueryName.Equals(other.QueryName, StringComparison.OrdinalIgnoreCase) &&
                      TemplateName.Equals(other.TemplateName, StringComparison.OrdinalIgnoreCase) &&
                      Namespace.Equals(other.Namespace, StringComparison.OrdinalIgnoreCase) &&
                      TypeName.Equals(other.TypeName, StringComparison.OrdinalIgnoreCase) &&
                      XmlDoc.Equals(other.XmlDoc, StringComparison.OrdinalIgnoreCase) &&
                      IdentifierPrefix.Equals(other.IdentifierPrefix, StringComparison.OrdinalIgnoreCase) &&
                      SqlText.Equals(other.SqlText, StringComparison.OrdinalIgnoreCase);

        return simple;
    }
}

/// <summary>
/// Metadata wrapper used to represent a single code generated type
/// when the type of records are known --- this is the case in Razor template
/// files where the @inherits directive will reference this model type.
/// </summary>
[JsonConverter(typeof(MetadataModelConverter))]
public class MetadataModel<TRecord> : MetadataModel, IEquatable<MetadataModel<TRecord>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataModel{TRecord}"/> type.
    /// </summary>
    /// <param name="toolVersion"></param>
    /// <param name="queryName">The name of the cssql file</param>
    /// <param name="templateName">The name of the cshtml file</param>
    /// <param name="namespace">The namespace of the generated type</param>
    /// <param name="typeName">The type name of the generated type.</param>
    /// <param name="xmlDoc">The xml-doc of the generated type.</param>
    /// <param name="identifierPrefix">The prefix to be added on the C# identifier.</param>
    /// <param name="domusIdentifierPrefix">The prefix used by Domus.</param>
    /// <param name="sqlText">The SQL expression that have generated the records.</param>
    /// <param name="recordTypeName">The assembly-qualified name of the record type.</param>
    /// <param name="records">The list of records.</param>
    public MetadataModel(
        string toolVersion,
        string queryName,
        string templateName,
        string @namespace,
        string typeName,
        string xmlDoc,
        string identifierPrefix,
        string domusIdentifierPrefix,
        string sqlText,
        string recordTypeName,
        IReadOnlyList<object> records)
        : base(toolVersion, queryName, templateName, @namespace, typeName, xmlDoc, identifierPrefix,
            domusIdentifierPrefix, sqlText, recordTypeName, records)
    {
    }

    private IReadOnlyList<TRecord>? _records;

    public new IReadOnlyList<TRecord> Records =>
        _records ??= new Wrapper<TRecord>(base.Records); // NOTE: det er det eneste vigtige!!!!!

    private class Wrapper<T> : IReadOnlyList<T>
    {
        private readonly IReadOnlyList<object> _records;

        public Wrapper(IReadOnlyList<object> records)
        {
            _records = records;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _records.Cast<T>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count => _records.Count;

        public T this[int index] => (T)_records[index];
    }

    public bool Equals(MetadataModel<TRecord>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (!SimpleEquals(other))
        {
            return false;
        }

        //for (int i = 0; i < Records.Count; i++)
        //{
        //    TRecord thisValue = Records[i];
        //    TRecord otherValue = other.Records[i];
        //    if (!EqualityComparer<TRecord>.Default.Equals(thisValue, otherValue))
        //    {
        //        return false;
        //    }
        //}

        using (var thisIter = Records.GetEnumerator())
        using (var otherIter = other.Records.GetEnumerator())
        {
            while (true)
            {
                bool thisCanRead = thisIter.MoveNext();
                bool otherCanRead = otherIter.MoveNext();
                if (thisCanRead != otherCanRead)
                {
                    return false; // different count
                }

                if (!thisCanRead)
                {
                    break; // both sequences are finished
                }

                TRecord thisValue = thisIter.Current;
                TRecord otherValue = otherIter.Current;
                if (!EqualityComparer<TRecord>.Default.Equals(thisValue, otherValue))
                {
                    return false; // different values
                }
            }
        }

        return true;
    }
}
