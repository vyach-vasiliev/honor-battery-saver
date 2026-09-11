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

    [Theory]
    [InlineData("de-DE", "de", "Zuhause")]
    [InlineData("pt-BR", "pt", "Casa")]
    [InlineData("ko-KR", "ko", "집")]
    [InlineData("zh-CN", "zh-Hans", "居家")]
    [InlineData("zh-SG", "zh-Hans", "居家")]
    public void ResolvesAdditionalLanguageFamilies(string cultureName, string resolvedCulture, string expectedModeName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);

        Assert.Equal(resolvedCulture, Strings.ResolveCulture(culture.Name).Name);
        Assert.Equal(expectedModeName, Strings.Get("Mode_Home", culture));
    }

    [Fact]
    public void FallsBackToAmericanEnglishForUnsupportedLanguages()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");

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
    [InlineData(UiLanguage.German, "de", "Zuhause")]
    [InlineData(UiLanguage.Portuguese, "pt", "Casa")]
    [InlineData(UiLanguage.Korean, "ko", "집")]
    [InlineData(UiLanguage.Chinese, "zh-Hans", "居家")]
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

    [Theory]
    [InlineData("Language_Russian", "Русский (Russian)")]
    [InlineData("Language_English", "English")]
    [InlineData("Language_German", "Deutsch (German)")]
    [InlineData("Language_Portuguese", "Português (Portuguese)")]
    [InlineData("Language_Korean", "한국어 (Korean)")]
    [InlineData("Language_Chinese", "简体中文 (Simplified Chinese)")]
    public void LanguageChoicesUseNativeNamesWithEnglishDescriptions(string key, string expected)
    {
        foreach (var cultureName in new[] { "en-US", "ru", "de", "pt", "ko", "zh-Hans" })
        {
            Assert.Equal(expected, Strings.Get(key, CultureInfo.GetCultureInfo(cultureName)));
        }
    }

    [Fact]
    public void EverySupportedLocaleContainsTheSameResourceKeysAndPlaceholders()
    {
        var resourceDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "HonorBatterySaver.Core", "Resources");
        resourceDirectory = Path.GetFullPath(resourceDirectory);

        var neutral = ReadResources(Path.Combine(resourceDirectory, "Strings.resx"));
        foreach (var locale in new[] { "ru", "de", "pt", "ko", "zh-Hans" })
        {
            var localized = ReadResources(Path.Combine(resourceDirectory, $"Strings.{locale}.resx"));
            Assert.Equal(neutral.Keys.Order(), localized.Keys.Order());

            foreach (var key in neutral.Keys)
            {
                Assert.Equal(GetPlaceholders(neutral[key]), GetPlaceholders(localized[key]));
            }
        }
    }

    private static Dictionary<string, string> ReadResources(string path)
    {
        var document = System.Xml.Linq.XDocument.Load(path);
        return document.Root!.Elements("data").ToDictionary(
            element => (string)element.Attribute("name")!,
            element => (string?)element.Element("value") ?? string.Empty);
    }

    private static string[] GetPlaceholders(string value) =>
        System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+\}")
            .Select(match => match.Value)
            .Order()
            .ToArray();
}
