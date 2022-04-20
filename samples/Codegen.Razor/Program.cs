using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CSharpRazor;

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

namespace Codegen.Razor;

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

internal static class Program
{
    public static async Task<int> Main()
    {
        // denne bestemmelse af root er irriterende
        string targetProjectDirectory = Directory.GetCurrentDirectory();
        int pos = targetProjectDirectory.LastIndexOf("bin", StringComparison.OrdinalIgnoreCase);
        string rootDirectory = pos > 0
            ? targetProjectDirectory[..(pos - 1)]
            : targetProjectDirectory;

        var engine = new RazorEngineBuilder()
            .SetRootDirectory(rootDirectory)
            .Build();

        // Mangler en TypeModelProvider (pba json filer)
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
}
