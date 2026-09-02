using System.Globalization;
using HonorBatterySaver.Core;

namespace HonorBatterySaver.Core.Tests;

public sealed class LocalizationTests
{
    [Theory]
    [InlineData("ru")]
    [InlineData("ru-RU")]
    public void ResolvesRussianLanguageFamily(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);

        Assert.Equal("ru", Strings.ResolveCulture(culture.Name).Name);
        Assert.NotEqual(Strings.Get("Mode_Home", CultureInfo.GetCultureInfo("en-US")),
            Strings.Get("Mode_Home", culture));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("en-GB")]
    public void ResolvesEnglishLanguageFamily(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);

        Assert.Equal("Home", Strings.Get("Mode_Home", culture));
    }

    [Fact]
    public void FallsBackToAmericanEnglishForUnsupportedLanguages()
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");

        Assert.Equal("Home", Strings.Get("Mode_Home", culture));
        Assert.Equal("en-US", Strings.ResolveCulture(culture.Name).Name);
    }

    [Fact]
    public void FormatsWithTheRequestedCulture()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.Equal("Networks found: 3.", Strings.Format(culture, "Wifi_CatalogCount", 3));
    }

    [Theory]
    [InlineData(UiLanguage.English, "en-US", "Home")]
    public void AppliesManualLanguage(UiLanguage language, string cultureName, string expectedModeName)
    {
        try
        {
            Strings.ApplyUiLanguage(language);

            Assert.Equal(cultureName, Strings.CurrentCulture.Name);
            Assert.Equal(expectedModeName, Strings.GetModeName(BatteryMode.Home));
        }
        finally
        {
            Strings.ApplyUiLanguage(UiLanguage.System);
        }
    }

    [Fact]
    public void FormatsEnglishNetworkNameUsingLanguageAppropriateQuotes()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.Equal("“Home Wi-Fi”", Strings.Format(culture, "Diagnostics_NetworkName", "Home Wi-Fi"));
    }
}
