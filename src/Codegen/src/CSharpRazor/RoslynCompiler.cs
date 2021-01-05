using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace CSharpRazor
{
    /// <summary>
    /// Roslyn C# compiler to turn generated code into executable assembly that can render/print
    /// the result of the code generation.
    /// </summary>
    public class RoslynCompiler
    {
        private CSharpParseOptions? _parseOptions;
        private EmitOptions? _emitOptions;
        private readonly List<MetadataReference> _metadataReferences;

        // AVOID duplicates, because of possible compile errors:
        // PE file     => file path
        // Compilation => Assembly name
        private readonly HashSet<string> _libPaths = new(StringComparer.OrdinalIgnoreCase);

        public RoslynCompiler(CSharpCompilationOptions compilationOptions)
        {
            CompilationOptions = compilationOptions ?? throw new ArgumentNullException(nameof(compilationOptions));
            _metadataReferences = new List<MetadataReference>();
        }

        public IReadOnlyList<MetadataReference> MetadataReferences => _metadataReferences;

        public CSharpCompilationOptions CompilationOptions { get; }

        public CSharpParseOptions ParseOptions => _parseOptions ?? CSharpParseOptions.Default;

        public EmitOptions EmitOptions =>
            _emitOptions ?? new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);

        public RoslynCompiler WithReferencePaths(IEnumerable<string> referencePaths)
        {
            foreach (string path in referencePaths)
            {
                var metadataReference = MetadataReference.CreateFromFile(path);
                if (metadataReference.Display is not null && _libPaths.Add(metadataReference.Display))
                {
                    _metadataReferences.Add(metadataReference);
                }
            }

            return this;
        }

        public RoslynCompiler WithAdditionalReferences(IEnumerable<MetadataReference> metadataReferences)
        {
            foreach (var metadataReference in metadataReferences)
            {
                if (metadataReference.Display is not null && _libPaths.Add(metadataReference.Display))
                {
                    _metadataReferences.Add(metadataReference);
                }
            }

            return this;
        }

        public RoslynCompiler WithParseOptions(CSharpParseOptions options)
        {
            _parseOptions = options;
            return this;
        }

        public RoslynCompiler WithEmitOptions(EmitOptions options)
        {
            _emitOptions = options;
            return this;
        }

        public CompiledTemplateILSource CompileAndEmit(CompiledTemplateCSharpSource source)
        {
            //
            // Phase 1: Create compilation
            //

            // Parse the code
            var sourceText = SourceText.From(source.SourceCSharpCode, Encoding.UTF8);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, ParseOptions);

            // Configure the compiler
            var compilation = CSharpCompilation.Create(assemblyName: source.TemplateName)
                .WithOptions(CompilationOptions)
                .AddReferences(MetadataReferences)
                .AddSyntaxTrees(syntaxTree);

            //
            // Phase 2: Compiling into in-memory streams
            //

            using (var assemblyStream = new MemoryStream())
            using (var pdbStream = new MemoryStream())
            {
                var emitResult = compilation.Emit(
                    assemblyStream,
                    pdbStream,
                    options: EmitOptions);

                if (!emitResult.Success)
                {
                    throw new ApplicationException(source.SourceCSharpCode + Environment.NewLine +
                                                   string.Join(Environment.NewLine,
                                                       emitResult.Diagnostics.Select(x => x.ToString())));
                }

                //if (!emitResult.Success)
                //{
                //    List<Diagnostic> errorsDiagnostics = emitResult.Diagnostics
                //        .Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error)
                //        .ToList();
                //    foreach (Diagnostic diagnostic in errorsDiagnostics)
                //    {
                //        FileLinePositionSpan lineSpan =
                //            diagnostic.Location.SourceTree.GetMappedLineSpan(
                //                diagnostic.Location.SourceSpan);
                //        string errorMessage = diagnostic.GetMessage();
                //        string formattedMessage =
                //            "("
                //            + lineSpan.StartLinePosition.Line
                //            + ":"
                //            + lineSpan.StartLinePosition.Character
                //            + ") "
                //            + errorMessage;
                //        Console.WriteLine(formattedMessage);
                //    }
                //    return;
                //}

                return new CompiledTemplateILSource(
                    source,
                    assemblyStream,
                    pdbStream);
            }
        }
    }
}
