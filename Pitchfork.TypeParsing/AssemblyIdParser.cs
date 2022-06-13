using System;
using System.Text;

namespace Pitchfork.TypeParsing
{
    internal static class AssemblyIdParser
    {
        public static AssemblyIdParseResult Parse(ReadOnlySpan<char> assemblyId, ParseOptions parseOptions)
        {
            AssemblyIdParseResult result = new(parseOptions);

            ReadOnlySpan<char> assemblyDisplayName = default;
            ReadOnlySpan<char> trailingData = default;

            // First, try to extract the name portion of the assembly.
            // Our delimiter is normally a comma, but we also have to deal
            // with single and double quotes and escape chars, since they
            // can change how we handle any commas we see. We'll deal with
            // trailing data (version, culture, PKT) later.

            int idxOfFirstDelimiter = assemblyId.IndexOfAny("\\,\'\"".AsSpan());
            if (idxOfFirstDelimiter < 0)
            {
                // Easy case: it's just the name with no version, culture, etc

                assemblyDisplayName = assemblyId;
            }
            else if (assemblyId[idxOfFirstDelimiter] != ',')
            {
                // The assembly name contains quotes or escape chars,
                // possibly also trailing data

                char observedDelimiterChar = assemblyId[idxOfFirstDelimiter];
                char terminationChar = ',';
                bool consumeTerminationChar = false;

                if (observedDelimiterChar != '\\')
                {
                    // Normally we'd search for a comma, but since we
                    // saw an open quote, we'll instead search for a
                    // matching close quote.

                    terminationChar = observedDelimiterChar;
                    idxOfFirstDelimiter++; // begin the search *after* this delimiter
                    consumeTerminationChar = true;
                }

                StringBuilder sb = new StringBuilder();
                sb.Append(assemblyId.Slice(0, idxOfFirstDelimiter));

                assemblyId = assemblyId.Slice(idxOfFirstDelimiter);
                int charsConsumed = UnescapeInto(assemblyId, terminationChar, consumeTerminationChar, sb);
                assemblyDisplayName = sb.ToString().AsSpan();

                trailingData = assemblyId.Slice(charsConsumed);
            }
            else
            {
                // Assembly name doesn't contain escape chars but does contain trailing data

                assemblyDisplayName = assemblyId.Slice(0, idxOfFirstDelimiter);
                trailingData = assemblyId.Slice(idxOfFirstDelimiter);
            }

            result.Name = assemblyDisplayName.TrimSpacesOnly().StripSurroundingQuotes().ToString();

            Token dummyToken = default; // unused
            Token versionToken = default;
            Token cultureToken = default;
            Token pktToken = default;

            // The length check prevents us from seeing "Hello," (a friendly name
            // with nothing after the comma) as valid. In this case, we'll avoid
            // the split here, and we'll let the loop below handle the error.

            trailingData = trailingData.TrimSpacesOnly();
            if (trailingData.Length >= 2 && trailingData[0] == ',')
            {
                trailingData = trailingData.Slice(1);
            }

            // Extract version, culture, pkt
            while (!trailingData.IsEmpty)
            {
                (var thisTokenPair, trailingData) = trailingData.SplitForbidEmptyTrailer(',');
                (var tokenName, var tokenValue) = thisTokenPair.SplitForbidEmptyTrailer('=');

                tokenName = tokenName.TrimSpacesOnly();
                ref Token tokenRef = ref dummyToken;

                if (tokenName.SequenceEqual("Version".AsSpan()))
                {
                    tokenRef = ref versionToken;
                }
                else if (tokenName.SequenceEqual("Culture".AsSpan()))
                {
                    tokenRef = ref cultureToken;
                }
                else if (tokenName.SequenceEqual("PublicKeyToken".AsSpan()))
                {
                    tokenRef = ref pktToken;
                }
                else
                {
                    ThrowHelper.ThrowArgumentException_UnrecognizedToken(tokenName);
                }

                // Did we already see this token?

                if (tokenRef.HasValue)
                {
                    ThrowHelper.ThrowArgumentException_DuplicateToken(tokenName);
                }

                tokenRef.HasValue = true;
                tokenRef.TokenValue = tokenValue.TrimSpacesOnly();
            }

            if (versionToken.HasValue)
            {
                result.Version = ParseInvariant.ParseVersion(versionToken.TokenValue);
            }

            if (cultureToken.HasValue)
            {
                result.Culture = cultureToken.TokenValue.ToString();
            }

            if (pktToken.HasValue && !pktToken.TokenValue.SequenceEqual("null".AsSpan()))
            {
                result.PublicKeyToken = new PublicKeyToken(pktToken.TokenValue);
            }

            return result;
        }

        private static int UnescapeInto(ReadOnlySpan<char> input, char eofChar, bool consumeTerminationChar, StringBuilder output)
        {
            int i = 0;
            for (; i < input.Length; i++)
            {
                char ch = input[i];
                if (ch == '\\')
                {
                    int nextIdx = i + 1;
                    if ((uint)nextIdx < (uint)input.Length)
                    {
                        char nextChar = input[nextIdx];
                        if (nextChar is ',' or '=')
                        {
                            ch = nextChar; // skip over the slash and write the unescaped char to the output
                            i = nextIdx;
                        }
                    }
                }
                else if (ch == eofChar)
                {
                    if (consumeTerminationChar)
                    {
                        output.Append(ch);
                        i++;
                    }
                    break;
                }

                output.Append(ch);
            }

            return i; // this is how many chars we processed before returning
        }

        private ref struct Token
        {
            internal bool HasValue;
            internal ReadOnlySpan<char> TokenValue;
        }
    }
}
