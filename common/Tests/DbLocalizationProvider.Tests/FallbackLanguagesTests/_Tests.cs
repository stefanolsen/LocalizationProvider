using System.Collections.Generic;
using System.Globalization;
using DbLocalizationProvider.Abstractions;
using DbLocalizationProvider.Internal;
using DbLocalizationProvider.Queries;
using DbLocalizationProvider.Sync;
using Microsoft.Extensions.Options;
using Xunit;

namespace DbLocalizationProvider.Tests.FallbackLanguagesTests;

public class FallbackLanguagesTests
{
    private readonly LocalizationProvider _sut;

    public FallbackLanguagesTests()
    {
        var ctx = new ConfigurationContext();
        var wrapper = new OptionsWrapper<ConfigurationContext>(ctx);
        var keyBuilder = new ResourceKeyBuilder(new ScanState(), wrapper);

        // try "sv" -> "no" -> "en"
        ctx.FallbackLanguages
            .Try(CultureInfo.GetCultureInfo("sv"))
            .Then(CultureInfo.GetCultureInfo("no"))
            .Then(CultureInfo.GetCultureInfo("en"));

        // for rare cases - configure language specific fallback
        ctx.FallbackLanguages
            .When(CultureInfo.GetCultureInfo("fr-BE"))
            .Try(CultureInfo.GetCultureInfo("fr"))
            .Then(CultureInfo.GetCultureInfo("en"));

        ctx.TypeFactory.ForQuery<GetTranslation.Query>()
            .SetHandler(() => new FallbackLanguagesTestTranslationHandler(ctx._fallbackCollection));

        IQueryExecutor queryExecutor = new QueryExecutor(ctx.TypeFactory);

        _sut = new LocalizationProvider(keyBuilder,
                                        new ExpressionHelper(keyBuilder),
                                        new OptionsWrapper<ConfigurationContext>(ctx),
                                        queryExecutor,
                                        new ScanState());
    }

    [Fact]
    public void FallbackTranslationTests()
    {
        Assert.Equal("Some Swedish translation", _sut.GetString("Resource.With.Swedish.Translation", CultureInfo.GetCultureInfo("sv")));
        Assert.Equal("Some English translation", _sut.GetString("Resource.With.English.Translation", CultureInfo.GetCultureInfo("sv")));
        Assert.Equal("Some Norwegian translation", _sut.GetString("Resource.With.Norwegian.Translation", CultureInfo.GetCultureInfo("sv")));
        Assert.Equal("Some Norwegian translation",
                     _sut.GetString("Resource.With.Norwegian.And.English.Translation", CultureInfo.GetCultureInfo("sv")));
    }

    [Fact]
    public void Language_ShouldFollowLanguageBranchSpecs()
    {
        Assert.Equal("Some French translation",
                     _sut.GetString("Resource.With.FrenchFallback.Translation", CultureInfo.GetCultureInfo("fr-BE")));
        Assert.Equal("Some English translation",
                     _sut.GetString("Resource.InFrench.With.EnglishFallback.Translation", CultureInfo.GetCultureInfo("fr-BE")));
    }

    [Fact]
    public void GetStringByNorwegianRegion_ShouldReturnInNorwegian()
    {
        Assert.Equal("Some Latvian translation", _sut.GetString("Resource.With.Latvian.Translation", CultureInfo.GetCultureInfo("lv")));
        Assert.Equal("Some Latvian translation", _sut.GetString("Resource.With.Latvian.Translation", CultureInfo.GetCultureInfo("lv-LV")));
    }
}

public class FallbackLanguagesTestTranslationHandler(FallbackLanguagesCollection fallbackCollection)
    : IQueryHandler<GetTranslation.Query, string>
{
    private readonly Dictionary<string, LocalizationResource> _resources = new()
    {
        {
            "Resource.With.Swedish.Translation",
            new LocalizationResource("Resource.With.Swedish.Translation", false)
            {
                Translations = new LocalizationResourceTranslationCollection(false)
                {
                    new() { Language = "sv", Value = "Some Swedish translation" }
                }
            }
        },
        {
            "Resource.With.English.Translation",
            new LocalizationResource("Resource.With.English.Translation", false)
            {
                Translations = new LocalizationResourceTranslationCollection(false)
                {
                    new() { Language = "en", Value = "Some English translation" }
                }
            }
        },
        {
            "Resource.With.Norwegian.Translation",
            new LocalizationResource("Resource.With.Norwegian.Translation", false)
            {
                Translations = new LocalizationResourceTranslationCollection(false)
                {
                    new() { Language = "no", Value = "Some Norwegian translation" }
                }
            }
        },
        {
            "Resource.With.Latvian.Translation",
            new LocalizationResource("Resource.With.Latvian.Translation", false)
            {
                Translations = new LocalizationResourceTranslationCollection(false)
                {
                    new() { Language = "lv", Value = "Some Latvian translation" }
                }
            }
        },
        {
            "Resource.With.Norwegian.And.English.Translation",
            new LocalizationResource("Resource.With.Norwegian.And.English.Translation", false)
            {
                Translations = new LocalizationResourceTranslationCollection(false)
                {
                    new() { Language = "no", Value = "Some Norwegian translation" },
                    new() { Language = "en", Value = "Some English translation" }
                }
            }
        },
        {
            "Resource.With.FrenchFallback.Translation",
            new LocalizationResource("Resource.With.FrenchFallback.Translation", false)
            {
                Translations = new LocalizationResourceTranslationCollection(false)
                {
                    new() { Language = "fr", Value = "Some French translation" }
                }
            }
        },
        {
            "Resource.InFrench.With.EnglishFallback.Translation",
            new LocalizationResource("Resource.InFrench.With.EnglishFallback.Translation", false)
            {
                Translations = new LocalizationResourceTranslationCollection(false)
                {
                    new() { Language = "en", Value = "Some English translation" }
                }
            }
        }
    };

    public string Execute(GetTranslation.Query query)
    {
        return _resources[query.Key]
            .Translations.GetValueWithFallback(
                query.Language,
                fallbackCollection.GetFallbackLanguages(query.Language));
    }
}
