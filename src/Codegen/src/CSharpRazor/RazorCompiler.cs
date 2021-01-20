using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.AspNetCore.Razor.Language;

namespace CSharpRazor
{
    /// <summary>
    /// Razor parser and compiler that can generate code (Hello.cshtml -> Hello.g.cshtml.cs)
    /// containing derived 'template' type that can 'print/render' our result string/stream
    /// based on 'template-model'.
    /// </summary>
    public class RazorCompiler
    {
        private readonly RazorProjectEngine  _projectEngine;

        /// <summary>
        /// Create compiler
        /// </summary>
        /// <param name="rootDirectoryPath"></param>
        /// <param name="namespace">The namespace of the dynamically build 'printer' type.</param>
        /// <param name="baseType">The base type of the dynamically build 'printer' type.</param>
        public RazorCompiler(
            string rootDirectoryPath,
            string @namespace,
            string? baseType = null)
        {
            BaseType = baseType;
            Namespace = @namespace ?? throw new ArgumentNullException(nameof(@namespace));
            var fileSystem = RazorProjectFileSystem.Create(rootDirectoryPath ??
                                                           throw new ArgumentNullException(nameof(rootDirectoryPath)));
            var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, fileSystem, builder =>
            {
                _ = builder
                    // This define global namespace and basetype common to all compilations
                    .SetNamespace(Namespace)
                    .SetBaseType(BaseType) // can be null...@inherits wil win...test that!
                    .ConfigureClass((document, @class) =>
                    {
                        // Convention: Filename equals typename and must be unique
                        @class.ClassName = Path.GetFileNameWithoutExtension(document.Source.FilePath);
                        @class.Modifiers.Clear();
                        @class.Modifiers.Add("public"); // internal
                    });

                //--------------------------------------------------------------------------------
                // The @functions directive enables a Razor Page to add a C# code block to a view
                //
                // @functions {
                //    public string GetHello()
                //    {
                //        return "Hello";
                //    }
                // }
                //--------------------------------------------------------------------------------
                // FunctionsDirective.Register(builder);

                //--------------------------------------------------------------------------------
                // The @inherits directive provides full control of the class the view inherits:
                //
                // @inherits RazorTemplate<SomeModel>
                //--------------------------------------------------------------------------------
                // InheritsDirective.Register(builder);

                //--------------------------------------------------------------------------------
                // The @section directive is used in conjunction with the layout to enable views
                // to render content in different parts of the HTML page.
                //--------------------------------------------------------------------------------
                // We don't need layout, sections, includes, partials
                //SectionDirective.Register(builder);


                // Avoid compilation error CS0234:
                //   The type or namespace name 'Hosting' does not exist in the namespace
                //   'Microsoft.AspNetCore.Razor' (are you missing an assembly reference?),
                //   because Razor (by default!!!!) is generating
                //       * RazorCompiledItemAttribute
                //       * RazorSourceChecksumAttribute
                // both found in Microsoft.AspNetCore.Razor.Runtime (thta we don't want to depend on)
                builder.Features.Add(new SuppressChecksumOptionsFeature());
                builder.Features.Add(new SuppressMetadataAttributesFeature());

                // TODO: Hvad er det?
                //builder.Features.Remove(builder.Features.OfType<IRazorDocumentClassifierPass>().Single());

                // Den behoever vi ikke, da vi ikke benytter C# Scripting
                //builder.Features.Add(new CSharpScriptDocumentClassifierPass());
            });

            _projectEngine = projectEngine;
        }

        public string Namespace { get; }

        public string? BaseType { get; }

        // TODO: Inline content for testing is not supported, only file content
        public CompiledTemplateCSharpSource CompileTemplate(string templatePath)
        {
            // TODO: extension is hardcoded, should live in config/convention type
            var templatePathToUse = templatePath.EndsWith(".cshtml") ?
                templatePath :
                string.Concat(templatePath, ".cshtml");

            // If fileKind is null, the document kind will be inferred from the file extension.
            //   .razor -> "component"
            //   _Imports.razor -> "componentImport"
            //   _ (wildcard/other) -> "mvc"
            // TODO: maybe create custom file kind?
            var razorProjectItem = _projectEngine.FileSystem.GetItem(templatePathToUse, null);

            // RazorCodeDocument contains the abstract representation of your template (AST)
            var razorCodeDocument = _projectEngine.Process(razorProjectItem);

            // RazorCSharpDocument contains the final produced C# code
            var razorCSharpDocument = razorCodeDocument.GetCSharpDocument();

            if (razorCSharpDocument.Diagnostics.Count > 0)
            {
                var diagnostics = string.Join(Environment.NewLine, razorCSharpDocument.Diagnostics);
                throw new InvalidOperationException(
                    $"One or more parse errors encountered: {Environment.NewLine}{diagnostics}.");
            }

            string templateName = Path.GetFileNameWithoutExtension(razorCodeDocument.Source.FilePath);
            string typeName = Namespace + "." + templateName;

            // Note: razorCodeDocument.Source is not passed on to the result
            return new CompiledTemplateCSharpSource(
                templateFilename: templatePathToUse,
                typeName,
                razorCSharpDocument.GeneratedCode);
        }

        private class SuppressChecksumOptionsFeature : RazorEngineFeatureBase, IConfigureRazorCodeGenerationOptionsFeature
        {
            [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
            public int Order { get; set; }

            public void Configure(RazorCodeGenerationOptionsBuilder options)
            {
                if (options is null)
                {
                    throw new ArgumentNullException(nameof(options));
                }

                // We don't want to depend on Microsoft.AspNetCore.Razor.Hosting.RazorSourceChecksumAttribute
                // defined in Microsoft.AspNetCore.Razor.Runtime
                options.SuppressChecksum = true;
            }
        }

        private class SuppressMetadataAttributesFeature : RazorEngineFeatureBase, IConfigureRazorCodeGenerationOptionsFeature
        {
            [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
            public int Order { get; set; }

            public void Configure(RazorCodeGenerationOptionsBuilder options)
            {
                if (options is null)
                {
                    throw new ArgumentNullException(nameof(options));
                }

                // We don't want to depend on Microsoft.AspNetCore.Razor.Hosting.RazorCompiledItemAttribute
                // defined in Microsoft.AspNetCore.Razor.Runtime
                options.SuppressMetadataAttributes = true;
            }
        }
    }
}
