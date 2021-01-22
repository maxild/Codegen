#nullable disable
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Shouldly;
using Xunit;

namespace RazorLearningTests
{
    public class RazorParserTests
    {
        [Fact]
        public void ParseBlackBox()
        {
            var razorCode =
//0123456789012345678901234567
@"@inherits MyBase<MyModel>

Hello @Model.Name

<!-- some comment -->
<p>Some text</p>

@functions {
    string Print(MyModel m)
    {
        return m.ToString();
    }
}
";

            var document = RazorSourceDocument.Create(razorCode, fileName: null);

            var options = RazorParserOptions.Create(builder =>
            {
                foreach (var directive in GetDirectives())
                {
                    builder.Directives.Add(directive);
                }
            });

            var syntaxTree = RazorSyntaxTree.Parse(document, options);

            syntaxTree.Source.FilePath.ShouldBeNull();
        }

        [Fact]
        public void Parse()
        {
            var razorCode =
                //0123456789012345678901234567
                @"@inherits MyBase<MyModel>

Hello @Model.Name

<!-- some comment -->
<p>Some text</p>

@functions {
    string Print(MyModel m)
    {
        return m.ToString();
    }
}
";

            var document = RazorSourceDocument.Create(razorCode, fileName: null);

            var options = RazorParserOptions.Create(builder =>
            {
                foreach (var directive in GetDirectives())
                {
                    builder.Directives.Add(directive);
                }
            });

            // Det foelgende svarer til
            //    RazorSyntaxTree syntaxTree = RazorSyntaxTree.Parse(document, options);

            var context = new ParserContext(document, options);
            var codeParser = new CSharpCodeParser(GetDirectives(), context);
            var markupParser = new HtmlMarkupParser(context);

            codeParser.HtmlParser = markupParser;
            markupParser.CodeParser = codeParser;

            // root node is called syntaxTree
            SyntaxNode syntaxTree = markupParser.ParseDocument().CreateRed();

            var result = NewTreeSerializer.Serialize(syntaxTree);

            result.ShouldNotBeNull();
        }

        [Fact]
        public void ParseIntermediateRepresentation()
        {
            var razorCode =
                //0123456789012345678901234567
                @"@inherits MyBase<MyModel>

Hello @Model.Name

<!-- some comment -->
<p>Some text</p>

@functions {
    string Print(MyModel m)
    {
        return m.ToString();
    }
}
";

            //var document = RazorSourceDocument.Create(razorCode, fileName: null);

            //var options = RazorParserOptions.Create(builder => {
            //    foreach (var directive in GetDirectives())
            //    {
            //        builder.Directives.Add(directive);
            //    }
            //});

            //RazorSyntaxTree syntaxTree = RazorSyntaxTree.Parse(document, options);

            RazorSourceDocument sourceDocument = CreateSourceDocument(razorCode);

            var engine = RazorProjectEngine.Create(RazorConfiguration.Default, RazorProjectFileSystem.Empty, configure: null);
            var codeDocument = engine.Process(sourceDocument, fileKind: null, importSources: null, tagHelpers: null);

            var irDocument = codeDocument.GetDocumentIntermediateNode();

            var formatter = new DebuggerDisplayFormatter();
            formatter.FormatTree(irDocument);
            string output = formatter.ToString();

            output.ShouldNotBeNull();
        }

        private static RazorSourceDocument CreateSourceDocument(
            string content = "Hello, world!",
            Encoding encoding = null,
            bool normalizeNewLines = false,
            string filePath = "test.cshtml",
            string relativePath = "test.cshtml")
        {
            if (normalizeNewLines)
            {
                content = NormalizeNewLines(content);
            }

            var properties = new RazorSourceDocumentProperties(filePath, relativePath);
            return new StringSourceDocument(content, encoding ?? Encoding.UTF8, properties);
        }

        private static string NormalizeNewLines(string content, string replaceWith = "\r\n")
        {
            return Regex.Replace(content, "(?<!\r)\n", replaceWith, RegexOptions.None, TimeSpan.FromSeconds(10));
        }

        private static IEnumerable<DirectiveDescriptor> GetDirectives()
        {
            var directives = new[]
            {
                DirectiveDescriptor.CreateDirective(
                    "inject",
                    DirectiveKind.SingleLine,
                    builder =>
                    {
                        _ = builder
                            .AddTypeToken()
                            .AddMemberToken();

                        builder.Usage = DirectiveUsage.FileScopedMultipleOccurring;
                    }),
                DirectiveDescriptor.CreateDirective(
                    "model",
                    DirectiveKind.SingleLine,
                    builder =>
                    {
                        _ = builder.AddTypeToken();
                        builder.Usage = DirectiveUsage.FileScopedSinglyOccurring;
                    }),
                DirectiveDescriptor.CreateDirective(
                    "namespace",
                    DirectiveKind.SingleLine,
                    builder =>
                    {
                        _ = builder.AddNamespaceToken();
                        builder.Usage = DirectiveUsage.FileScopedSinglyOccurring;
                    }),
                DirectiveDescriptor.CreateDirective(
                    "page",
                    DirectiveKind.SingleLine,
                    builder =>
                    {
                        _ = builder.AddOptionalStringToken();
                        builder.Usage = DirectiveUsage.FileScopedSinglyOccurring;
                    }),
                DirectiveDescriptor.CreateDirective(
                    "functions",
                    DirectiveKind.SingleLine,
                    builder =>
                    {
                        _ = builder.AddTypeToken();
                        builder.Usage = DirectiveUsage.FileScopedSinglyOccurring;
                    }),
                DirectiveDescriptor.CreateDirective(
                    "inherits",
                    DirectiveKind.SingleLine,
                    builder =>
                    {
                        _ = builder.AddTypeToken();
                        builder.Usage = DirectiveUsage.FileScopedSinglyOccurring;
                    }),
                DirectiveDescriptor.CreateDirective(
                    "section",
                    DirectiveKind.RazorBlock,
                    builder => builder.AddMemberToken())
            };

            return directives;
        }
    }


}
