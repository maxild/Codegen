﻿using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace CSharpRazor
{
    // NOTE: The best place to look for documentation is in RazorPageBase

    /// <summary>
    /// The abstract base class for all our 'Razor DSL' templates that acts both
    /// as a 'global object' in every template (that can be accessed in C# mode: '@Model'),
    /// and as a 'printer' type that can render (See <see cref="RenderAsync"/>) our
    /// template to a string. In contrary to RazorPageBase all writing/printing is done
    /// without HTML encoding (or any other encoding).
    /// </summary>
    public class TemplateBase
    {
        public TextWriter Output { get; private set; } = TextWriter.Null;

        /// <summary>
        /// Writes the specified <paramref name="value"/> to <see cref="Output"/>.
        /// </summary>
        /// <param name="value">The <see cref="object"/> to write.</param>
        public void WriteLiteral(object? value)
        {
            WriteLiteral(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Writes the specified <paramref name="value"/> to <see cref="Output"/>.
        /// </summary>
        /// <param name="value">The <see cref="string"/> to write.</param>
        public void WriteLiteral(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                Output.Write(value);
            }
        }

        /// <summary>
        /// Writes the specified <paramref name="value"/> to <see cref="Output"/>.
        /// </summary>
        /// <param name="value">The <see cref="object"/> to write.</param>
        public void Write(string? value)
        {
            WriteLiteral(value); // no html encoding
        }

        /// <summary>
        /// Writes the specified <paramref name="value"/> to <see cref="Output"/>.
        /// </summary>
        /// <param name="value">The <see cref="object"/> to write.</param>
        public void Write(object? value)
        {
            WriteLiteral(value); // no html encoding
        }

        // --------------------------
        // WEIRD attribute writer API
        // --------------------------
        // In order to be able to emit xml doc strings with embedded xml
        //    /// <summary>
        //    /// Create a <see cref="@Model.TypeName"/> value from the Domus text representation.
        //    /// </summary>
        // We need to provide BeginWriteAttribute, WriteAttributeValue and EndWriteAttribute APIs,
        // because the RazorCompiler in aspnetcore 5 will emit
        //
        // WriteLiteral("/// <summary>\n        /// Create a <see");
        // BeginWriteAttribute("cref", " cref=\"", 1604, "\"", 1626, 1);
        // WriteAttributeValue("", 1611, Model.TypeName, 1611, 15, false);
        // EndWriteAttribute();
        // WriteLiteral(@"/> value from the Domus text representation.
        // </summary>");

        private AttributeInfo _attributeInfo;

        /// <summary>
        /// Begins writing out an attribute.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="prefix">The prefix.</param>
        /// <param name="prefixOffset">The prefix offset.</param>
        /// <param name="suffix">The suffix.</param>
        /// <param name="suffixOffset">The suffix offset.</param>
        /// <param name="attributeValuesCount">The attribute values count.</param>
#pragma warning disable IDE0060 // warning IDE0060: Remove unused parameter
        public void BeginWriteAttribute(
            string name,
            string prefix,
            int prefixOffset,
            string suffix,
            int suffixOffset,
            int attributeValuesCount)
#pragma warning restore IDE0060
        {
            if (prefix == null)
            {
                throw new ArgumentNullException(nameof(prefix));
            }

            if (suffix == null)
            {
                throw new ArgumentNullException(nameof(suffix));
            }

            _attributeInfo = new AttributeInfo(name, prefix, suffix, attributeValuesCount);

            // Single valued attributes might be omitted in entirety if the attribute value strictly evaluates to
            // null or false. Consequently defer the prefix generation until we encounter the attribute value.
            if (attributeValuesCount != 1)
            {
                WriteLiteral(prefix);
            }
        }

        /// <summary>
        /// Writes out an attribute value.
        /// </summary>
        /// <param name="prefix">The prefix.</param>
        /// <param name="prefixOffset">The prefix offset.</param>
        /// <param name="value">The value.</param>
        /// <param name="valueOffset">The value offset.</param>
        /// <param name="valueLength">The value length.</param>
        /// <param name="isLiteral">Whether the attribute is a literal.</param>
#pragma warning disable IDE0060 // warning IDE0060: Remove unused parameter
        public void WriteAttributeValue(
            string? prefix,
            int prefixOffset,
            object? value,
            int valueOffset,
            int valueLength,
            bool isLiteral)
#pragma warning restore IDE0060
        {
            if (_attributeInfo.AttributeValuesCount == 1)
            {
                if (IsBoolFalseOrNullValue(prefix, value))
                {
                    // Value is either null or the bool 'false' with no prefix; don't render the attribute.
                    _attributeInfo.Suppressed = true;
                    return;
                }

                // We are not omitting the attribute. Write the prefix.
                WriteLiteral(_attributeInfo.Prefix);

                if (IsBoolTrueWithEmptyPrefixValue(prefix, value))
                {
                    // The value is just the bool 'true', write the attribute name instead of the string 'True'.
                    value = _attributeInfo.Name;
                }
            }

            // This block handles two cases.
            // 1. Single value with prefix.
            // 2. Multiple values with or without prefix.
            if (value is not null)
            {
                if (!string.IsNullOrEmpty(prefix))
                {
                    WriteLiteral(prefix);
                }

                WriteLiteral(value);
            }
        }

        /// <summary>
        /// Ends writing an attribute.
        /// </summary>
        public void EndWriteAttribute()
        {
            if (!_attributeInfo.Suppressed)
            {
                WriteLiteral(_attributeInfo.Suffix);
            }
        }

        private bool IsBoolFalseOrNullValue(string? prefix, object? value)
        {
            return string.IsNullOrEmpty(prefix) &&
                   (value is null ||
                    (value is bool b && !b));
        }

        private bool IsBoolTrueWithEmptyPrefixValue(string? prefix, object? value)
        {
            // If the value is just the bool 'true', use the attribute name as the value.
            return string.IsNullOrEmpty(prefix) && value is bool b && b;
        }

        private struct AttributeInfo
        {
            public AttributeInfo(
                string name,
                string prefix,
                string suffix,
                int attributeValuesCount)
            {
                Name = name;
                Prefix = prefix;
                Suffix = suffix;
                AttributeValuesCount = attributeValuesCount;
                Suppressed = false;
            }

            public int AttributeValuesCount { get; }

            public string Name { get; }

            public string Prefix { get; }

            public string Suffix { get; }

            public bool Suppressed { get; set; }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        // ReSharper disable once VirtualMemberNeverOverridden.Global
        public virtual async Task ExecuteAsync()
        {
            // whatever, we just need something that compiles...and it has to be a virtual method,
            // because it is overriden in all the derived printer classes
            //     error CS0506: 'dataenum.ExecuteAsync()': cannot override inherited member 'TemplateBase.ExecuteAsync()' because it is not marked virtual, abstract, or override
            await Task.Yield();
        }

        public async Task<string> RenderAsync(object model)
        {
            using var writer = new StringWriter();
            SetContext(writer, model);
            await ExecuteAsync().ConfigureAwait(false);
            return writer.ToString();
        }

        private void SetContext(TextWriter tw, object model)
        {
            Output = tw;
            Model = model;
        }

        public object Model { get; private set; } = null!;
    }

    public abstract class TemplateBase<TModel> : TemplateBase
    {
        public new TModel Model => (TModel)base.Model;
    }
}
