using System;

namespace CSharpRazor
{
    /// <summary>
    /// This is a wrapper/descriptor of the finished C# code produced
    /// by the overall system represented by <see cref="RazorEngine"/>.
    /// </summary>
    public class RenderResult : IRenderedTemplateDescriptor
    {
        public RenderResult(CompiledTemplate source, string content)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            TemplateName = source.TemplateName;
            TemplateFilename = source.TemplateFilename;
            TypeName = source.TypeName;
            SourceCSharpCode = source.SourceCSharpCode;
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        /// <inheritdoc />
        public string TemplateName { get; }

        /// <inheritdoc />
        public string TemplateFilename { get; }

        /// <inheritdoc />
        public string TypeName { get; }

        /// <inheritdoc />
        public string SourceCSharpCode { get; }

        /// <inheritdoc />
        public string Content { get; }
    }
}
