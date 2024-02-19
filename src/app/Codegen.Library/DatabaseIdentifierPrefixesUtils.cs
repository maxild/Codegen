using System;
using System.Collections.Generic;
using System.Text;

namespace Codegen.Library;

// FIXME: Tests
// NOTE: Prefixes are case-sensitive
public static class DatabaseIdentifierPrefixesUtils
{
    // Parse
    public static IReadOnlyDictionary<string, int>? Split(string? s)
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

    // Format
    public static string Join(IReadOnlyDictionary<string, int> databaseIdentifierPrefixes)
    {
        StringBuilder sb = new();
        bool notFirst = false;
        foreach (KeyValuePair<string, int> kvp in databaseIdentifierPrefixes)
        {
            if (notFirst) sb.Append('|');
            sb.Append(kvp.Key).Append('=').Append(kvp.Value);
            notFirst = true;
        }
        return sb.ToString();
    }
}
