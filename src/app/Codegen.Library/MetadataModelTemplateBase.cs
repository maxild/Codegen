using System;
using System.Collections.Generic;
using System.Linq;
using CSharpRazor;

namespace Codegen.Library;

/// <summary>
/// Codegen specific template class that makes the model into a <see cref="MetadataModelTemplateBase{TRecord}"/>
/// </summary>
/// <typeparam name="TRecord">The type used for database records.</typeparam>
public abstract class MetadataModelTemplateBase<TRecord> : TemplateBase<MetadataModel<TRecord>>
{
    /// <summary>
    /// The identifier for the default(TEnum) value. All generated
    /// Enums should have zero value with this name.
    /// </summary>
    public string DefaultValueText => "NONE";

    /// <summary>
    /// Used by all templates to convert the database text code to a valid C# identifier.
    /// </summary>
    /// <param name="key">The identifier in the database.</param>
    /// <returns>The sanitized C# identifier.</returns>
    public string GetIdentifier(string key)
    {
        static string Sanitize(string s) =>
            s.Replace("-", "_", StringComparison.Ordinal);

        CheckPrefixOfKey(key);

        return string.Concat(Model.IdentifierPrefix, Sanitize(key));
    }

    /// <summary>
    /// Used by value_enum template to convert the database text code to an int value.
    /// </summary>
    /// <param name="key">The identifier in the database.</param>
    /// <returns>The int value.</returns>
    public int GetValue(string key)
    {
        CheckPrefixOfKey(key);

        // Without any prefixes match = { null, 0 } which is fine, see below
        KeyValuePair<string, int> match =
            Model.DatabaseIdentifierPrefixes.FirstOrDefault(kvp => key.StartsWith(kvp.Key, StringComparison.Ordinal));

        int prefixLength = match.Key?.Length ?? 0;
        int prefixBase = match.Value;
        int prefixNumber = prefixLength < key.Length
            ? int.Parse(key[prefixLength..])
            : throw new InvalidOperationException($"The database identifier '{key}' cannot be converted to an integer value.");

        return prefixBase + prefixNumber;
    }

    /// <summary>
    /// Helper function to encode xml doc strings into valid xml text.
    /// </summary>
    /// <param name="s">The text to xml encode.</param>
    /// <returns>Xml encoded text.</returns>
    protected static string XmlDocString(string s) =>
        s.Replace("'", "&apos;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>
    /// Check that the database identifier (key) has a valid database prefix, if
    /// the @cg-DatabaseIdentifierPrefixes directive have been used to configure any prefixes.
    /// </summary>
    private void CheckPrefixOfKey(string key)
    {
        if (Model.DatabaseIdentifierPrefixes.Count > 0 &&
            Model.DatabaseIdentifierPrefixes.Keys.All(prefix => !key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"The database identifier '{key}' does not have a prefix in the set {{'{string.Join(',', Model.DatabaseIdentifierPrefixes.Keys)}}}'.");
        }
    }
}
