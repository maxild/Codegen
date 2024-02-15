using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace CSharpRazor;

/// <summary>
/// Builder to override default configuration
/// </summary>
public class RazorEngineBuilder
{
    private const string DEFAULT_NAMESPACE = "CSharpRazor.Views";

    private Assembly? _entryAssembly;
    private string? _namespace;
    private string? _baseType;
    private string? _rootDirectoryPath;
    private HashSet<MetadataReference>? _metadataReferences;

    /// <summary>
    /// The <see cref="Assembly"/> used to resolve references and compilation options. EntryAssembly
    /// does not have to be set (i.e. is optional). It defaults to
    /// <see cref="Assembly.GetEntryAssembly()"/> in the BCL.
    /// </summary>
    /// <param name="entryAssembly">The 'root' <see cref="Assembly"/> with the CLR entry point.</param>
    public RazorEngineBuilder SetEntryAssembly(Assembly? entryAssembly)
    {
        _entryAssembly = entryAssembly;
        return this;
    }

    // TODO: Rename to SetDefaultNamespace
    // TODO: Ensure '@cg-Namespace' directive will win over any configured default namespace.
    /// <summary>
    /// Set the default namespace of the generated 'printer' type.
    /// The default namespace does not have to be set (i.e. is optional).
    /// The default namespace defaults to 'CSharpRazor.Views'.
    /// The '*.cssql' sql files can define the namespace using the '@cg-Namespace' directive.
    /// </summary>
    /// <param name="namespace">The namespace of the generated 'printer' type.</param>
    public RazorEngineBuilder SetNamespace(string? @namespace)
    {
        _namespace = @namespace;
        return this;
    }

    // TODO: Rename to SetDefaultBaseType
    // TODO: Ensure that @inherits directive will win over any configured default base type!!!
    // TODO: What is the base type if neither defined here or via @inherits directive?
    /// <summary>
    /// Set the default base type for the generated 'printer' type.
    /// The default base type does not have to be set (i.e. is optional).
    /// It defaults to empty/null.
    /// The '*.cshtml' razor template files can define the base type using the '@inherits' directive.
    /// </summary>
    /// <param name="baseType">the base type of the generated 'printer' type.</param>
    public RazorEngineBuilder SetBaseType(string? baseType)
    {
        _baseType = baseType;
        return this;
    }

    public RazorEngineBuilder SetBaseType<TBaseType>()
    {
        // TODO: Make unit test for TGeneric -> Typename
        throw new NotImplementedException();
    }

    // TODO: Rename to SetTemplateDirectory
    /// <summary>
    /// Set the root directory where (*.cshtml) razor template files can be found.
    /// The root directory must be set (i.e. is required).
    /// </summary>
    /// <param name="rootDirectoryPath">The directory where (*.cshtml) razor template files can be found.</param>
    public RazorEngineBuilder SetRootDirectory(string rootDirectoryPath)
    {
        // Used in RazorCompiler to find templates
        _rootDirectoryPath = rootDirectoryPath ?? throw new ArgumentNullException(nameof(rootDirectoryPath));
        return this;
    }

    /// <summary>
    /// Add additional references to the dynamic compilation system.
    /// </summary>
    /// <remarks>
    /// When the dynamic compilation system internally compiles your Razor generated C# code,
    /// it will load all the assemblies defined when you compiled the <see cref="RazorEngine.EntryAssembly"/>
    /// assembly. This can be cleverly done because you have compiled your <see cref="RazorEngine.EntryAssembly"/>
    /// with PreserveCompilationContext set to true in the *.csproj file. This is the default strategy for
    /// resolving all the necessary dependencies to avoid compilation errors, and this should work for most cases.
    /// If the compilation system need additional references to avoid compiler errors, you can pass additional
    /// references to the <see cref="RoslynCompiler"/> using this method.
    /// </remarks>
    /// <param name="references">One or more additional references.</param>
    public RazorEngineBuilder AddAdditionalReferences(params MetadataReference[] references)
    {
        if (references is null)
        {
            throw new ArgumentNullException(nameof(references));
        }

        _metadataReferences = new HashSet<MetadataReference>();

        foreach (var reference in references)
        {
            _ = _metadataReferences.Add(reference);
        }

        return this;
    }

    public RazorEngine Build()
    {
        // TODO: Are there any good default for root of project file system?
        if (_rootDirectoryPath is null)
        {
            throw new InvalidOperationException($"Uninitialized RootDirectory. You must always call {nameof(SetRootDirectory)} before calling {nameof(Build)}.");
        }

        Assembly? entryAssembly = _entryAssembly ?? Assembly.GetEntryAssembly();

        if (entryAssembly is null)
        {
            throw new InvalidOperationException($"Uninitialized EntryAssembly. You must always call {nameof(SetEntryAssembly)} before calling {nameof(Build)}.");
        }

        // template (cshtml) compiler that generates the C# source code of the 'printer' type.
        var razorCompiler = new RazorCompiler(_rootDirectoryPath, _namespace ?? DEFAULT_NAMESPACE, _baseType);

        // .deps file info for the C# compiler
        var referencePathResolver = new DepsFileReferencePathResolver(entryAssembly);
        var compilationOptionsResolver = new DepsFileCompilationOptionsResolver(entryAssembly);

        // C# compiler that compiles the C# source code of the 'printer' type into executable code (assembly).
        var roslynCompiler = new RoslynCompiler(compilationOptionsResolver.CompilationOptions)
            .WithParseOptions(compilationOptionsResolver.ParseOptions)
            .WithEmitOptions(compilationOptionsResolver.EmitOptions)
            .WithReferencePaths(referencePathResolver.GetReferencePaths())
            .WithAdditionalReferences(_metadataReferences ?? Enumerable.Empty<MetadataReference>());

        return new RazorEngine(entryAssembly, razorCompiler, roslynCompiler);
    }
}
