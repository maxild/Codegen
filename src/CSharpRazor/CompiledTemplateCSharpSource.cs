using System;
using System.IO;

namespace CSharpRazor;

/// <summary>
/// This is a wrapper/descriptor that captures the C# code
/// generated by the <see cref="RazorCompiler"/> from the
/// template source (e.g. hello.cshtml).
/// </summary>
public class CompiledTemplateCSharpSource : ICompiledTemplateDescriptor
{
    /// <summary>
    /// Create the result of using the <see cref="RazorCompiler"/> to generate the intermediate
    /// C# source code representation of TemplateBase derived type.
    /// </summary>
    /// <param name="templateFilename">
    /// The filename of the Razor template, including the file extension(e.g. hello.cshtml).
    /// </param>
    /// <param name="typeName">
    /// The fully qualified type name of the generated type, including its namespace but not its assembly.
    /// </param>
    /// <param name="generatedCSharpCode">
    /// The generated C# code containing the type that can render the result to a string.
    /// </param>
    public CompiledTemplateCSharpSource(
        string templateFilename,
        string typeName,
        string generatedCSharpCode)
    {
        TemplateFilename = templateFilename ?? throw new ArgumentNullException(nameof(templateFilename));
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        SourceCSharpCode = generatedCSharpCode ?? throw new ArgumentNullException(nameof(generatedCSharpCode));
    }

    /// <inheritdoc />
    public string TemplateName => Path.GetFileNameWithoutExtension(TemplateFilename);

    /// <inheritdoc />
    public string TemplateFilename { get; }

    /// <inheritdoc />
    public string TypeName { get; }

    /// <summary>
    /// The C# code generated by the <see cref="RazorCompiler"/> that will be dynamically compiled
    /// to an executable type that can ve used to render the template at runtime.
    /// </summary>
    public string SourceCSharpCode { get; }
}
