using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CSharpRazor;
using Microsoft.CodeAnalysis;

// Idea and Inspiration: https://daveaglick.com/posts/the-bleeding-edge-of-razor

// Notes
// 1. An important distinction that I want to make here is that Razor is not the set of HTML helpers and other
//    support functionality that comes along with ASP.NET MVC. For example, helpers like Html.Partial() and
//    page directives like@section aren't part of the Razor language.
// 2. the ASP.NET team has been focusing on separating Razor the language from Razor for ASP.NET MVC. This is
//    partly out of necessity as Razor has grown to support at least three different dialects (ASP.NET MVC,
//    Razor Pages, and Blazor), but it also makes using Razor for your own purposes easier too.
// 3. I think they've now created a standalone version of the Razor language published on nuget.org with the package
//    Microsoft.AspNetCore.Razor.Language. This package contains the parser and compiler (code generation), and
//    is therefore the pure language. That is Razor without aspnetcore runtime. So the ASP.NET team have separated
//    the runtime assemblies from the compiler assemblies.
// 4. Runtime Assemblies (maybe there are more...?)
//      Microsoft.AspNetCore.Html.Abstractions (IHtmlContent)
//      Microsoft.AspNetCore.Razor (a few enums)
//      Microsoft.AspNetCore.Razor.Runtime (Tag Helper runtime)

namespace Codegen.Razor
{
    public class MyModel
    {
        public string Name { get; set; } = "Killroy";
    }

    public class DayOfWeekModel
    {
        private readonly Func<DayOfWeek, bool> _predicate;

        public DayOfWeekModel(Func<DayOfWeek, bool>? predicate)
        {
            _predicate = predicate ?? (_ => true);
        }

        public IEnumerable<DayOfWeek> GivenWeekDays()
        {
            return Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().Where(day => _predicate(day));
        }
    }

    public static class DayOfWeekExtensions
    {
        public static bool IsWeekend(this DayOfWeek d)
        {
            return d is DayOfWeek.Saturday or DayOfWeek.Sunday;
        }

        public static bool IsNotWeekend(this DayOfWeek d)
        {
            return d is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        }
    }

    static class Program
    {
        public static async Task<int> Main()
        {
            // TODO: denne bestemmelse af root er irriterende
            string targetProjectDirectory = Directory.GetCurrentDirectory();
            int pos = targetProjectDirectory.LastIndexOf("bin", StringComparison.OrdinalIgnoreCase);
            string rootDirectory = pos > 0
                ? targetProjectDirectory.Substring(0, pos - 1)
                : targetProjectDirectory;

            var engine = new RazorEngineBuilder()
                .SetRootDirectory(rootDirectory)
                .Build();

            // TODO: Mangler en TypeModelProvider (pba json filer)
            var templatePaths = new Dictionary<string, object>
            {
                { "RazorSource.cshtml", new DayOfWeekModel(day => day.IsNotWeekend())},
                { "hello.cshtml", new MyModel { Name = "Morten Maxild"}}
            };

            foreach (var kvp in templatePaths)
            {
                // Benytter un-typed model here, because we do not have TypeProvider
                var renderResult = await engine.RenderTemplateAsync(kvp.Key,
                    kvp.Value);

                // Save g.cshtml.cs file
                await File.WriteAllTextAsync(
                    Path.Combine(rootDirectory, renderResult.TemplateName + ".g.cshtml.cs"),
                    renderResult.SourceCSharpCode);

                // Save generated.cs file
                await File.WriteAllTextAsync(
                    Path.Combine(rootDirectory, renderResult.TemplateName + ".generated.cs"),
                    renderResult.Content);
            }

            return 0;
        }

        public static async Task<int> OldMain()
        {
            string targetProjectDirectory = Directory.GetCurrentDirectory();
            int pos = targetProjectDirectory.LastIndexOf("bin", StringComparison.OrdinalIgnoreCase);
            string rootDirectory = pos > 0
                ? targetProjectDirectory.Substring(0, pos - 1)
                : targetProjectDirectory;

            const string TEMPLATE_PATH = "RazorSource.cshtml";
            //const string TEMPLATE_PATH = "hello.cshtml";

            var engine = new RazorEngineBuilder()
                .SetRootDirectory(rootDirectory)
                .AddAdditionalReferences(GetAdditionalRuntimeReferences().ToArray())
                .Build();

            var renderResult = await engine.RenderTemplateAsync(TEMPLATE_PATH,
                new DayOfWeekModel(day => day.IsNotWeekend()));

            // Save g.cshtml.cs file
            await File.WriteAllTextAsync(
                Path.Combine(rootDirectory, renderResult.TemplateName + ".g.cshtml.cs"),
                renderResult.SourceCSharpCode);

            // Save generated.cs file
            await File.WriteAllTextAsync(
                Path.Combine(rootDirectory, Path.Combine(rootDirectory, renderResult.TemplateName + ".generated.cs")),
                renderResult.Content);

            //
            // Razor
            //

            //var razorCompiler = new RazorCompiler(rootDirectory,
            //    @namespace: "Maxfire.SpikeForCodeGenWithRazor", baseType: null);
            //var compiledTemplateCSharpSource = razorCompiler.CompileTemplate(TEMPLATE_PATH);

            //
            // Roslyn
            //

            //var assembly = Assembly.GetEntryAssembly();
            //var referencePathResolver = new DepsFileReferencePathResolver(assembly);
            //var compilerOptionsResolver = new DepsFileCompilationOptionsResolver(assembly);

            //var roslynCompiler = new RoslynCompiler(compilerOptionsResolver.CompilationOptions)
            //        .WithParseOptions(compilerOptionsResolver.ParseOptions)
            //        .WithEmitOptions(compilerOptionsResolver.EmitOptions)
            //        .WithReferencePaths(referencePathResolver.GetReferencePaths())       // deps file resolved
            //        // INVESTIGATE: Attempting to use runtime assemblies as compile references
            //        //              is not really supported in .Net Core and will possibly lead to compile
            //        //              errors due to the structure of the runtime assemblies (type forwarding).
            //        //              The job of finding the set of references (and resolving conflicts) is
            //        //              normally up to MSBuild, but here we catch at runtime time after the
            //        //              MSBuild references have been resolved.
            //        .WithAdditionalReferences(GetAdditionalRuntimeReferences()) // runtime/reflection resolved
            //        ;

            {
                // BLOCK to show possible problems with duplicates, type forwarders, reference assemblies etc..

                // mscorlib, Private.CoreLib....many inconsistent core libs w.r.t. compilation in .Net Core!!!
                var corLibReferences = engine.RoslynCompiler.MetadataReferences
                    .Where(peRef =>
                        new Regex("Private.CoreLib|mscorlib", RegexOptions.IgnoreCase).IsMatch(
                            Path.GetFileName(peRef.Display) ?? string.Empty));

                Console.WriteLine("'corlib' references:");
                foreach (var reference in corLibReferences)
                {
                    Console.WriteLine(reference.Display);
                }

                var netstandardReferences = engine.RoslynCompiler.MetadataReferences
                    .Where(peRef =>
                        Path.GetFileName(peRef.Display)?.Contains("netstandard", StringComparison.OrdinalIgnoreCase) ?? false);

                Console.WriteLine("netstandard references:");
                foreach (var reference in netstandardReferences)
                {
                    Console.WriteLine(reference.Display);
                }
            }

            //var compiledTemplateILSource = roslynCompiler.CompileAndEmit(compiledTemplateCSharpSource);

            //var compiledTemplate = new CompiledTemplate(compiledTemplateILSource);

            //Console.WriteLine("Call the 'print/render' method on the instance.");

            //var cSharpCode = await compiledTemplate.RenderAsync(new DayOfWeekModel(day => day.IsNotWeekend()));
            //var cSharpCode = await compiledTemplate.RenderAsync(new MyModel { Name = "Morten Maxild" }));

            // RazorSource.Generated.cs
            //string codegenFilename = compiledTemplate.TemplateName + ".generated.cs";

            Console.WriteLine();
            Console.WriteLine("The C# code was successfully generated.");
            Console.WriteLine();

            return 0;
        }

        private static IReadOnlyList<MetadataReference> GetAdditionalRuntimeReferences() =>
            new MetadataReference[]
            {
                // BUG [1]: CS8021: No value for RuntimeMetadataVersion found. No assembly containing
                //      System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                // NOTE:
                // This behavior is "By Design". On .Net Core all of the core types are actually located in a
                // private DLL (System.Private.CorLib.dll). The mscorlib library, at runtime, is largely a
                // collection of type forwarders. The C# compiler is unable to resolve these type forwarders
                // and hence issues an error because it can't locate System.Object.

                // BUG: Don't do this (see above [1])
                //MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // System.Private.CorLib.dll

                // BUG: Don't do this either (see above [1])
                //MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll")),

                // This one will be ignored, because it is a duplicate that is already resolved by reading deps file
                //MetadataReference.CreateFromFile(typeof(RazorCompiler).Assembly.Location), // CSharpRazor.dll

                // This one will be ignored, because it is a duplicate that is already resolved by reading deps file
                //MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location), // Codegen.Razor.dll

                // RazorCompiledItemAttribute is from Microsoft.AspNetCore.Razor.Runtime, but we do NOT need it.
                //MetadataReference.CreateFromFile(typeof(RazorCompiledItemAttribute).Assembly.Location),

                // In order to get it working in netstandard 2.0 class library running in netcore2 app
                // on mac and linux I had to add an additional reference to netstandard dll:
                // ReSharper disable once AssignNullToNotNullAttribute
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "netstandard.dll")) // 2.x.y/netstandard.dll (netstandard part of netcoreapp2.x.y)
            };

    }
}
