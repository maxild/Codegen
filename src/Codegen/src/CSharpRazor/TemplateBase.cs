using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace CSharpRazor
{
    /// <summary>
    /// The abstract base class for all our 'Razor DSL' templates that acts both
    /// as a 'global object' in every template (that can be accessed in C# mode: '@Model'),
    /// and as a 'printer' type that can render (See <see cref="RenderAsync"/>) our
    /// template to a string.
    /// </summary>
    public class TemplateBase
    {
        public TextWriter Output { get; private set; } = TextWriter.Null;

        public virtual void WriteLiteral(object? value)
        {
            WriteLiteral(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        public virtual void WriteLiteral(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                Output.Write(value);
            }
        }

        public virtual void Write(string? value)
        {
            WriteLiteral(value); // no html encoding
        }

        public virtual void Write(object? value)
        {
            WriteLiteral(value); // no html encoding
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual async Task ExecuteAsync()
        {
            await Task.Yield(); // whatever, we just need something that compiles...
        }

        public async Task<string> RenderAsync(object model)
        {
            using (var writer = new StringWriter())
            {
                SetContext(writer, model);
                await ExecuteAsync().ConfigureAwait(false);
                return writer.ToString();
            }
        }

        void SetContext(TextWriter tw, object model)
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
