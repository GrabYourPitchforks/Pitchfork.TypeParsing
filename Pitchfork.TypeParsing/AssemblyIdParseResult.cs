using System;

namespace Pitchfork.TypeParsing
{
    internal struct AssemblyIdParseResult
    {
        private readonly ParseOptions _options;

        internal AssemblyIdParseResult(ParseOptions options)
        {
            this = default;
            _options = options;
        }

        private string? _name;
        public string Name
        {
            readonly get => _name!;
            set
            {
                IdentifierRestrictor.ThrowIfDisallowedAssemblyName(value, _options);
                _name = value;
            }
        }

        public Version? Version { readonly get; set; }

        private string? _culture;
        public string Culture
        {
            readonly get => string.IsNullOrEmpty(_culture) ? "neutral" : _culture!;
            set
            {
                if (string.IsNullOrEmpty(value) || value == "neutral")
                {
                    value = "neutral"; // normalize all representations to "neutral"
                }
                else
                {
                    // throws if not a legal culture name
                    value = CultureUtil.GetPredefinedCultureInfo(value).Name;
                }

                _culture = value;
            }
        }

        public PublicKeyToken? PublicKeyToken { readonly get; set; }
    }
}
