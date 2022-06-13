using System.Collections.Generic;

namespace Pitchfork.TypeParsing.Tests
{
    public static class CommonData
    {
        private static IEnumerable<string> GetDisallowedAssemblyIdentifierComponents()
        {
            yield return "\0"; // null and C0 controls
            yield return "\r";
            yield return "\n";
            yield return "\""; // no quotes
            yield return "\'";
            yield return "["; // generic confusion
            yield return "]";
            yield return ":"; // no protocol markers
            yield return "\\"; // no path separators
            yield return "/";
            yield return ":"; // more path disallowed chars
            yield return "*";
            yield return "?";
            yield return "\u007F"; // C1 control
            yield return "\u009F"; // C1 control
            yield return "\u200D"; // ZWJ (prohibited Format category)
            yield return "\u2028"; // LineSeparator category
            yield return "\u2029"; // ParagraphSeparator category
            yield return "\uE000"; // PrivateUse category
            yield return "\u3000"; // Separator category
            yield return "\uFFFF"; // permanently unassigned
            yield return "\uDC00\uD800"; // mismatched surrogates
        }

        public static IEnumerable<object[]> SampleDisallowedAssemblyIdentifiersAsWrappedStrings()
        {
            foreach (var disallowedComponent in GetDisallowedAssemblyIdentifierComponents())
            {
                yield return new object[] { new WrappedString("abc" + disallowedComponent + "xyz") };
            }
        }

        private static IEnumerable<string> GetDisallowedTypeNameComponents()
        {
            yield return "\0"; // null and C0 controls
            yield return "\r";
            yield return "\n";
            yield return "\""; // no quotes
            yield return "\'";
            yield return "["; // generic confusion
            yield return "]";
            yield return ":"; // no protocol markers
            yield return ";"; // no delimiters
            yield return "\\"; // no path separators
            yield return "/";
            yield return ":"; // more path disallowed chars
            yield return "*";
            yield return "?";
            yield return "\u007F"; // C1 control
            yield return "\u009F"; // C1 control
            yield return "\u200D"; // ZWJ (prohibited Format category)
            yield return "\u2028"; // LineSeparator category
            yield return "\u2029"; // ParagraphSeparator category
            yield return "\uE000"; // PrivateUse category
            yield return "\u3000"; // Separator category
            yield return "\uFFFF"; // permanently unassigned
            yield return "\uDC00\uD800"; // mismatched surrogates
        }

        public static IEnumerable<object[]> SampleDisallowedTypeNamesAsWrappedStrings()
        {
            foreach (var disallowedComponent in GetDisallowedAssemblyIdentifierComponents())
            {
                yield return new object[] { new WrappedString("abc" + disallowedComponent + "xyz") };
            }
        }
    }
}
