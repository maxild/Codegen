using System;
using System.IO;

namespace CSharpRazor;

/// <summary>
/// This is a wrapper/descriptor that captures the Common Intermediate Language (CIL),
/// formerly called Microsoft Intermediate Language (MSIL), code generated by
/// the <see cref="RoslynCompiler"/> from the template source (e.g. hello.cshtml).
/// </summary>
public class CompiledTemplateILSource : ICompiledTemplateDescriptor
{
    private readonly CompiledTemplateCSharpSource _source;

    public CompiledTemplateILSource(
        CompiledTemplateCSharpSource source,
        MemoryStream rawAssembly,
        MemoryStream rawAssemblySymbols)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        RawAssembly = rawAssembly.GetBuffer();
        RawAssemblySymbols = rawAssemblySymbols.GetBuffer();
    }

    /// <inheritdoc />
    public string TemplateName => _source.TemplateName;

    /// <inheritdoc />
    public string TemplateFilename => _source.TemplateFilename;

    /// <inheritdoc />
    public string TypeName => _source.TypeName;

    /// <inheritdoc />
    public string SourceCSharpCode => _source.SourceCSharpCode;

    /// <summary>
    /// A byte array that is a COFF-based image containing an emitted assembly.
    /// </summary>
    public byte[] RawAssembly { get; }

    /// <summary>
    /// A byte array that contains the raw bytes representing the symbols for the assembly.
    /// </summary>
    public byte[] RawAssemblySymbols { get; }
}
