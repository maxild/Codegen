using System.Collections.Generic;
using Codegen.Library;

namespace Codegen.Tests;

public class DatabaseIdentifierPrefixesUtilsTests
{
    [Fact]
    public void Parse()
    {
        IReadOnlyDictionary<string, int>? r = DatabaseIdentifierPrefixesUtils.Parse("HB|RT");
        r.ShouldNotBeNull();
        // NOTE: Keys are ordered
        r["HB"].ShouldBe(1);
        r["RT"].ShouldBe(1001);
    }

    [Fact]
    public void Format()
    {
        string s = DatabaseIdentifierPrefixesUtils.Format(new Dictionary<string, int> { { "HB", 1 }, { "RT", 1001 } });
        // NOTE: Keys are ordered
        s.ShouldBe("HB=1|RT=1001");
    }

    [Fact]
    public void Format_NonOrderedKeys()
    {
        string s = DatabaseIdentifierPrefixesUtils.Format(new Dictionary<string, int> { { "RT", 1001 }, { "HB", 1 } });
        // NOTE: Keys are ordered
        s.ShouldBe("HB=1|RT=1001");
    }
}
