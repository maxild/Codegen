using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace CSharpRazor
{
    /// <summary>
    /// Builder to override default configuration
    /// </summary>
    public class RazorEngineBuilder
    {
        private Assembly? _entryAssembly;
        private string? _namespace;
        private string? _baseType;
        private string? _rootDirectoryPath;
        private HashSet<MetadataReference>? _metadataReferences;

        /// <summary>
        /// The <see cref="Assembly"/> used to resolve references and compilation options ().
        /// </summary>
        /// <param name="entryAssembly">The 'root' <see cref="Assembly"/> with the CLR entry point.</param>
        public RazorEngineBuilder SetEntryAssembly(Assembly? entryAssembly)
        {
            _entryAssembly = entryAssembly;
            return this;
        }

        /// <summary>
        /// Override the namespace of the generated 'printer' type..
        /// </summary>
        /// <param name="namespace">The namespace of the generated 'printer' type.</param>
        public RazorEngineBuilder SetNamespace(string? @namespace)
        {
            _namespace = @namespace;
            return this;
        }

        /// <summary>
        /// Override the base type of the generated 'printer' type.
        /// </summary>
        /// <param name="baseType">the base type of the generated 'printer' type.</param>
        public RazorEngineBuilder SetBaseType(string? baseType)
        {
            _baseType = baseType;
            return this;
        }

        /// <summary>
        /// Override the base type of the generated 'printer' type.
        /// </summary>
        /// <typeparam name="TBaseType">the base type of the generated 'printer' type.</typeparam>
        // ReSharper disable once UnusedTypeParameter
        public RazorEngineBuilder SetBaseType<TBaseType>()
        {
            // TODO: Make unit test for TGeneric -> Typename
            throw new NotImplementedException();
        }

        public RazorEngineBuilder SetRootDirectory(string? rootDirectoryPath)
        {
            // TODO: rootDirectoryDir, TemplatesFolderSubPath...only used in RazorCompiler to find templates
            _rootDirectoryPath = rootDirectoryPath;
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
                _metadataReferences.Add(reference);
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

            if (_baseType is null)
            {
                throw new InvalidOperationException($"Uninitialized BaseType. You must always call {nameof(SetBaseType)} before calling {nameof(Build)}.");
            }

            Assembly? entryAssembly = _entryAssembly ?? Assembly.GetEntryAssembly();

            if (entryAssembly is null)
            {
                throw new InvalidOperationException($"Uninitialized EntryAssembly. You must always call {nameof(SetEntryAssembly)} before calling {nameof(Build)}.");
            }

            // template (cshtml) compiler that generates the C# source code of the 'printer' type.
            var razorCompiler = new RazorCompiler(_rootDirectoryPath, _namespace ?? "CSharpRazor.Views", _baseType);

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
}
