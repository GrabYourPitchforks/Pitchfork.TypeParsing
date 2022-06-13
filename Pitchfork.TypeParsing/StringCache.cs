using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Pitchfork.TypeParsing
{
    internal static class StringCache
    {
        [ThreadStatic]
        private static Dictionary<string, string>? _tlsCache;

        public static IDisposable CreateScope()
        {
            CacheScope scope = new CacheScope();
            _tlsCache = new();
            return scope;
        }

        [return: NotNullIfNotNull("value")]
        public static string? GetFromOrAddToCache(string? value)
        {
            if (value is not null)
            {
                var cache = _tlsCache;
                if (cache is not null)
                {
#if NET6_0_OR_GREATER
                    ref string? cachedStringRef = ref CollectionsMarshal.GetValueRefOrAddDefault(cache, value, out bool exists);
                    if (exists)
                    {
                        value = cachedStringRef;
                    }
                    else
                    {
                        cachedStringRef = value;
                    }
#else
                    cache.TryGetValue(value, out string? cachedString);
                    if (cachedString is not null)
                    {
                        value = cachedString;
                    }
                    else
                    {
                        cache[value] = value;
                    }
#endif
                }
            }

            return value;
        }

        private sealed class CacheScope : IDisposable
        {
            public void Dispose()
            {
                _tlsCache = null;
            }
        }
    }
}
