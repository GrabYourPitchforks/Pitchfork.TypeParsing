using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Pitchfork.TypeParsing.Resources;

namespace Pitchfork.TypeParsing
{
    [StackTraceHidden]
    internal static class ThrowHelper
    {
        [DoesNotReturn]
        public static void ThrowArgumentException_CannotParseAssemblyFullName(ReadOnlySpan<char> fullName, Exception innerException, string paramName)
        {
            throw new ArgumentException(
                paramName: paramName,
                message: string.Format(CultureInfo.CurrentCulture, SR.AssemblyId_CannotParseAssemblyFullName, fullName.ToString()),
                innerException: innerException);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_DuplicateToken(ReadOnlySpan<char> token)
        {
            throw new ArgumentException(
                message: string.Format(CultureInfo.CurrentCulture, SR.Common_DuplicateToken, token.ToString()));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_IdentifierMustNotBeNullOrEmpty()
        {
            throw new ArgumentException(SR.Common_IdentifierMustNotBeNullOrEmpty);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_IdentifierNotAllowedForbiddenCodePoint(string identifier, uint codePoint)
        {
            throw new ArgumentException(
                message: string.Format(CultureInfo.CurrentCulture, SR.Common_IdentifierNotAllowedForbiddenCodePoint, identifier, codePoint));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_UnrecognizedToken(ReadOnlySpan<char> token)
        {
            throw new ArgumentException(
                message: string.Format(CultureInfo.CurrentCulture, SR.Common_UnrecognizedToken, token.ToString()));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_TypeId_InvalidTypeString()
        {
            throw new ArgumentException(message: SR.TypeId_InvalidTypeString);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_TypeId_InvalidTypeString(string typeString)
        {
            throw new ArgumentException(
                message: string.Format(CultureInfo.CurrentCulture, SR.TypeId_InvalidTypeString_WithValue, typeString));
        }

        [DoesNotReturn]
        public static void ThrowValueArgumentOutOfRange_NeedNonNegNumException()
        {
            throw new ArgumentOutOfRangeException(
                paramName: "value",
                message: SR.Common_NonNegativeNumberNeeded);
        }
    }
}
