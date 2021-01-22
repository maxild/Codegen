using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace CSharpRazor
{
    // Immutable...created by builder
    public class RazorEngine
    {
        // TODO: maybe use ConcurrentDictionary and be done with it...no locking in this class
        //private readonly ConcurrentDictionary<string, CompiledTemplate> _compiledTemplates
        //    = new ConcurrentDictionary<string, CompiledTemplate>(StringComparer.Ordinal);

        // TODO: Should these fields be static?
        // TODO: Should multiple instances of the engine share ALC and compiled template cache
        private static readonly Dictionary<string, CompiledTemplate> s_compiledTemplateCache
            = new(StringComparer.Ordinal);
        private static readonly object s_lockTemplateCache = new();

        internal RazorEngine(Assembly entryAssembly, RazorCompiler razorCompiler, RoslynCompiler roslynCompiler)
        {
            EntryAssembly = entryAssembly ?? throw new ArgumentNullException(nameof(entryAssembly));
            RazorCompiler = razorCompiler ?? throw new ArgumentNullException(nameof(razorCompiler));
            RoslynCompiler = roslynCompiler ?? throw new ArgumentNullException(nameof(roslynCompiler));
        }

        /// <summary>
        /// Gets the <see cref="Assembly"/> of the <see cref="RazorEngine"/> used to resolve
        /// dependencies (references) and compilation options-- This should be your entry point
        /// <see cref="Assembly"/>.
        /// </summary>
        public Assembly EntryAssembly { [UsedImplicitly] get; }

        public RazorCompiler RazorCompiler { get; }

        public RoslynCompiler RoslynCompiler { get; }

        /// <summary>
        /// Get a compiled template for a given unique templatePath -- invoke the
        /// compiler pipeline if the template have not been compiled already.
        /// </summary>
        /// <param name="templatePath">Unique templatePath of the template.</param>
        /// <returns>A compiled template that can render models into text.</returns>
        public CompiledTemplate GetCompiledTemplate(string templatePath)
        {
            // Compile
            CompiledTemplate? compiledTemplate;
            lock (s_lockTemplateCache)
            {
                if (!s_compiledTemplateCache.TryGetValue(templatePath, out compiledTemplate))
                {
                    // compile + emit
                    var compiledTemplateCSharpSource = RazorCompiler.CompileTemplate(templatePath);
                    var compiledTemplateILSource = RoslynCompiler.CompileAndEmit(compiledTemplateCSharpSource);
                    // load emitted code into new (anonymous) load context (ALC) and cache the thing
                    compiledTemplate = new CompiledTemplate(compiledTemplateILSource);
                    s_compiledTemplateCache.Add(templatePath, compiledTemplate);
                }
            }

            // Compile
            //var compiledTemplate = _compiledTemplates.GetOrAdd(templatePath, key =>
            //{
            //    var compiledTemplateCSharpSource = RazorCompiler.CompileTemplate(key);
            //    var compiledTemplateILSource = RoslynCompiler.CompileAndEmit(compiledTemplateCSharpSource);
            //    return new CompiledTemplate(compiledTemplateILSource);
            //});

            return compiledTemplate;
        }

        /// <summary>
        /// Compiles and renders a template with a given <paramref name="templatePath"/>
        /// </summary>
        /// <param name="templatePath">Unique templatePath of the template</param>
        /// <param name="model">The model instance</param>
        /// <returns>Rendered template as a string result</returns>
        public async Task<RenderResult> RenderTemplateAsync(string templatePath, object model)
        {
            // Compile
            CompiledTemplate compiledTemplate = GetCompiledTemplate(templatePath);

            // Render
            string cSharpCode = await compiledTemplate.RenderAsync(model);
            return new RenderResult(compiledTemplate, cSharpCode);
        }

        // TODO: Implement this one,closer to real printf/string.Format
        ///// <summary>
        ///// Compiles and renders a template. Template content is taken directly from <paramref name="content"/> parameter
        ///// </summary>
        ///// <param name="key">Unique templatePath of the template</param>
        ///// <param name="content">Content of the template</param>
        ///// <param name="model">Template model</param>
        //public Task<string> RenderTemplateStringAsync(string key, string content, object model)
        //{
        //    // TODO: This one is for integration/functional testing without a file system, and caching
        //    // Notes
        //    //   1. you cannot unload Assemblies
        //    //   2. That said, you can load the same Assembly again, even if the prior one was not unloaded
        //    throw new NotImplementedException();
        //}
    }
}
