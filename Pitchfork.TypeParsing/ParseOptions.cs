using System;
using System.Diagnostics;

namespace Pitchfork.TypeParsing
{
    public sealed class ParseOptions : ICloneable
    {
        public ParseOptions()
            : this(toCopy: null)
        {
        }

        public ParseOptions(ParseOptions? toCopy)
        {
            toCopy ??= GlobalDefaults;
            this.AllowNonAsciiIdentifiers = toCopy.AllowNonAsciiIdentifiers;
            this.MaxRecursiveDepth = toCopy.MaxRecursiveDepth;
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

        public bool AllowNonAsciiIdentifiers { get; set; }

        private int _maxRecursiveDepth;
        public int MaxRecursiveDepth
        {
            get => _maxRecursiveDepth;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(paramName: nameof(value));
                }
                _maxRecursiveDepth = value;
            }
        }

        public static ParseOptions Clone(ParseOptions? original) => new ParseOptions(original);

        object ICloneable.Clone() => Clone(this);
    }
}
