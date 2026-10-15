using System.Text.RegularExpressions;
using QaaS.Framework.SDK.Session.MetaDataObjects;

namespace QaaS.Framework.Protocols.Protocols;

internal static partial class HttpRequestUriResolver
{
    internal static string JoinRoute(Uri baseAddress, string route)
    {
        if (
            route.StartsWith("//", StringComparison.Ordinal)
            || route.Contains('\\')
            || (!route.StartsWith('/') && Uri.TryCreate(route, UriKind.Absolute, out _))
        )
            throw new ArgumentException(
                "HTTP Route must be a path, not an authority or absolute URI. Use Http.Uri to change the destination.",
                nameof(route)
            );

        var suffixIndex = route.IndexOfAny(['?', '#']);
        var path = suffixIndex < 0 ? route : route[..suffixIndex];
        var suffix = suffixIndex < 0 ? string.Empty : route[suffixIndex..];
        if (!suffix.StartsWith('?'))
            suffix = baseAddress.Query + suffix;
        return baseAddress.GetLeftPart(UriPartial.Authority)
            + (
                path.Length == 0
                    ? baseAddress.AbsolutePath
                    : baseAddress.AbsolutePath.TrimEnd('/') + "/" + path.TrimStart('/')
            )
            + suffix;
    }

    internal static Uri Resolve(Uri baseAddress, string fallback, Http? metadata)
    {
        Uri uri;
        if (metadata?.Uri is { } overridden)
        {
            var directory = new UriBuilder(baseAddress)
            {
                Path = baseAddress.AbsolutePath.TrimEnd('/') + "/",
            };
            uri = overridden.IsAbsoluteUri ? overridden : new Uri(directory.Uri, overridden);
        }
        else
        {
            var destination = metadata?.Route is { } route
                ? JoinRoute(baseAddress, route)
                : fallback;
            var pathStart = destination.IndexOf(
                '/',
                destination.IndexOf("://", StringComparison.Ordinal) + 3
            );
            if (pathStart >= 0)
            {
                var suffixIndex = destination.IndexOfAny(['?', '#'], pathStart);
                if (suffixIndex < 0)
                    suffixIndex = destination.Length;
                var path = Placeholder()
                    .Replace(
                        destination[pathStart..suffixIndex],
                        match =>
                        {
                            var name = match.Groups[1].Value;
                            if (
                                metadata?.PathParameters is null
                                || !metadata.PathParameters.TryGetValue(name, out var value)
                                || value is null
                            )
                                throw new ArgumentException(
                                    $"Missing HTTP path parameter '{name}'.",
                                    nameof(metadata)
                                );
                            if (value is "." or "..")
                                throw new ArgumentException(
                                    $"HTTP path parameter '{name}' cannot be a dot segment.",
                                    nameof(metadata)
                                );
                            return Uri.EscapeDataString(value);
                        }
                    );
                destination = destination[..pathStart] + path + destination[suffixIndex..];
            }
            uri = new Uri(destination, UriKind.Absolute);
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException(
                "HTTP destination must use http or https.",
                nameof(metadata)
            );

        if (metadata?.QueryParameters is not { Count: > 0 } query)
            return uri;
        var builder = new UriBuilder(uri);
        var appended = string.Join(
            "&",
            query.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value ?? throw new ArgumentException($"HTTP query parameter '{pair.Key}' cannot be null.", nameof(metadata)))}"
            )
        );
        builder.Query = string.IsNullOrEmpty(builder.Query)
            ? appended
            : builder.Query + "&" + appended;
        return builder.Uri;
    }

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex Placeholder();
}
