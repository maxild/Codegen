using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Codegen.Library;
using Shouldly;
using Xunit;

namespace Codegen.Tests;

public class MetadataModelTests
{
#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    private readonly struct RecordTuple : IEquatable<RecordTuple>
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    {
        public string Key { get; init; }

        public string Value { get; init; }

#pragma warning disable 659
        public override bool Equals(object? obj)
#pragma warning restore 659
        {
            return obj is RecordTuple recordTuple && Equals(recordTuple);
        }

        public bool Equals(RecordTuple other)
        {
            return string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RecordsOfObjectsAreDownCastedToItemsOfKeyValuePair()
    {
        // Problem: KeyValuePair values are boxed (bad perf!!!)
        var records = new List<object>
        {
            new KeyValuePair<string, string>("key1", "value1"),
            new KeyValuePair<string, string>("key2", "value2")
        };

        var metadata = MetadataModel.Create<KeyValuePair<string, string>>(
            toolVersion: "0.1.0",
            queryName: "betalingstype",
            templateName: "dataenum",
            @namespace: "Acme.Models",
            typeName: "Betalingstype",
            xmlDoc: "Betalingstype er en type fra databasen",
            identifierPrefix: string.Empty,
            databaseIdentifierPrefix: "B",
            sqlText: "SELECT * FROM SOME_TABLE",
            recordTypeName: typeof(KeyValuePair<string, string>).AssemblyQualifiedName!,
            records);

        metadata.Records.ElementAt(0).ShouldBe(new KeyValuePair<string, string>("key1", "value1"));
        metadata.Records.ElementAt(1).ShouldBe(new KeyValuePair<string, string>("key2", "value2"));
    }

    [Fact]
    public void RecordsOfObjectsAreDownCastedToItemsOfRecordTuple()
    {
        // Problem: RecordTuple values are boxed (bad perf!!!)
        var records = new List<object>
        {
            new RecordTuple { Key = "key1", Value = "value1"},
            new RecordTuple { Key = "key2", Value = "value2"}
        };

        var metadata = MetadataModel.Create<RecordTuple>(
            toolVersion: "0.1.0",
            queryName: "betalingstype",
            templateName: "dataenum",
            @namespace: "Acme.Models",
            typeName: "Betalingstype",
            xmlDoc: "Betalingstype er en type fra databasen",
            identifierPrefix: string.Empty,
            databaseIdentifierPrefix: "B",
            sqlText: "SELECT * FROM SOME_TABLE",
            recordTypeName: typeof(RecordTuple).AssemblyQualifiedName!,
            records);

        metadata.Records.ElementAt(0).ShouldBe(new RecordTuple { Key = "key1", Value = "value1" });
        metadata.Records.ElementAt(1).ShouldBe(new RecordTuple { Key = "key2", Value = "value2" });
    }

    [Fact]
    public void IfRecordTypesInSqlLambdaDoesntMatchRecordTypeInTemplateBase_Then_ThrowsInvalidCastException()
    {
        // Problem: RecordTuple values are boxed (bad perf!!!)
        var records = new List<object>
        {
            new RecordTuple { Key = "key1", Value = "value1"},
            new RecordTuple { Key = "key2", Value = "value2"}
        };

        var metadata = MetadataModel.Create<KeyValuePair<string, string>>(
            toolVersion: "0.1.0",
            queryName: "betalingstype",
            templateName: "dataenum",
            @namespace: "Acme.Models",
            typeName: "Betalingstype",
            xmlDoc: "Betalingstype er en type fra databasen",
            identifierPrefix: string.Empty,
            databaseIdentifierPrefix: "B",
            sqlText: "SELECT * FROM SOME_TABLE",
            recordTypeName: typeof(RecordTuple).AssemblyQualifiedName!,
            records);

        Assert.Throws<InvalidCastException>(() => metadata.Records.ElementAt(0).ShouldBe(new KeyValuePair<string, string>("key1", "value1")))
            .Message.ShouldBe("Unable to cast object of type 'RecordTuple' to type 'System.Collections.Generic.KeyValuePair`2[System.String,System.String]'.");
    }

    [Fact]
    public void Serialize()
    {
        // Problem: RecordTuple values are boxed (bad perf!!!)
        var records = new List<object>
        {
            new RecordTuple {Key = "key1", Value = "value1"},
            new RecordTuple {Key = "key2", Value = "value2"}
        };

        MetadataModel model1 = MetadataModel.Create(
            toolVersion: "0.1.0",
            queryName: "betalingstype",
            templateName: "dataenum",
            @namespace: "Acme.Models",
            typeName: "Betalingstype",
            xmlDoc: "Betalingstype er en type fra databasen.",
            identifierPrefix: string.Empty,
            databaseIdentifierPrefix: "B",
            sqlText: "SELECT * FROM SOME_TABLE",
            recordTypeName: typeof(RecordTuple).AssemblyQualifiedName!,
            records);

        MetadataModel<RecordTuple> model2 = MetadataModel.Create<RecordTuple>(
            toolVersion: "0.1.0",
            queryName: "betalingstype",
            templateName: "dataenum",
            @namespace: "Acme.Models",
            typeName: "Betalingstype",
            xmlDoc: "Betalingstype er en type fra databasen.",
            identifierPrefix: string.Empty,
            databaseIdentifierPrefix: "B",
            sqlText: "SELECT * FROM SOME_TABLE",
            recordTypeName: typeof(RecordTuple).AssemblyQualifiedName!,
            records);

        var json1 = MetadataModel.Serialize(model1);
        var json2 = MetadataModel.Serialize(model2);

        string expectedJson = @"{
                                    |  ""ToolVersion"": ""0.1.0"",
                                    |  ""QueryName"": ""betalingstype"",
                                    |  ""TemplateName"": ""dataenum"",
                                    |  ""Namespace"": ""Acme.Models"",
                                    |  ""TypeName"": ""Betalingstype"",
                                    |  ""XmlDoc"": ""Betalingstype er en type fra databasen."",
                                    |  ""DatabaseIdentifierPrefix"": ""B"",
                                    |  ""SqlText"": ""SELECT * FROM SOME_TABLE"",
                                    |  ""RecordTypeName"": ""Codegen.Tests.MetadataModelTests+RecordTuple, Codegen.Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",
                                    |  ""Records"": [
                                    |    {
                                    |      ""Key"": ""key1"",
                                    |      ""Value"": ""value1""
                                    |    },
                                    |    {
                                    |      ""Key"": ""key2"",
                                    |      ""Value"": ""value2""
                                    |    }
                                    |  ]
                                    |}".ToMultiline();
        json1.ShouldBe(expectedJson);
        json2.ShouldBe(expectedJson);
    }

    [Fact]
    public void Deserialize()
    {
        string json = @"{
                           |  ""ToolVersion"": ""0.1.0"",
                           |  ""QueryName"": ""betalingstype"",
                           |  ""TemplateName"": ""dataenum"",
                           |  ""Namespace"": ""Acme.Models"",
                           |  ""TypeName"": ""Betalingstype"",
                           |  ""XmlDoc"": ""Betalingstype er en type fra databasen."",
                           |  ""DatabaseIdentifierPrefix"": ""B"",
                           |  ""SqlText"": ""SELECT * FROM SOME_TABLE"",
                           |  ""RecordTypeName"": ""Codegen.Tests.MetadataModelTests+RecordTuple, Codegen.Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",
                           |  ""Records"": [
                           |    {
                           |      ""Key"": ""key1"",
                           |      ""Value"": ""value1""
                           |    },
                           |    {
                           |      ""Key"": ""key2"",
                           |      ""Value"": ""value2""
                           |    }
                           |  ]
                           |}".ToMultiline();

        MetadataModel model1 = MetadataModel.Deserialize(json);
        MetadataModel<RecordTuple> model2 = MetadataModel.Deserialize<RecordTuple>(json);
        MetadataModel<RecordTuple> model3 = (MetadataModel<RecordTuple>)MetadataModel.Deserialize(json, typeof(RecordTuple));

        model1.ToolVersion.ShouldBe("0.1.0");
        model1.QueryName.ShouldBe("betalingstype");
        model1.TemplateName.ShouldBe("dataenum");
        model1.Namespace.ShouldBe("Acme.Models");
        model1.TypeName.ShouldBe("Betalingstype");
        model1.XmlDoc.ShouldBe("Betalingstype er en type fra databasen.");
        model1.IdentifierPrefix.ShouldBeEmpty();
        model1.SqlText.ShouldBe("SELECT * FROM SOME_TABLE");
        model1.RecordTypeName.ShouldBe("Codegen.Tests.MetadataModelTests+RecordTuple, Codegen.Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
        model1.RecordType.ShouldBe(typeof(RecordTuple));
        model1.Records.ElementAt(0).ShouldBe(new RecordTuple { Key = "key1", Value = "value1" });
        model1.Records.ElementAt(1).ShouldBe(new RecordTuple { Key = "key2", Value = "value2" });
        model1.Records.Count().ShouldBe(2);

        model2.ShouldBe(model1);
        model3.ShouldBe(model1);
    }

    [Fact]
    public void SerializeAndDeserializeWithTypedRecords()
    {
        // Problem: RecordTuple values are boxed (bad perf!!!)
        var records = new List<object>
        {
            new RecordTuple { Key = "key1", Value = "value1"},
            new RecordTuple { Key = "key2", Value = "value2"}
        };

        MetadataModel<RecordTuple> model = MetadataModel.Create<RecordTuple>(
            toolVersion: "0.1.0",
            queryName: "betalingstype",
            templateName: "dataenum",
            @namespace: "Acme.Models",
            typeName: "Betalingstype",
            xmlDoc: "Betalingstype er en type fra databasen",
            identifierPrefix: string.Empty,
            databaseIdentifierPrefix: "B",
            sqlText: "SELECT * FROM SOME_TABLE",
            recordTypeName: typeof(RecordTuple).AssemblyQualifiedName!,
            records);

        var json = MetadataModel.Serialize(model);

        MetadataModel<RecordTuple> copyOfModel = MetadataModel.Deserialize<RecordTuple>(json);

        copyOfModel.ShouldBe(model);
    }
}

public enum NewlineKind
{
    /// <summary>
    /// Environment.Newline
    /// </summary>
    Platform,
    /// <summary>
    /// {'\r','\n'}
    /// </summary>
    Windows,
    /// <summary>
    /// '\n'
    /// </summary>
    Linux
}

public static class StringExtensions
{
    // How do we escape the | character in the string? We don't need to, because...
    // After newlines (any kind), | must be the first non-whitespace character, and following | characters
    // are not ignored, and doesn't have to be escaped.
    // The first non-whitespace character | is an invisible (zero-width, non-breaking) character,
    // that is removed from the resulting string. It is only a start of line marker.
    // Newlines must be normalized to System.Newline matching the newlines of the platform.
    // NewlineKind is optional.
    //
    // public string json = $@"{{
    //                        |  "FirstName": "Carole",
    //                        |  "Age": {{carole.Age}},
    //                        |  "Children": [
    //                        |    "Kurt",
    //                        |    "Ann"
    //                        |  ]
    //                        |}}".ToMultiline();
    //
    // PROBLEM: In a verbatim string literal, the characters between the delimiters are interpreted verbatim,
    // the only exception being a quote_escape_sequence. Therefore line endings are defined by the source file.
    // Running on Windows GIT will use \r\n, running on Linux, GIT will use \n.
    public static string ToMultiline(this string s, NewlineKind kind = NewlineKind.Platform)
    {
        string newline = kind switch
        {
            NewlineKind.Platform => Environment.NewLine,
            NewlineKind.Windows => "\r\n",
            NewlineKind.Linux => "\n",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown newline kind.")
        };

        var sb = new StringBuilder();

        string[] lines = s.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        int lineNo = 0;
        int? constIndent = null;

        foreach (ReadOnlySpan<char> line in lines)
        {
            // last line should not have newline
            if (lineNo > 0)
                sb.Append(newline);

            // count all leading whitespace
            int indent = 0;
            while (indent < line.Length && char.IsWhiteSpace(line[indent])) indent += 1;

            if (indent > 0)
            {
                // constant indent
                if (constIndent != null)
                {
                    if (indent != constIndent)
                    {
                        throw new FormatException(
                            "The multiline string does not have a constant indent -- adjust the indent such that all the | characters align.");
                    }
                }
                else
                {
                    constIndent = indent;
                }

                // first non-whitespace character must be |
                if (indent >= line.Length || line[indent] != '|')
                    throw new FormatException("All indented lines must begin with the '|' character.");

                sb.Append(line[(indent + 1)..]);
            }
            else
            {
                // only first line can be unindented
                if (lineNo > 0)
                {
                    throw new FormatException(
                        "All lines except the first line must be indented -- indent the following lines and align the beginning | character with the \" of the first line.");
                }

                if (line.Length > 0)
                    sb.Append(line);
            }

            lineNo += 1;
        }

        return sb.ToString();
    }
}
