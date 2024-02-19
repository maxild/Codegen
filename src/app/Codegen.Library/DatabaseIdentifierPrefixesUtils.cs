using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codegen.Library;

// NOTE: Prefixes are case-sensitive
public static class DatabaseIdentifierPrefixesUtils
{
    public static IReadOnlyDictionary<string, int>? Parse(string? s)
    {
        if (s is null) return null;

        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);

        int @base = 1;
        foreach (string segment in s.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            int pos = segment.IndexOf('=');
            if (pos >= 0)
            {
                string prefix = segment[..pos];
                int prefixBase = int.Parse(segment[(pos + 1)..]);
                result.Add(prefix, prefixBase);
            }
            else
            {
                result.Add(segment, @base);
                @base += 1000;
            }
        }

        return result;
    }

    public static string Format(IReadOnlyDictionary<string, int> databaseIdentifierPrefixes)
    {
        StringBuilder sb = new();
        bool notFirst = false;
        foreach (KeyValuePair<string, int> kvp in databaseIdentifierPrefixes.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (notFirst) sb.Append('|');
            sb.Append(kvp.Key).Append('=').Append(kvp.Value);
            notFirst = true;
        }
        return sb.ToString();
    }
}
