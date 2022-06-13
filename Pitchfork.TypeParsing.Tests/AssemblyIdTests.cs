using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Xunit;

namespace Pitchfork.TypeParsing.Tests
{
    public class AssemblyIdTests
    {
        [Theory]
        [InlineData(typeof(AssemblyId))]
        [InlineData(typeof(AssemblyIdTests))]
        [InlineData(typeof(object))]
        [InlineData(typeof(Assert))] // xunit
        public void CreateFromExisting_Success(Type type)
        {
            (string expectedName, Version expectedVersion, string expectedCulture, PublicKeyToken expectedPKT) = DecomposeAssemblyName(type.Assembly.GetName());

            // First try from Assembly

            var assemblyId = AssemblyId.CreateFromExisting(type.Assembly);
            Assert.Equal(expectedName, assemblyId.Name);
            Assert.Equal(expectedVersion, assemblyId.Version);
            Assert.Equal(expectedCulture, assemblyId.Culture);
            Assert.Equal(expectedPKT, assemblyId.PublicKeyToken);

            // Then try from AssemblyName

            assemblyId = AssemblyId.CreateFromExisting(type.Assembly.GetName());
            Assert.Equal(expectedName, assemblyId.Name);
            Assert.Equal(expectedVersion, assemblyId.Version);
            Assert.Equal(expectedCulture, assemblyId.Culture);
            Assert.Equal(expectedPKT, assemblyId.PublicKeyToken);
        }

        [Theory]
        [InlineData("\'Hello\'", "Hello")]
        [InlineData("\"Hello\"", "Hello")]
        [InlineData("Hello there", "Hello there")]
        [InlineData("\"Hello there\"", "Hello there")]
        [InlineData("Hello\\,there", "Hello,there")]
        [InlineData("'Hello,there'", "Hello,there")]
        [InlineData("\"Hello=there\"", "Hello=there")]
        [InlineData("Hello\\=\\,there", "Hello=,there")]
        [InlineData("\"Hello\\=,there\"", "Hello=,there")]
        [InlineData("\"Hello\\=,there\" ", "Hello=,there")] // same as above, but with trailing space
        public void Parse_WithQuotedAndEscapedNames_SuccessCases(string fullName, string expectedFriendlyName)
        {
            AssemblyId idWithoutVersion = AssemblyId.Parse(fullName);
            Assert.Equal(expectedFriendlyName, idWithoutVersion.Name);

            AssemblyId idWithVersion = AssemblyId.Parse(fullName + ", Version=1.2.3.4");
            Assert.Equal(expectedFriendlyName, idWithVersion.Name);
            Assert.Equal(new Version(1, 2, 3, 4), idWithVersion.Version);
        }

        [Theory]
        [InlineData("Hello,")] // trailing comma
        [InlineData("Hello, ")] // trailing comma
        [InlineData("Hello, Version=1.2\0.3.4")] // embedded null in Version (the Version class normally allows this)
        [InlineData("Hello, Version=1.2 .3.4")] // extra space in Version (the Version class normally allows this)
        [InlineData("Hello, Version=1.2.3.4, Version=1.2.3.4")] // duplicate Versions specified
        [InlineData("Hello, PublicKeyToken=bad")] // invalid PKT
        [InlineData("Hello, Culture=en-US_XYZ")] // invalid culture
        [InlineData("Hello, \r\nCulture=en-US")] // disallowed whitespace
        [InlineData("Hello, Version=1.2.3.4,")] // another trailing comma
        [InlineData("Hello, Version=1.2.3.4, =")] // malformed key=token pair
        [InlineData("Hello, Version=1.2.3.4, Architecture=x86")] // Architecture disallowed
        [InlineData("Hello, CodeBase=file://blah")] // CodeBase disallowed
        [InlineData("Hello, version=1.2.3,4")] // wrong case
        public void Parse_MalformedFullNames_Throws(string fullName)
        {
            Assert.Throws<ArgumentException>("assemblyFullName", () => AssemblyId.Parse(fullName));
        }

        [Theory]
        [MemberData(nameof(CommonData.SampleDisallowedAssemblyIdentifiersAsWrappedStrings), MemberType = typeof(CommonData))]
        public void CreateFromExisting_FromAssemblyName_WithInvalidName_Throws(WrappedString assemblyFriendlyName)
        {
            AssemblyName asmName = new AssemblyName()
            {
                Name = assemblyFriendlyName.Unwrap()
            };

            Assert.Throws<ArgumentException>("assemblyName", () => AssemblyId.CreateFromExisting(asmName));
        }

        [Fact]
        public void Parse_RespectsNonAsciiCharsAllowedSetting()
        {
            // With non-ASCII disallowed

            Assert.Throws<ArgumentException>("assemblyFullName", () => AssemblyId.Parse("Helloéthere", new ParseOptions() { AllowNonAsciiIdentifiers = false }));

            // With non-ASCII allowed

            AssemblyId assemblyId = AssemblyId.Parse("Helloéthere", new ParseOptions() { AllowNonAsciiIdentifiers = true });
            Assert.Equal("Helloéthere", assemblyId.Name);

            // But certain classes (like newlines) are still disallowed, even with non-ASCII allowed

            Assert.Throws<ArgumentException>("assemblyFullName", () => AssemblyId.Parse("Hello\u2028there", new ParseOptions() { AllowNonAsciiIdentifiers = false }));
        }

        public static IEnumerable<object[]> AllAssembliesInCurrentAppDomain()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().ContentType != AssemblyContentType.Default)
                {
                    continue; // we don't support WindowsRuntime assemblies
                }

                (var name, var version, var culture, var pkt) = DecomposeAssemblyName(asm.GetName());
                yield return new object[] { asm, name, version, culture, pkt };
            }
        }

        [Theory]
        [MemberData(nameof(AllAssembliesInCurrentAppDomain), DisableDiscoveryEnumeration = true)]
        public void CreateAndParse_FromAllAssembliesInAppDomain_Success(Assembly assembly, string expectedName, Version expectedVersion, string expectedCulture, PublicKeyToken expectedPkt)
        {
            // First from Assembly

            AssemblyId assemblyId = AssemblyId.CreateFromExisting(assembly);

            Assert.Equal(expectedName, assemblyId.Name);
            Assert.Equal(expectedVersion, assemblyId.Version);
            Assert.Equal(expectedCulture, assemblyId.Culture);
            Assert.Equal(expectedPkt, assemblyId.PublicKeyToken);

            // Then from AssemblyName

            assemblyId = AssemblyId.CreateFromExisting(assembly.GetName());

            Assert.Equal(expectedName, assemblyId.Name);
            Assert.Equal(expectedVersion, assemblyId.Version);
            Assert.Equal(expectedCulture, assemblyId.Culture);
            Assert.Equal(expectedPkt, assemblyId.PublicKeyToken);

            // Then from Assembly.FullName

            assemblyId = AssemblyId.Parse(assembly.FullName);

            Assert.Equal(expectedName, assemblyId.Name);
            Assert.Equal(expectedVersion, assemblyId.Version);
            Assert.Equal(expectedCulture, assemblyId.Culture);
            Assert.Equal(expectedPkt, assemblyId.PublicKeyToken);

            // And back to an AssemblyName

            AssemblyName roundTrippedAssemblyName = assemblyId.ToAssemblyName();

            Assert.Equal(expectedName, roundTrippedAssemblyName.Name);
            Assert.Equal(expectedVersion, roundTrippedAssemblyName.Version);
            if (expectedCulture == "neutral")
            {
                Assert.Equal(CultureInfo.InvariantCulture, roundTrippedAssemblyName.CultureInfo);
            }
            else
            {
                Assert.Equal(expectedCulture, roundTrippedAssemblyName.CultureName, StringComparer.OrdinalIgnoreCase);
            }
            if (expectedPkt is null)
            {
                Assert.Equal(new byte[0], roundTrippedAssemblyName.GetPublicKeyToken());
            }
            else
            {
                Assert.Equal(expectedPkt.TokenBytes.ToArray(), roundTrippedAssemblyName.GetPublicKeyToken());
            }
        }

        [Theory]
        [InlineData("Hello, Version = 1.2.3", "Hello", "1.2.3", "neutral", null)]
        [InlineData("Hello, Culture = neutral ,Version = 1.2.3", "Hello", "1.2.3", "neutral", null)]
        [InlineData("Hello, Culture = neutral ,Version = 1.2.3, PublicKeyToken = null ", "Hello", "1.2.3", "neutral", null)]
        [InlineData("Hello, Culture = EN-US ,Version = 1.2.3, PublicKeyToken = null ", "Hello", "1.2.3", "en-US", null)]
        [InlineData("Hello, PublicKeyToken = 0011223344556677 , Culture = EN-gb ,Version = 1.2.3.4", "Hello", "1.2.3.4", "en-GB", "0011223344556677")]
        [InlineData("Hello\\,world, PublicKeyToken = 0011223344556677 , Culture = EN-GB ,Version = 1.2.3.4", "Hello,world", "1.2.3.4", "en-GB", "0011223344556677")]
        [InlineData("Hello\\,world, PublicKeyToken = null, Culture = EN-GB ,Version = 1.2.3.4", "Hello,world", "1.2.3.4", "en-GB", null)]
        public void Parse_MoreComplexValidTestCases_Success(
            string assemblyFullName,
            string expectedFriendlyName,
            string expectedVersion,
            string expectedCulture,
            string expectedPkt)
        {
            AssemblyId assemblyId = AssemblyId.Parse(assemblyFullName);

            Assert.Equal(expectedFriendlyName, assemblyId.Name);
            if (expectedVersion is null)
            {
                Assert.Null(assemblyId.Version);
            }
            else
            {
                Assert.Equal(new Version(expectedVersion), assemblyId.Version);
            }
            Assert.Equal(expectedCulture, assemblyId.Culture);
            if (expectedPkt is null)
            {
                Assert.Null(assemblyId.PublicKeyToken);
            }
            else
            {
                Assert.Equal(new PublicKeyToken(expectedPkt), assemblyId.PublicKeyToken);
            }
        }

        [Theory]
        [InlineData(null, null, true)] // null == null
        [InlineData("Hello", null, false)] // null != not null
        [InlineData("Hello", "Hello", true)] // exact name match, no trailing data
        [InlineData("Hello", "hello", false)] // names are case-sensitive
        [InlineData("Hello", "Hello, Culture=neutral, PublicKeyToken=null", true)] // Culture=neutral and PKT=null are defaulted
        [InlineData("Hello, Culture=en-US", "Hello, Culture=en", false)] // culture mismatch
        [InlineData("Hello, Culture=en-US", "Hello, Culture=EN-US", true)] // culture names are case-insensitive
        [InlineData("Hello1", "Hello2", false)] // name mismatch
        [InlineData("Hello, PublicKeyToken=0011223344556677", "Hello, PublicKeyToken=0011223344556677, Culture=neutral", true)]
        [InlineData("Hello, PublicKeyToken=0011223344556677", "Hello, PublicKeyToken=0011223344556688, Culture=neutral", false)] // PKT mismatch
        public void EqualityTests(string assemblyFullName1, string assemblyFullName2, bool expectedResult)
        {
            AssemblyId asmId1 = (assemblyFullName1 is not null) ? AssemblyId.Parse(assemblyFullName1) : null;
            AssemblyId asmId2 = (assemblyFullName2 is not null) ? AssemblyId.Parse(assemblyFullName2) : null;

            if (asmId1 is not null)
            {
                Assert.Equal(expectedResult, asmId1.Equals(asmId2));
                Assert.Equal(expectedResult, asmId1.Equals((object)asmId2));
            }
            if (asmId2 is not null)
            {
                Assert.Equal(expectedResult, asmId2.Equals(asmId1));
                Assert.Equal(expectedResult, asmId2.Equals((object)asmId1));
            }

            Assert.Equal(expectedResult, asmId1 == asmId2);
            Assert.Equal(expectedResult, asmId2 == asmId1);

            Assert.NotEqual(expectedResult, asmId1 != asmId2);
            Assert.NotEqual(expectedResult, asmId2 != asmId1);

            if (expectedResult && asmId1 is not null && asmId2 is not null)
            {
                Assert.Equal(asmId1.GetHashCode(), asmId2.GetHashCode());
            }
        }

        [Theory]
        [InlineData("System", "System", true)] // exact match
        [InlineData("System", "system", false)] // case mismatch => fail
        [InlineData("System", "Syst", false)] // prefix mismatch
        [InlineData("System", "Something_Else", false)] // prefix mismatch
        [InlineData("Systemm", "System", false)] // hierarchy mismatch
        [InlineData("System.", "System", true)]
        [InlineData("System.", "", false)] // hierarchy mismatch
        [InlineData("Foo.Bar.Baz", "Foo.Bar", true)]
        [InlineData("Foo.Bar.Baz", "Foo.Bar.Baz.Quux", false)] // prefix mismatch
        [InlineData("Foo.Bar.Baz", "Foo.Bar.Baz.", false)] // prefix mismatch
        [InlineData("Foo.Bar.Baz.", "Foo.Bar.Baz.", true)] // exact match
        [InlineData("Foo.Bar.Baz.Quux", "Foo.Bar.Baz", true)]
        public void IsAssemblyUnder_Tests(string assemblyFullName, string requestedBaseName, bool expectedResult)
        {
            AssemblyId assemblyId = AssemblyId.Parse(assemblyFullName);
            Assert.Equal(expectedResult, assemblyId.IsAssemblyUnder(requestedBaseName));
        }

        [Theory]
        [InlineData("Hello", "Hello, Culture=neutral, PublicKeyToken=null")]
        [InlineData("\"Hello\", Version = 1.2.3", "Hello, Version=1.2.3, Culture=neutral, PublicKeyToken=null")]
        [InlineData("\"Hel,lo\", Version = 1.2.3", "Hel\\,lo, Version=1.2.3, Culture=neutral, PublicKeyToken=null")]
        [InlineData("'H=el lo' , Version = 1.2.3, PublicKeyToken =7766554433221100", "H\\=el lo, Version=1.2.3, Culture=neutral, PublicKeyToken=7766554433221100")]
        [InlineData("Some Assembly , Culture=FR-FR, Version = 1.2.3, PublicKeyToken =7766554433221100", "Some Assembly, Version=1.2.3, Culture=fr-FR, PublicKeyToken=7766554433221100")]
        public void ToString_Tests(string assemblyFullName, string expectedToString)
        {
            AssemblyId assemblyId = AssemblyId.Parse(assemblyFullName);
            Assert.Equal(expectedToString, assemblyId.ToString());
        }

        [Theory]
        [InlineData(null, "neutral")]
        [InlineData("", "neutral")]
        [InlineData("neutral", "neutral")]
        [InlineData("en-us", "en-US")]
        [InlineData("FR-FR", "fr-FR")]
        public void WithCultureTest_ByString_Success(string setCultureName, string expectedCultureName)
        {
            // Arrange

            AssemblyName assemblyName = new AssemblyName("Test")
            {
                Version = new Version(1, 2, 3, 4),
                CultureName = "de-de"
            };
            assemblyName.SetPublicKeyToken(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            AssemblyId originalAssemblyId = AssemblyId.CreateFromExisting(assemblyName);

            // Act & assert

            AssemblyId newAssemblyId = originalAssemblyId.WithCulture(setCultureName);
            Assert.Equal(originalAssemblyId.Name, newAssemblyId.Name);
            Assert.Equal(originalAssemblyId.Version, newAssemblyId.Version);
            Assert.Equal(expectedCultureName, newAssemblyId.Culture);
            Assert.Equal(originalAssemblyId.PublicKeyToken, newAssemblyId.PublicKeyToken);
        }

        [Fact]
        public void WithCultureTest_ByString_ReturnsSameInstanceIfNoChange()
        {
            // Test 1: neutral -> neutral

            AssemblyName assemblyName = new AssemblyName("Test")
            {
                Version = new Version(1, 2, 3, 4),
                CultureName = ""
            };
            assemblyName.SetPublicKeyToken(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            AssemblyId originalAssemblyId = AssemblyId.CreateFromExisting(assemblyName);
            AssemblyId newAssemblyId = originalAssemblyId.WithCulture("neutral");
            Assert.Same(originalAssemblyId, newAssemblyId);

            // Test 2: assigned culture -> same assigned culture

            AssemblyId withChangedCulture1 = originalAssemblyId.WithCulture("de-de");
            AssemblyId withChangedCulture2 = withChangedCulture1.WithCulture("de-de");
            Assert.Same(withChangedCulture1, withChangedCulture2);
        }

        [Theory]
        [InlineData("en-US-BAD")]
        [InlineData("invariant")]
        [InlineData("en-US\0")] // null terminator
        public void WithCultureTest_ByString_FailureCases(string cultureName)
        {
            AssemblyName assemblyName = new AssemblyName("Test")
            {
                Version = new Version(1, 2, 3, 4),
                CultureName = ""
            };
            assemblyName.SetPublicKeyToken(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            AssemblyId assemblyId = AssemblyId.CreateFromExisting(assemblyName);

            Assert.Throws<CultureNotFoundException>(() => assemblyId.WithCulture(cultureName));
        }

        [Theory]
        [InlineData(null, "neutral")]
        [InlineData("", "neutral")]
        [InlineData("en-us", "en-US")]
        [InlineData("FR-FR", "fr-FR")]
        public void WithCultureTest_ByCultureInfo_Success(string lookupCultureName, string expectedCultureName)
        {
            // Arrange

            AssemblyName assemblyName = new AssemblyName("Test")
            {
                Version = new Version(1, 2, 3, 4)
            };
            assemblyName.SetPublicKeyToken(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            AssemblyId originalAssemblyId = AssemblyId.CreateFromExisting(assemblyName);

            // Act & assert

            CultureInfo ci = (lookupCultureName is not null) ? CultureInfo.GetCultureInfo(lookupCultureName) : null;
            AssemblyId newAssemblyId = originalAssemblyId.WithCulture(ci);
            Assert.Equal(originalAssemblyId.Name, newAssemblyId.Name);
            Assert.Equal(originalAssemblyId.Version, newAssemblyId.Version);
            Assert.Equal(expectedCultureName, newAssemblyId.Culture);
            Assert.Equal(originalAssemblyId.PublicKeyToken, newAssemblyId.PublicKeyToken);
        }

        [Fact]
        public void WithVersionTests()
        {
            PublicKeyToken expectedPkt = new PublicKeyToken("0011223344556677");

            // Act 1 - Without a version, assert that original instance did not change

            AssemblyId asmId1 = AssemblyId.Parse("Hello, Culture=en-us, PublicKeyToken=0011223344556677");
            AssemblyId asmId2 = asmId1.WithVersion(new Version(1, 2));

            Assert.Equal("Hello", asmId1.Name);
            Assert.Null(asmId1.Version);
            Assert.Equal("en-US", asmId1.Culture);
            Assert.Equal(expectedPkt, asmId1.PublicKeyToken);

            Assert.Equal("Hello", asmId2.Name);
            Assert.Equal(new Version(1, 2), asmId2.Version);
            Assert.Equal("en-US", asmId2.Culture);
            Assert.Equal(expectedPkt, asmId2.PublicKeyToken);

            // Act 2 - change version to same value, should be no change

            AssemblyId asmId3 = asmId2.WithVersion(new Version(1, 2));

            Assert.Same(asmId2, asmId3);
            Assert.Equal("Hello", asmId3.Name);
            Assert.Equal(new Version(1, 2), asmId3.Version);
            Assert.Equal("en-US", asmId3.Culture);
            Assert.Equal(expectedPkt, asmId3.PublicKeyToken);

            // Act 3 - and back to null

            AssemblyId asmId4 = asmId3.WithVersion(null);

            Assert.Equal("Hello", asmId3.Name);
            Assert.Equal(new Version(1, 2), asmId3.Version);
            Assert.Equal("en-US", asmId3.Culture);
            Assert.Equal(expectedPkt, asmId3.PublicKeyToken);

            Assert.Equal("Hello", asmId4.Name);
            Assert.Null(asmId4.Version);
            Assert.Equal("en-US", asmId4.Culture);
            Assert.Equal(expectedPkt, asmId4.PublicKeyToken);
        }

        [Fact]
        public void WithPublicKeyTokenTests()
        {
            PublicKeyToken originalPkt = new PublicKeyToken("0011223344556677");
            PublicKeyToken newPkt = new PublicKeyToken("8899aabbccddeeff");

            // Act 1 - With some original PKT changed to new PKT
            // assert that original instance did not change

            AssemblyId asmId1 = AssemblyId.Parse("Hello, Version=1.2.3.4, Culture=en-us, PublicKeyToken=0011223344556677");
            AssemblyId asmId2 = asmId1.WithPublicKeyToken(newPkt);

            Assert.Equal("Hello", asmId1.Name);
            Assert.Equal(new Version(1, 2, 3, 4), asmId1.Version);
            Assert.Equal("en-US", asmId1.Culture);
            Assert.Equal(originalPkt, asmId1.PublicKeyToken);

            Assert.Equal("Hello", asmId2.Name);
            Assert.Equal(new Version(1, 2, 3, 4), asmId2.Version);
            Assert.Equal("en-US", asmId2.Culture);
            Assert.Equal(newPkt, asmId2.PublicKeyToken);

            // Act 2 - change PKT to same value, should be no change

            AssemblyId asmId3 = asmId2.WithPublicKeyToken(newPkt);

            Assert.Same(asmId2, asmId3);
            Assert.Equal("Hello", asmId3.Name);
            Assert.Equal(new Version(1, 2, 3, 4), asmId3.Version);
            Assert.Equal("en-US", asmId3.Culture);
            Assert.Equal(newPkt, asmId3.PublicKeyToken);

            // Act 3 - and finally to null

            AssemblyId asmId4 = asmId3.WithPublicKeyToken(null);

            Assert.Equal("Hello", asmId3.Name);
            Assert.Equal(new Version(1, 2, 3, 4), asmId3.Version);
            Assert.Equal("en-US", asmId3.Culture);
            Assert.Equal(newPkt, asmId3.PublicKeyToken);

            Assert.Equal("Hello", asmId4.Name);
            Assert.Equal(new Version(1, 2, 3, 4), asmId4.Version);
            Assert.Equal("en-US", asmId4.Culture);
            Assert.Null(asmId4.PublicKeyToken);
        }

        private static (string Name, Version Version, string Culture, PublicKeyToken PublicKeyToken) DecomposeAssemblyName(AssemblyName assemblyName)
        {
            string originalName = assemblyName.Name;
            Version originalVersion = assemblyName.Version;
            CultureInfo originalCI = assemblyName.CultureInfo;
            string normalizedCultureName = originalCI?.Name;
            if (string.IsNullOrEmpty(normalizedCultureName)) { normalizedCultureName = "neutral"; }
            normalizedCultureName = normalizedCultureName.ToLowerInvariant();
            ReadOnlySpan<byte> originalPKT = assemblyName.GetPublicKeyToken();
            PublicKeyToken normalizedPKT = (originalPKT.IsEmpty) ? null : new(originalPKT);
            return (originalName, originalVersion, normalizedCultureName, normalizedPKT);
        }
    }
}
