#nullable disable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Newtonsoft.Json;

namespace RazorLearningTests
{
    internal static class NewTreeSerializer
    {
        public static string Serialize(SyntaxNode node)
        {
            var rootNode = new Node();

            Visit(node, rootNode);

            var result = JsonConvert.SerializeObject(rootNode);

            return result;
        }

        internal static void Visit(SyntaxNode root, Node node)
        {
            if (!root.IsList)
            {
                var content = GetNodeContent(root);
                node.Content = Normalize(content);
                node.Start = root.Position;
                node.Length = root.FullWidth;
            }

            if (root.SlotCount > 0 &&
                !root.GetType().Name.EndsWith("LiteralSyntax"))
            {
                for (var i = 0; i < root.SlotCount; i++)
                {
                    var child = root.GetNodeSlot(i);
                    if (child == null)
                    {
                        continue;
                    }
                    if (!child.IsList)
                    {
                        var n = new Node();
                        node.Children.Add(n);
                        Visit(child, n); // recursive
                    }
                    else
                    {
                        Visit(child, node); // recursive
                    }
                }
            }
        }

        private static string GetNodeContent(SyntaxNode node)
        {
            if (node is SyntaxToken token)
            {
                return GetTokenContent(token);
            }

            var builder = new StringBuilder()
                .Append($"{nameof(SyntaxKind)}.{node.Kind}")
                .Append(" - ")
                .Append($"[{node.Position}..{node.EndPosition})::{node.FullWidth}")
                .Append(" - ")
                .Append($"[{node.ToFullString()}]");

            var annotation = node.GetAnnotations().FirstOrDefault(a => a.Kind == SyntaxConstants.SpanContextKind);
            if (annotation != null && annotation.Data is SpanContext context)
            {
                _ = builder.Append(" - ")
                    .Append($"Gen<{context.ChunkGenerator}>")
                    .Append(" - ")
                    .Append(context.EditHandler);
            }

            return builder.ToString();
        }

        private static string GetTokenContent(SyntaxToken token)
        {
            var content = token.IsMissing ? "<Missing>" : token.Content;
            var diagnostics = token.GetDiagnostics();
            var tokenString = $"{nameof(SyntaxKind)}.{token.Kind};[{content}];{string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Id + diagnostic.Span))}";
            return tokenString;
        }

        private static string Normalize(string content)
        {
            var result = content.Replace("\r\n", "\n");
            return result.Replace("\n", "LF");
        }
    }

    internal class Node
    {
        public Node()
        {
        }

        public Node(string content, int start, int length)
        {
            Content = content;
            Start = start;
            Length = length;
        }

        public string Content { get; set; }

        public int Start { get; set; }

        public int Length { get; set; }

        public IList<Node> Children { get; set; } = new List<Node>();
    }
}
