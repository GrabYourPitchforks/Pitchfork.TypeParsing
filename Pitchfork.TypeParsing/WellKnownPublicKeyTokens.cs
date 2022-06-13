using System;

// The values below come from:
// https://github.com/dotnet/arcade/blob/e7ede87875f41a9b3df898ae08da5ebc96e24f56/src/Microsoft.DotNet.Arcade.Sdk/tools/StrongName.targets#L23-L76

namespace Pitchfork.TypeParsing
{
    /// <summary>
    /// Contains <see cref="PublicKeyToken"/> values corresponding to common Microsoft-created
    /// .NET Framework and .NET libraries.
    /// </summary>
    public static class WellKnownPublicKeyTokens
    {
        /// <summary>
        /// The <see cref="PublicKeyToken"/> with value <em>b77a5c561934e089</em>, used by
        /// ECMA-speced libraries that are part of the .NET Framework, like mscorlib.dll.
        /// </summary>
        public static PublicKeyToken ECMA { get; } = new PublicKeyToken(new byte[] { 0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89 });

        /// <summary>
        /// The <see cref="PublicKeyToken"/> with value <em>b03f5f7f11d50a3a</em>, used by
        /// libraries that are part of the .NET Framework and SDK components like msbuild.
        /// </summary>
        public static PublicKeyToken Microsoft { get; } = new PublicKeyToken(new byte[] { 0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a });

        /// <summary>
        /// The <see cref="PublicKeyToken"/> with value <em>adb9793829ddae60</em>, used by ASP.NET
        /// Core 2.1+ and related .NET Core 2.1+ components.
        /// </summary>
        public static PublicKeyToken MicrosoftAspNetCore { get; } = new PublicKeyToken(new byte[] { 0xad, 0xb9, 0x79, 0x38, 0x29, 0xdd, 0xae, 0x60 });

        /// <summary>
        /// The <see cref="PublicKeyToken"/> with value <em>31bf3856ad364e35</em>, used by non-.NET
        /// Microsoft components like PowerShell, plus some WCF / WPF components and out-of-band
        /// ASP.NET Full Framework releases.
        /// </summary>
        public static PublicKeyToken MicrosoftShared { get; } = new PublicKeyToken(new byte[] { 0x31, 0xbf, 0x38, 0x56, 0xad, 0x36, 0x4e, 0x35 });

        /// <summary>
        /// The <see cref="PublicKeyToken"/> with value <em>7cec85d7bea7798e</em>, used by
        /// .NET Core components like System.Private.CoreLib.dll.
        /// </summary>
        public static PublicKeyToken SilverlightPlatform { get; } = new PublicKeyToken(new byte[] { 0x7c, 0xec, 0x85, 0xd7, 0xbe, 0xa7, 0x79, 0x8e });
    }
}
