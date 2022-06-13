using System;
using System.Linq;
using Xunit;

namespace Pitchfork.TypeParsing.Tests
{
    public class PublicKeyTokenTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData(new byte[0])]
        [InlineData(new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 })]
        [InlineData(new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 })]
        public void Ctor_Bytes_NegativeTests(byte[] bytes)
        {
            Assert.Throws<ArgumentException>("tokenBytes", () => new PublicKeyToken(bytes));
            Assert.Throws<ArgumentException>("tokenBytes", () => new PublicKeyToken(bytes.AsSpan()));
        }

        [Theory]
        [InlineData(new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 }, "0011223344556677")]
        [InlineData(new byte[] { 0x1a, 0x2b, 0x3c, 0x4d, 0x5e, 0x6f, 0x99, 0x88 }, "1a2b3c4d5e6f9988")]
        public void Ctor_Bytes_Success(byte[] bytes, string expectedTokenString)
        {
            // Test and assert: byte[] ctor

            var pkt1 = new PublicKeyToken(bytes);
            Assert.Equal(bytes, pkt1.TokenBytes.ToArray());
            Assert.Equal(expectedTokenString, pkt1.TokenString);
            Assert.Equal(expectedTokenString, pkt1.ToString());

            // Test and assert: ROS<byte> ctor

            var pkt2 = new PublicKeyToken(bytes.AsSpan());
            Assert.Equal(bytes, pkt2.TokenBytes.ToArray());
            Assert.Equal(expectedTokenString, pkt2.TokenString);
            Assert.Equal(expectedTokenString, pkt2.ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("00112233445566")]
        [InlineData("001122334455667788")]
        [InlineData("0011223G44556677")] // start of bad hex (edge case testing) entries
        [InlineData("0011223g44556677")]
        [InlineData("0011223344556@77")]
        [InlineData("0011223344556`77")]
        [InlineData("001122334455/677")]
        [InlineData("001122334455:677")]
        public void Ctor_String_NegativeTests(string str)
        {
            Assert.Throws<ArgumentException>("tokenString", () => new PublicKeyToken(str));
            Assert.Throws<ArgumentException>("tokenString", () => new PublicKeyToken(str.AsSpan()));
        }

        [Theory]
        [InlineData("0011223344556677", "0011223344556677", new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77 })]
        [InlineData("1a2b3c4d5e6f9988", "1a2b3c4d5e6f9988", new byte[] { 0x1a, 0x2b, 0x3c, 0x4d, 0x5e, 0x6f, 0x99, 0x88 })]
        [InlineData("1A2B3c4d5e6f9988", "1a2b3c4d5e6f9988", new byte[] { 0x1a, 0x2b, 0x3c, 0x4d, 0x5e, 0x6f, 0x99, 0x88 })]
        public void Ctor_String_Success(string inputTokenString, string expectedTokenString, byte[] expectedBytes)
        {
            // Test and assert: string ctor

            var pkt1 = new PublicKeyToken(inputTokenString);
            Assert.Equal(expectedBytes, pkt1.TokenBytes.ToArray());
            Assert.Equal(expectedTokenString, pkt1.TokenString);
            Assert.Equal(expectedTokenString, pkt1.ToString());

            // Test and assert: ROS<byte> ctor

            var pkt2 = new PublicKeyToken(inputTokenString.AsSpan());
            Assert.Equal(expectedBytes, pkt2.TokenBytes.ToArray());
            Assert.Equal(expectedTokenString, pkt2.TokenString);
            Assert.Equal(expectedTokenString, pkt2.ToString());
        }

        [Fact]
        public void Equals_WithNullObjects()
        {
            PublicKeyToken nullPkt = null;
            PublicKeyToken notNullPkt = new PublicKeyToken("0011223344556677");

            Assert.False(notNullPkt.Equals(nullPkt));
            Assert.False(notNullPkt.Equals((object)nullPkt));

            Assert.False(notNullPkt == nullPkt);
            Assert.False(nullPkt == notNullPkt);
            Assert.False(notNullPkt == nullPkt);
            Assert.False(nullPkt == notNullPkt);
#pragma warning disable CS1718 // Comparison made to same variable - we're testing op_Equality
            Assert.True(nullPkt == nullPkt);
#pragma warning restore CS1718

            Assert.True(notNullPkt != nullPkt);
            Assert.True(nullPkt != notNullPkt);
            Assert.True(notNullPkt != nullPkt);
            Assert.True(nullPkt != notNullPkt);
#pragma warning disable CS1718 // Comparison made to same variable - we're testing op_Inequality
            Assert.False(nullPkt != nullPkt);
#pragma warning restore CS1718
        }

        [Theory]
        [InlineData("0011223344556677", "0011223344556677", true)]
        [InlineData("1A2B3c4d5e6f9988", "1a2b3c4d5e6f9988", true)]
        [InlineData("0011223344556677", "0011223344556688", false)]
        public void Equals_WithNonNullObjects(string tokenString1, string tokenString2, bool expectedEquals)
        {
            var pkt1 = new PublicKeyToken(tokenString1);
            var pkt2 = new PublicKeyToken(tokenString2);

            Assert.Equal(expectedEquals, pkt1.Equals(pkt2));
            Assert.Equal(expectedEquals, pkt2.Equals(pkt1));

            Assert.Equal(expectedEquals, pkt1.Equals((object)pkt2));
            Assert.Equal(expectedEquals, pkt2.Equals((object)pkt1));

            Assert.Equal(expectedEquals, pkt1 == pkt2);
            Assert.Equal(expectedEquals, pkt2 == pkt1);

            Assert.NotEqual(expectedEquals, pkt1 != pkt2);
            Assert.NotEqual(expectedEquals, pkt2 != pkt1);
        }

        [Fact]
        public void GetHashCode_WithEqualObjects_AreEqual()
        {
            var pkt1 = new PublicKeyToken("0011223344556677");
            var pkt2 = new PublicKeyToken("0011223344556677");

            Assert.Equal(pkt1.GetHashCode(), pkt2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_WithUnequalObjects_AreNotEqual()
        {
            var pkts = new[]
            {
                new PublicKeyToken("00112233445566aa"),
                new PublicKeyToken("00112233445566bb"),
                new PublicKeyToken("00112233445566cc"),
            };

            // The odds of a collision with any 2 objects are 1 in 2^32,
            // which is highly unlikely but not out of the realm of possibility.
            // But the odds of a *third* object colliding is smaller still,
            // so if we see all three objects colliding then we know something
            // went horribly wrong in our hash code calculation.

            var distinctHashCodeCount = pkts.Select(o => o.GetHashCode()).Distinct().Count();

            Assert.True(distinctHashCodeCount >= 2, "Saw unexpected hash code collisions?");
        }

        [Fact]
        public void GetHashCode_WithUnequalObjects_AndCraftedColldingTokens_AreNotEqual()
        {
            // Ensures that we're not relying on long.GetHashCode, where collisions
            // can be generated trivially (set same value for high and low components).

            var pkts = new[]
            {
                new PublicKeyToken("0123456701234567"),
                new PublicKeyToken("89abcdef89abcdef"),
                new PublicKeyToken("0011223300112233"),
            };

            // The odds of a collision with any 2 objects are 1 in 2^32,
            // which is highly unlikely but not out of the realm of possibility.
            // But the odds of a *third* object colliding is smaller still,
            // so if we see all three objects colliding then we know something
            // went horribly wrong in our hash code calculation.

            var distinctHashCodeCount = pkts.Select(o => o.GetHashCode()).Distinct().Count();

            Assert.True(distinctHashCodeCount >= 2, "Saw unexpected hash code collisions?");
        }
    }
}
