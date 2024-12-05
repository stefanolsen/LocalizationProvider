// Copyright (c) Valdis Iljuconoks. All rights reserved.
// Licensed under Apache-2.0. See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using DbLocalizationProvider.Abstractions;
using DbLocalizationProvider.Internal;
using DbLocalizationProvider.Queries;
using DbLocalizationProvider.Sync;
using Microsoft.Extensions.Options;

namespace DbLocalizationProvider;

/// <summary>
/// Main class to use when resource translation is needed.
/// </summary>
public class LocalizationProvider : ILocalizationProvider
{
    private readonly ExpressionHelper _expressionHelper;
    private readonly FallbackLanguagesCollection _fallbackCollection;
    private readonly ResourceKeyBuilder _keyBuilder;
    internal readonly IQueryExecutor _queryExecutor;
    private readonly ScanState _scanState;
    private readonly ReflectionConverter _reflectionConverter;

    /// <summary>
    /// Creates new localization provider with all the required settings and services injected.
    /// </summary>
    /// <param name="keyBuilder">
    /// Key builder (with help of <paramref name="expressionHelper" /> this dependency will help to translate from lambda
    /// expression to resource key as string).
    /// </param>
    /// <param name="expressionHelper">Can walk lambda expressions and return string representation of the expression.</param>
    /// <param name="context">ConfigurationContext (fallback language collection will be used from this).</param>
    /// <param name="queryExecutor">Small utility robot to help with queries.</param>
    /// <param name="scanState">Scanner state.</param>
    public LocalizationProvider(
        ResourceKeyBuilder keyBuilder,
        ExpressionHelper expressionHelper,
        IOptions<ConfigurationContext> context,
        IQueryExecutor queryExecutor,
        ScanState scanState)
    {
        _keyBuilder = keyBuilder;
        _expressionHelper = expressionHelper;
        _fallbackCollection = context.Value._fallbackCollection;
        _queryExecutor = queryExecutor;
        _scanState = scanState;

        _reflectionConverter = new ReflectionConverter(_queryExecutor, _scanState, _keyBuilder);
    }

    /// <summary>
    /// Gets translation for the resource with specific key.
    /// </summary>
    /// <param name="resourceKey">Key of the resource to look translation for.</param>
    /// <returns>Translation for the resource with specific key.</returns>
    /// <remarks><see cref="CultureInfo.CurrentUICulture" /> is used as language.</remarks>
    public virtual string GetString(string resourceKey)
    {
        return GetString(resourceKey, _queryExecutor.Execute(new GetCurrentUICulture.Query()));
    }

    /// <summary>
    /// Gets translation for the resource with specific key.
    /// </summary>
    /// <param name="resourceKey">Key of the resource to look translation for.</param>
    /// <param name="culture">
    /// If you want to get translation for other language as <see cref="CultureInfo.CurrentUICulture" />,
    /// then specify that language here.
    /// </param>
    /// <returns>Translation for the resource with specific key.</returns>
    public virtual string GetString(string resourceKey, CultureInfo culture)
    {
        return GetStringByCulture(resourceKey, culture);
    }

    /// <summary>
    /// Gets translation for the resource (reference to the resource is specified as lambda expression).
    /// </summary>
    /// <param name="resource">Lambda expression for the resource.</param>
    /// <param name="formatArguments">
    /// If you have placeholders in translation to replace to - use this argument to specify those.
    /// </param>
    /// <returns>Translation for the resource with specific key.</returns>
    /// <remarks><see cref="CultureInfo.CurrentUICulture" /> is used as language.</remarks>
    public virtual string GetString(Expression<Func<object>> resource, params object[] formatArguments)
    {
        return GetStringByCulture(resource, _queryExecutor.Execute(new GetCurrentUICulture.Query()), formatArguments);
    }

    /// <summary>
    /// Gets translation for the resource (reference to the resource is specified as lambda expression).
    /// </summary>
    /// <param name="resource">Lambda expression for the resource.</param>
    /// <param name="culture">
    /// If you want to get translation for other language as <see cref="CultureInfo.CurrentUICulture" />,
    /// then specific that language here.
    /// </param>
    /// <param name="formatArguments">
    /// If you have placeholders in translation to replace to - use this argument to specify those.
    /// </param>
    /// <returns>Translation for the resource with specific key.</returns>
    public virtual string GetString(Expression<Func<object>> resource, CultureInfo culture, params object[] formatArguments)
    {
        return GetStringByCulture(resource, culture, formatArguments);
    }

    /// <summary>
    /// Gets key and translations for the specified culture.
    /// </summary>
    /// <param name="culture">
    /// If you want to get translation for other language as <see cref="CultureInfo.CurrentUICulture" />,
    /// then specify that language here.
    /// </param>
    /// <returns>Translation for the resource with specific key.</returns>
    public virtual IDictionary<string, string> GetStringsByCulture(CultureInfo culture)
    {
        if (culture == null)
        {
            throw new ArgumentNullException(nameof(culture));
        }

        var localizationResources = _queryExecutor.Execute(new GetAllResources.Query());
        var translationDictionary =
            localizationResources.ToDictionary(res => res.ResourceKey, res => res.Translations.ByLanguage(culture, true));

        return translationDictionary;
    }

    /// <summary>
    /// Gets translation for the resource (reference to the resource is specified as lambda expression).
    /// </summary>
    /// <param name="resource">Lambda expression for the resource.</param>
    /// <param name="culture">
    /// If you want to get translation for other language as <see cref="CultureInfo.CurrentUICulture" />, then specific
    /// that language here.
    /// </param>
    /// <param name="formatArguments">
    /// If you have placeholders in translation to replace to - use this argument to specify those.
    /// </param>
    /// <returns>Translation for the resource with specific key.</returns>
    public virtual string GetStringByCulture(
        Expression<Func<object>> resource,
        CultureInfo culture,
        params object[] formatArguments)
    {
        if (resource == null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        var resourceKey = _expressionHelper.GetFullMemberName(resource);

        return GetStringByCulture(resourceKey, culture, formatArguments);
    }

    /// <summary>
    /// Gets translation for the resource with specific key.
    /// </summary>
    /// <param name="resourceKey">Key of the resource to look translation for.</param>
    /// <param name="culture">
    /// If you want to get translation for other language as <see cref="CultureInfo.CurrentUICulture" />,
    /// then specify that language here.
    /// </param>
    /// <param name="formatArguments">
    /// If you have placeholders in translation to replace to - use this argument to specify
    /// those.
    /// </param>
    /// <returns>Translation for the resource with specific key.</returns>
    public virtual string GetStringByCulture(string resourceKey, CultureInfo culture, params object[] formatArguments)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            throw new ArgumentNullException(nameof(resourceKey));
        }

        if (culture == null)
        {
            throw new ArgumentNullException(nameof(culture));
        }

        var resourceValue = _queryExecutor.Execute(new GetTranslation.Query(resourceKey, culture));
        if (resourceValue == null)
        {
            return null;
        }

        try
        {
            return Format(resourceValue, formatArguments);
        }
        catch (Exception)
        {
            // TODO: log
        }

        return resourceValue;
    }

    /// <summary>
    /// Give a type to this method and it will return instance of the type but translated
    /// </summary>
    /// <typeparam name="T">Type of the target class you want to translate</typeparam>
    /// <returns>Translated class based on <see cref="CultureInfo.CurrentUICulture" /> language</returns>
    public T Translate<T>()
    {
        return Translate<T>(_queryExecutor.Execute(new GetCurrentUICulture.Query()));
    }

    /// <summary>
    /// Give a type to this method, and it will return instance of the type but translated
    /// </summary>
    /// <typeparam name="T">Type of the target class you want to translate</typeparam>
    /// <param name="language">Language to use during translation</param>
    /// <returns>Translated class</returns>
    public T Translate<T>(CultureInfo language)
    {
        return _reflectionConverter.Convert<T>(language.Name, _fallbackCollection);
    }

    /// <summary>
    /// Translates the specified enum with some formatting arguments (if needed).
    /// </summary>
    /// <param name="target">The enum to translate.</param>
    /// <param name="formatArguments">The format arguments.</param>
    /// <returns>Translated enum values</returns>
    public string Translate(Enum target, params object[] formatArguments)
    {
        return TranslateByCulture(target, _queryExecutor.Execute(new GetCurrentUICulture.Query()), formatArguments);
    }

    /// <summary>
    /// Translates the specified enum with some formatting arguments (if needed).
    /// </summary>
    /// <param name="target">The enum to translate.</param>
    /// <param name="culture">The culture.</param>
    /// <param name="formatArguments">The format arguments.</param>
    /// <returns>Translated enum values</returns>
    /// <exception cref="ArgumentNullException">
    /// target
    /// or
    /// culture
    /// </exception>
    public string TranslateByCulture(Enum target, CultureInfo culture, params object[] formatArguments)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (culture == null)
        {
            throw new ArgumentNullException(nameof(culture));
        }

        var resourceKey = _keyBuilder.BuildResourceKey(target.GetType(), target.ToString());

        return GetStringByCulture(resourceKey, culture, formatArguments);
    }

    /// <inheritdoc />
    public string GetStringWithInvariantFallback(Expression<Func<object>> resource, params object[] formatArguments)
    {
        if (resource == null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        var resourceKey = _expressionHelper.GetFullMemberName(resource);
        var culture = _queryExecutor.Execute(new GetCurrentUICulture.Query());
        var resourceValue = _queryExecutor.Execute(new GetTranslation.Query(resourceKey, culture) { FallbackToInvariant = true });

        return Format(resourceValue, formatArguments);
    }

    /// <summary>
    /// Gets translation for the resource (reference to the resource is specified as lambda expression).
    /// </summary>
    /// <param name="resource">Lambda expression for the resource.</param>
    /// <param name="attribute">
    /// Type of the custom attribute (registered in
    /// <see cref="ConfigurationContext.CustomAttributes" /> collection).
    /// </param>
    /// <param name="formatArguments">
    /// If you have placeholders in translation to replace to - use this argument to specify
    /// those.
    /// </param>
    /// <returns>Translation for the resource with specific key.</returns>
    /// <remarks><see cref="CultureInfo.CurrentUICulture" /> is used as language.</remarks>
    public virtual string GetString(Expression<Func<object>> resource, Type attribute, params object[] formatArguments)
    {
        return GetStringByCulture(resource, attribute, _queryExecutor.Execute(new GetCurrentUICulture.Query()), formatArguments);
    }

    /// <summary>
    /// Gets translation for the resource (reference to the resource is specified as lambda expression).
    /// </summary>
    /// <param name="resource">Lambda expression for the resource.</param>
    /// <param name="attribute">
    /// Type of the custom attribute (registered in
    /// <see cref="ConfigurationContext.CustomAttributes" /> collection).
    /// </param>
    /// <param name="culture">
    /// If you want to get translation for other language as <see cref="CultureInfo.CurrentUICulture" />,
    /// then specific that language here.
    /// </param>
    /// <param name="formatArguments">
    /// If you have placeholders in translation to replace to - use this argument to specify
    /// those.
    /// </param>
    /// <returns>Translation for the resource with specific key in language specified  in <paramref name="culture" />.</returns>
    public virtual string GetStringByCulture(
        Expression<Func<object>> resource,
        Type attribute,
        CultureInfo culture,
        params object[] formatArguments)
    {
        if (resource == null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        var resourceKey = _expressionHelper.GetFullMemberName(resource);
        resourceKey = _keyBuilder.BuildResourceKey(resourceKey, attribute);

        return GetStringByCulture(resourceKey, culture, formatArguments);
    }

    /// <summary>
    /// Converts a localized resource dictionary to a translated dictionary based on the specified type.
    /// </summary>
    /// <typeparam name="T">The type to retrieve localized resources for.</typeparam>
    /// <returns>A dictionary containing the localized resources translated to the current culture.</returns>
    /// <exception cref="ArgumentException">Thrown when the object does not have a LocalizedResourceAttribute.</exception>
    public IDictionary<string, string> ToDictionary<T>()
    {
        return ToDictionary(typeof(T));
    }

    /// <summary>
    /// Converts a localized resource dictionary to a translated dictionary based on the specified type.
    /// </summary>
    /// <typeparam name="T">The type to retrieve localized resources for.</typeparam>
    /// <param name="culture">Culture to get translations in.</param>
    /// <returns>A dictionary containing the localized resources translated to the current culture.</returns>
    /// <exception cref="ArgumentException">Thrown when the object does not have a LocalizedResourceAttribute.</exception>
    public IDictionary<string, string> ToDictionary<T>(CultureInfo culture)
    {
        return ToDictionary(typeof(T), culture);
    }

    /// <summary>
    /// Converts a localized resource dictionary to a translated dictionary based on the specified type.
    /// </summary>
    /// <param name="type">The type to retrieve localized resources for.</param>
    /// <returns>A dictionary containing the localized resources translated to the current culture.</returns>
    /// <exception cref="ArgumentException">Thrown when the object does not have a LocalizedResourceAttribute.</exception>
    public IDictionary<string, string> ToDictionary(Type type)
    {
        return ToDictionary(type, _queryExecutor.Execute(new GetCurrentUICulture.Query()));
    }

    /// <summary>
    /// Converts a localized resource dictionary to a translated dictionary based on the specified type.
    /// </summary>
    /// <param name="type">The type to retrieve localized resources for.</param>
    /// <param name = "culture" > Culture to get translations in.</param>
    /// <returns>A dictionary containing the localized resources translated to the current culture.</returns>
    /// <exception cref="ArgumentException">Thrown when the object does not have a LocalizedResourceAttribute.</exception>
    public IDictionary<string, string> ToDictionary(Type type, CultureInfo culture)
    {
        _ = Attribute.GetCustomAttribute(type, typeof(LocalizedResourceAttribute)) ?? throw new ArgumentException($"Object needs to have a {nameof(LocalizedResourceAttribute)} to be converted");

        return GetLocalizedResourceTranslations(type, culture)
            .ToDictionary(k => k.Key, v => v.Value);
    }

    internal IEnumerable<KeyValuePair<string, string>> GetLocalizedResourceTranslations(Type type, CultureInfo culture)
    {
        foreach (var property in type.GetProperties())
        {
            if (property.IsHidden())
            {
                continue;
            }

            var resourceKey = _keyBuilder.BuildResourceKey(type, property.Name);

            yield return new(property.Name, GetString(resourceKey, culture));
        }
    }

    internal static string Format(string message, params object[] formatArguments)
    {
        if (formatArguments == null || !formatArguments.Any())
        {
            return message;
        }

        // check if first element is not scalar - format with named placeholders
        var first = formatArguments.First();

        return !first.GetType().IsSimpleType()
            ? FormatWithAnonymousObject(message, first)
            : string.Format(message, formatArguments);
    }

    private static string FormatWithAnonymousObject(string message, object model)
    {
        var type = model.GetType();
        if (type == typeof(string))
        {
            return string.Format(message, model);
        }

        var placeHolders = Regex.Matches(message, "{.*?}").Select(m => m.Value).ToList();

        if (!placeHolders.Any())
        {
            return message;
        }

        var placeholderMap = new Dictionary<string, object>();
        var properties = type.GetProperties();

        foreach (var placeHolder in placeHolders)
        {
            var propertyInfo = properties.FirstOrDefault(p => p.Name == placeHolder.Trim('{', '}'));

            // property found - extract value and add to the map
            var val = propertyInfo?.GetValue(model);
            if (val != null && !placeholderMap.ContainsKey(placeHolder))
            {
                placeholderMap.Add(placeHolder, val);
            }
        }

        return placeholderMap.Aggregate(message, (current, pair) => current.Replace(pair.Key, pair.Value.ToString()));
    }
}
