using CSharpRazor;

namespace Codegen.Library
{
    // TODO: Move to dotnet-cgcsharp project (this type is only used in razor templates)
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
                s.Replace("-", "_", System.StringComparison.Ordinal);

            CheckPrefix(key);
            // IDE0049 should be a warning here
            // return String.Concat(Model.IdentifierPrefix, Sanitize(key));
            return string.Concat(Model.IdentifierPrefix, Sanitize(key));
        }

        /// <summary>
        /// Used by value_enum template to convert the database text code to an int value.
        /// </summary>
        /// <param name="key">The identifier in the database.</param>
        /// <returns>The int value.</returns>
        public int GetValue(string key)
        {
            CheckPrefix(key);
            int c = Model.DomusIdentifierPrefix.Length;
            return c < key.Length
                ? int.Parse(key[c..])
                : throw new System.InvalidOperationException($"The database identifier '{key}' cannot be converted to an integer value.");
        }

        /// <summary>
        /// Helper function to encode xml doc strings into valid xml text.
        /// </summary>
        /// <param name="s">The text to xml encode.</param>
        /// <returns>Xml encoded text.</returns>
        protected static string XmlDocString(string s)
        {
            return s.Replace("'", "&apos;", System.StringComparison.Ordinal)
                .Replace("\"", "&quot;", System.StringComparison.Ordinal)
                .Replace("&", "&amp;", System.StringComparison.Ordinal)
                .Replace("<", "&lt;", System.StringComparison.Ordinal)
                .Replace(">", "&gt;", System.StringComparison.Ordinal);
        }

        private void CheckPrefix(string key)
        {
            if (!string.IsNullOrEmpty(Model.DomusIdentifierPrefix) && !key.StartsWith(Model.DomusIdentifierPrefix, System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException($"The database identifier '{key}' does not have prefix '{Model.DomusIdentifierPrefix}'.");
            }
        }


    }
}
