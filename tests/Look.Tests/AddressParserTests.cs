using Look.Services;
using Xunit;

namespace Look.Tests;

public sealed class AddressParserTests
{
    [Fact]
    public void EmptyInputOpensSearchHome()
    {
        var result = AddressParser.Parse("  ");
        Assert.Equal("https://www.google.com/", result.AbsoluteUri);
    }

    [Fact]
    public void AbsoluteHttpsAddressIsPreserved()
    {
        var result = AddressParser.Parse("https://example.org/path?q=look");
        Assert.Equal("https://example.org/path?q=look", result.AbsoluteUri);
    }

    [Fact]
    public void BareDomainGetsHttpsScheme()
    {
        var result = AddressParser.Parse("example.org");
        Assert.Equal("https://example.org/", result.AbsoluteUri);
    }

    [Fact]
    public void TextWithSpacesBecomesSearchQuery()
    {
        var result = AddressParser.Parse("quiet windows browser");
        Assert.Equal("quiet windows browser", Uri.UnescapeDataString(
            result.Query.TrimStart('?').Split('=', 2)[1]));
        Assert.Equal("www.google.com", result.Host);
    }

    [Fact]
    public void NonHttpSchemesAreNotNavigatedDirectly()
    {
        var result = AddressParser.Parse("file:///C:/secret.txt");
        Assert.Equal("www.google.com", result.Host);
        Assert.Contains("file", result.Query);
    }
}
