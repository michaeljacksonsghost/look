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
    public void DomainWithPortGetsHttpsScheme()
    {
        var result = AddressParser.Parse("example.org:8443/path");
        Assert.Equal("https", result.Scheme);
        Assert.Equal("example.org", result.Host);
        Assert.Equal(8443, result.Port);
    }

    [Fact]
    public void TextWithSpacesBecomesSearchQuery()
    {
        var result = AddressParser.Parse("quiet windows browser");
        Assert.Equal("quiet windows browser", Uri.UnescapeDataString(
            result.Query.TrimStart('?').Split('=', 2)[1]));
        Assert.Equal("www.google.com", result.Host);
    }

    [Theory]
    [InlineData("file:///C:/secret.txt")]
    [InlineData("ftp://example.org/file.zip")]
    [InlineData("javascript:alert(1)")]
    [InlineData("chrome://settings")]
    [InlineData("mailto:person@example.org")]
    public void NonHttpSchemesAreNotNavigatedDirectly(string input)
    {
        var result = AddressParser.Parse(input);
        Assert.Equal("www.google.com", result.Host);
        Assert.Contains("file", result.Query);
    }
}
