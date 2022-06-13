using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Pitchfork.TypeParsing.Tests
{
    public class WellKnownPublicKeyTokenTests
    {
        private static readonly Dictionary<string, string> _expectedKnownTokens = new()
        {
            ["ECMA"] = "b77a5c561934e089",
            ["Microsoft"] = "b03f5f7f11d50a3a",
            ["MicrosoftAspNetCore"] = "adb9793829ddae60",
            ["MicrosoftShared"] = "31bf3856ad364e35",
            ["SilverlightPlatform"] = "7cec85d7bea7798e",
        };

        [Fact]
        public void IterateKnownTokens_MatchedExpectedList()
        {
            var remainingTokens = new Dictionary<string, string>(_expectedKnownTokens); // clone

            MemberInfo[] allMembers = typeof(WellKnownPublicKeyTokens).GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (MemberInfo mi in allMembers)
            {
                switch (mi.MemberType)
                {
                    case MemberTypes.Property:
                        PropertyInfo pi = (PropertyInfo)mi;
                        if (pi.GetMethod is null || pi.SetMethod is not null || pi.PropertyType != typeof(PublicKeyToken))
                        {
                            throw new InvalidOperationException($"Expected property {pi} to be readonly property of type PublicKeyToken.");
                        }
                        remainingTokens.TryGetValue(pi.Name, out var expectedPktTokenString);
                        if (expectedPktTokenString is null)
                        {
                            throw new InvalidOperationException($"Test harness doesn't have an expected value for {pi}. Please update test harness.");
                        }
                        var candidatePkt = (PublicKeyToken)pi.GetValue(null);
                        if (candidatePkt is null)
                        {
                            throw new InvalidOperationException($"Property {pi} returned a non-null value.");
                        }
                        var calledAgainPkt = (PublicKeyToken)pi.GetValue(null);
                        if (!ReferenceEquals(candidatePkt, calledAgainPkt))
                        {
                            throw new InvalidOperationException($"Property {pi} returned a non-singleton PKT.");
                        }
                        if (candidatePkt.TokenString != expectedPktTokenString)
                        {
                            throw new InvalidOperationException($"Property {pi} returned public key token {candidatePkt.TokenString}, expected token string {expectedPktTokenString}.");
                        }
                        remainingTokens.Remove(pi.Name);
                        continue;

                    case MemberTypes.Method:
                        MethodInfo mmi = (MethodInfo)mi;
                        if (mmi.IsSpecialName)
                        {
                            continue;
                        }
                        goto default;

                    default:
                        throw new InvalidOperationException($"Unexpected member {mi} seen.");
                }
            }

            if (remainingTokens.Count > 0)
            {
                throw new InvalidOperationException(
                    "Expected to see missing well-known public key tokens: "
                    + string.Join(", ", remainingTokens.Select(entry => $"{entry.Key} ({entry.Value})")));
            }
        }
    }
}
