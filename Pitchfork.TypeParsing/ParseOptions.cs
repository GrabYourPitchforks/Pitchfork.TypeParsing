using System;
using System.Diagnostics;

namespace Pitchfork.TypeParsing
{
    public sealed class ParseOptions : ICloneable
    {
        private _ByValDetails _details;

        public ParseOptions()
            : this(toCopy: null)
        {
        }

        public ParseOptions(ParseOptions? toCopy)
        {
            _details = (toCopy ?? GlobalDefaults)._details;
        }

        // no-op ctor
        private ParseOptions(bool virgin)
        {
            Debug.Assert(virgin, "Non-virgin instances should use a different ctor.");
        }

        public static ParseOptions GlobalDefaults { get; } = new ParseOptions(virgin: true)
        {
            AllowNonAsciiIdentifiers = false,
            MaxRecursiveDepth = 10
        };

        internal static ParseOptions FromExistingIdentifier { get; } = new ParseOptions(virgin: true)
        {
            AllowNonAsciiIdentifiers = true
        };

        public bool AllowNonAsciiIdentifiers
        {
            get => _details.AllowNonAsciiIdentifiers;
            set => _details.AllowNonAsciiIdentifiers = value;
        }

        public int MaxRecursiveDepth
        {
            get => _details.MaxRecursiveDepth;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(paramName: nameof(value));
                }
                _details.MaxRecursiveDepth = value;
            }
        }

        public static ParseOptions Clone(ParseOptions? original) => new ParseOptions(original);

        object ICloneable.Clone() => Clone(this);

        private struct _ByValDetails
        {
            internal bool AllowNonAsciiIdentifiers;
            internal int MaxRecursiveDepth;
        }
    }
}
