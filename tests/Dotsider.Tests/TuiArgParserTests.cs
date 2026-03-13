using Dotsider.Infrastructure;

namespace Dotsider.Tests;

public class TuiArgParserTests
{
    [Fact]
    public void Parse_FileOnly_ReturnsDefaults()
    {
        var result = TuiArgParser.Parse(["app.dll"], "app.dll");

        Assert.Equal("app.dll", result.FilePath);
        Assert.Equal(0, result.InitialTab);
        Assert.Equal(4, result.MinStringLength);
    }

    [Fact]
    public void Parse_TabBeforeFile_ParsesTab()
    {
        var result = TuiArgParser.Parse(["--tab", "3", "app.dll"], "app.dll");

        Assert.Equal(2, result.InitialTab); // 1-indexed input, 0-indexed output
    }

    [Fact]
    public void Parse_TabAfterFile_ParsesTab()
    {
        var result = TuiArgParser.Parse(["app.dll", "--tab", "5"], "app.dll");

        Assert.Equal(4, result.InitialTab);
    }

    [Fact]
    public void Parse_ShortTabBeforeFile_ParsesTab()
    {
        var result = TuiArgParser.Parse(["-t", "2", "app.dll"], "app.dll");

        Assert.Equal(1, result.InitialTab);
    }

    [Fact]
    public void Parse_MinLenBeforeFile_ParsesMinLen()
    {
        var result = TuiArgParser.Parse(["--min-len", "10", "app.dll"], "app.dll");

        Assert.Equal(10, result.MinStringLength);
    }

    [Fact]
    public void Parse_ShortMinLenBeforeFile_ParsesMinLen()
    {
        var result = TuiArgParser.Parse(["-n", "8", "app.dll"], "app.dll");

        Assert.Equal(8, result.MinStringLength);
    }

    [Fact]
    public void Parse_AllOptionsBeforeFile_ParsesBoth()
    {
        var result = TuiArgParser.Parse(["-t", "4", "-n", "12", "app.dll"], "app.dll");

        Assert.Equal(3, result.InitialTab);
        Assert.Equal(12, result.MinStringLength);
    }

    [Fact]
    public void Parse_MixedOrdering_ParsesBoth()
    {
        var result = TuiArgParser.Parse(["--tab", "7", "app.dll", "--min-len", "6"], "app.dll");

        Assert.Equal(6, result.InitialTab);
        Assert.Equal(6, result.MinStringLength);
    }

    [Fact]
    public void Parse_TabOutOfRange_Clamps()
    {
        var high = TuiArgParser.Parse(["app.dll", "-t", "99"], "app.dll");
        var low = TuiArgParser.Parse(["app.dll", "-t", "0"], "app.dll");

        Assert.Equal(7, high.InitialTab); // clamped to max (index 7 = tab 8)
        Assert.Equal(0, low.InitialTab);  // clamped to min (index 0 = tab 1)
    }

    [Fact]
    public void Parse_InvalidTabValue_KeepsDefault()
    {
        var result = TuiArgParser.Parse(["app.dll", "--tab", "abc"], "app.dll");

        Assert.Equal(0, result.InitialTab);
    }

    [Fact]
    public void Parse_EscapeTimeout_Parses()
    {
        var result = TuiArgParser.Parse(["--escape-timeout", "200", "app.dll"], "app.dll");

        Assert.Equal(200, result.EscapeTimeoutMs);
    }

    [Fact]
    public void Parse_ShortEscapeTimeout_Parses()
    {
        var result = TuiArgParser.Parse(["-e", "75", "app.dll"], "app.dll");

        Assert.Equal(75, result.EscapeTimeoutMs);
    }

    [Fact]
    public void Parse_EscapeTimeoutBelowMin_Clamps()
    {
        var result = TuiArgParser.Parse(["-e", "5", "app.dll"], "app.dll");

        Assert.Equal(10, result.EscapeTimeoutMs);
    }

    [Fact]
    public void Parse_EscapeTimeoutInvalid_IgnoresNonNumeric()
    {
        var result = TuiArgParser.Parse(["--escape-timeout", "abc", "app.dll"], "app.dll");

        Assert.Equal(100, result.EscapeTimeoutMs);
    }

    [Fact]
    public void Parse_FileOnly_EscapeTimeoutDefault()
    {
        var result = TuiArgParser.Parse(["app.dll"], "app.dll");

        Assert.Equal(100, result.EscapeTimeoutMs);
    }
}
