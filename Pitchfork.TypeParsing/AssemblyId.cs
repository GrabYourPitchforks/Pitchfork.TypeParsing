using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Pitchfork.TypeParsing
{
    /// <summary>
    /// Represents the result of parsing an assembly string without actually
    /// creating an <see cref="AssemblyName"/> or loading the assembly into
    /// the runtime.
    /// </summary>
    public sealed class AssemblyId : IEquatable<AssemblyId>, IRandomizedHashCode
    {
        // private ctor used for reading from existing loaded types
        private AssemblyId(AssemblyName existingAssemblyName, string exceptionParamName)
        {
            try
            {
                string? candidateName = existingAssemblyName.Name;
                IdentifierRestrictor.ThrowIfDisallowedAssemblyName(candidateName, ParseOptions.FromExistingIdentifier);
                Name = candidateName;

                Version = existingAssemblyName.Version;

                string? candidateCultureName = existingAssemblyName.CultureName;
                Culture = string.IsNullOrEmpty(candidateCultureName) ? "neutral" : candidateCultureName;

                var pktSpan = existingAssemblyName.GetPublicKeyToken().AsSpan();
                if (!pktSpan.IsEmpty)
                {
                    PublicKeyToken = new PublicKeyToken(pktSpan);
                }
            }
            catch (Exception ex)
            {
                // Throw a friendly error message, but accessing the FullName property might throw.
                // If it does, we'll just use the naked name in our exception message.

                string? asmName;
                try
                {
                    asmName = existingAssemblyName.FullName;
                }
                catch
                {
                    asmName = existingAssemblyName.Name;
                }
                ThrowHelper.ThrowArgumentException_CannotParseAssemblyFullName(asmName.AsSpan(), ex, exceptionParamName);
            }
        }

        // private ctor used when parsing a potentially untrusted string
        private AssemblyId(ReadOnlySpan<char> assemblyFullName, ParseOptions parseOptions)
        {
            AssemblyIdParseResult result;
            try
            {
                result = AssemblyIdParser.Parse(assemblyFullName, parseOptions);
                Name = result.Name;
                Version = result.Version;
                Culture = result.Culture;
                PublicKeyToken = result.PublicKeyToken;
            }
            catch (Exception ex)
            {
                ThrowHelper.ThrowArgumentException_CannotParseAssemblyFullName(assemblyFullName, ex, nameof(assemblyFullName));
            }
        }

        // private copy ctor
        private AssemblyId(AssemblyId other)
        {
            this.Name = other.Name;
            this.Version = other.Version;
            this.Culture = other.Culture;
            this.PublicKeyToken = other.PublicKeyToken;
        }

        /// <summary>
        /// Gets the culture name of this assembly, such as "en-us" or "fr-fr".
        /// By default, returns "neutral".
        /// </summary>
        /// <remarks>
        /// Culture is only used by satellite assemblies, which contain only resources
        /// and no executable code. Assemblies which contain code are expected to have
        /// a "neutral" culture. See the <see cref="AssemblyCultureAttribute"/> docs
        /// for more information.
        /// </remarks>
        public string Culture { get; set; }

        /// <summary>
        /// The name of this assembly; e.g., "mscorlib" or "YourCompany.YourAssembly".
        /// Does not contain the version, culture, or public key token.
        /// </summary>
        /// <remarks>
        /// Use <see cref="ToString"/> to retrieve the full name of this assembly,
        /// including the version, culture, and public key token.
        /// </remarks>
        public string Name { get; private set; }

        /// <summary>
        /// The public key token of this assembly, or null if there is no public key token.
        /// </summary>
        /// <remarks>
        /// <see cref="TypeParsing.PublicKeyToken"/> instances commonly observed throughout
        /// the .NET libraries are listed in the <see cref="WellKnownPublicKeyTokens"/> type.
        /// </remarks>
        public PublicKeyToken? PublicKeyToken { get; private set; }

        /// <summary>
        /// The version of this assembly, or null if there is no version.
        /// </summary>
        public Version? Version { get; private set; }

        /// <summary>
        /// Returns true iff two <see cref="AssemblyId"/> instances refer to the same assembly.
        /// </summary>
        public static bool operator ==(AssemblyId? a, AssemblyId? b) => (a is null) ? (b is null) : a.Equals(b);

        /// <summary>
        /// Returns true iff two <see cref="AssemblyId"/> instances refer to different assemblies.
        /// </summary>
        public static bool operator !=(AssemblyId? a, AssemblyId? b) => !(a == b);

        /// <summary>
        /// Creates an <see cref="AssemblyId"/> from a currently loaded <see cref="Assembly"/>.
        /// </summary>
        public static AssemblyId CreateFromExisting(Assembly assembly)
            => new AssemblyId(assembly.GetName(), nameof(assembly));

        /// <summary>
        /// Creates an <see cref="AssemblyId"/> from an existing <see cref="AssemblyName"/> instance.
        /// </summary>
        public static AssemblyId CreateFromExisting(AssemblyName assemblyName)
            => new AssemblyId(assemblyName, nameof(assemblyName));

        /// <summary>
        /// Returns true iff two <see cref="AssemblyId"/> instances refer to the same assembly.
        /// </summary>
        public override bool Equals(object? other) => Equals(other as AssemblyId);

        /// <summary>
        /// Returns true iff two <see cref="AssemblyId"/> instances refer to the same assembly.
        /// </summary>
        public bool Equals(AssemblyId? other)
        {
            return other is not null
                && this.Name == other.Name
                && this.Version == other.Version
                && CultureUtil.AreCulturesEqual(this.Culture, other.Culture)
                && this.PublicKeyToken == other.PublicKeyToken;
        }

        /// <summary>
        /// Gets a hash code for this instance.
        /// </summary>
        /// <remarks>The hash code is randomized per application instance. Callers should not persist the hash code.</remarks>
        public override int GetHashCode()
        {
            RandomizedHashCode hashCode = new RandomizedHashCode(RandomizedHashCode.Caller.AssemblyId);
            hashCode.Add(Name);
            hashCode.Add(Version);
            hashCode.Add(CultureUtil.AnsiToLower(Culture));
            hashCode.Add(PublicKeyToken);
            return hashCode.ToHashCode();
        }

        /// <summary>
        /// Determines whether the current assembly is under the hierchy specified by <paramref name="baseName"/>.
        /// </summary>
        /// <remarks>
        /// For example, if <paramref name="baseName"/> is "System.Foo", this method returns true
        /// if the current <see cref="AssemblyId"/> represents "System.Foo", "System.Foo.Bar",
        /// or "System.Foo.Bar.Baz". But it will return false if the current <see cref="AssemblyId"/>
        /// represents "System.Quux" or "System.Foo2", as neither of those is under the "System.Foo" hierarchy.
        /// </remarks>
        public bool IsAssemblyUnder(string baseName)
        {
            if (!Name.StartsWith(baseName, StringComparison.Ordinal))
            {
                return false;
            }

            return (Name.Length == baseName.Length)
                || (Name[baseName.Length] == '.');
        }

        /// <summary>
        /// Returns an <see cref="AssemblyName"/> instance generated from this <see cref="AssemblyId"/>.
        /// </summary>
        public AssemblyName ToAssemblyName()
        {
            AssemblyName asmName = new AssemblyName()
            {
                Name = Name,
                Version = Version,
                CultureName = (Culture == "neutral") ? string.Empty /* = invariant */ : Culture
            };

            // empty (not null) array => "PublicKeyToken=null"
            asmName.SetPublicKeyToken(((PublicKeyToken is not null) ? PublicKeyToken.TokenBytes : default).ToArray());

            return asmName;
        }

        /// <summary>
        /// Returns the full name of this assembly, including version, culture,
        /// and public key token. For example, for the main runtime library in
        /// .NET Framework, returns "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089".
        /// </summary>
        public override string ToString() => ToAssemblyName().FullName;

        /// <summary>
        /// Parses the provided assembly name and creates a new <see cref="AssemblyId"/> instance wrapping
        /// it. <paramref name="assemblyFullName"/> may also contain version, culture, and public key
        /// token information..
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="assemblyFullName"/> is malformed or disallowed.</exception>
        public static AssemblyId Parse(ReadOnlySpan<char> assemblyFullName, ParseOptions? parseOptions = null)
            => new AssemblyId(assemblyFullName, parseOptions ?? ParseOptions.GlobalDefaults);

        /// <summary>
        /// Parses the provided assembly name and creates a new <see cref="AssemblyId"/> instance wrapping
        /// it. <paramref name="assemblyFullName"/> may also contain version, culture, and public key
        /// token information..
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="assemblyFullName"/> is malformed or disallowed.</exception>
        public static AssemblyId Parse(string assemblyFullName, ParseOptions? parseOptions = null)
            => Parse(assemblyFullName.AsSpan(), parseOptions);

        /// <summary>
        /// Returns a new <see cref="AssemblyId"/> with the same <see cref="Name"/>,
        /// <see cref="Version"/>, and <see cref="PublicKeyToken"/> as this, but with
        /// the specified <see cref="Culture"/>.
        /// </summary>
        /// <remarks>
        /// Set <paramref name="cultureInfo"/> to null or <see cref="CultureInfo.InvariantCulture"/>
        /// to generate a "neutral" culture <see cref="AssemblyId"/>.
        /// </remarks>
        public AssemblyId WithCulture(CultureInfo? cultureInfo)
        {
            string? desiredCultureName = cultureInfo?.Name;
            if (string.IsNullOrEmpty(desiredCultureName))
            {
                desiredCultureName = "neutral";
            }

            return CultureUtil.AreCulturesEqual(this.Culture, desiredCultureName)
                ? this
                : new AssemblyId(this) { Culture = desiredCultureName! };
        }

        /// <summary>
        /// Returns a new <see cref="AssemblyId"/> with the same <see cref="Name"/>,
        /// <see cref="Version"/>, and <see cref="PublicKeyToken"/> as this, but with
        /// the specified <see cref="Culture"/>.
        /// </summary>
        /// <remarks>
        /// Set <paramref name="cultureInfo"/> to null, <see cref="string.Empty"/>, or "neutral"
        /// to generate a "neutral" culture <see cref="AssemblyId"/>.
        /// </remarks>
        public AssemblyId WithCulture(string? culture)
        {
            CultureInfo? ci = (string.IsNullOrEmpty(culture) || culture == "neutral") ? null : CultureUtil.GetPredefinedCultureInfo(culture!);
            return WithCulture(ci);
        }

        /// <summary>
        /// Returns a new <see cref="AssemblyId"/> with the same <see cref="Name"/>,
        /// <see cref="Version"/>, and <see cref="Culture"/> as this, but with
        /// the specified <see cref="PublicKeyToken"/>.
        /// </summary>
        public AssemblyId WithPublicKeyToken(PublicKeyToken? publicKeyToken)
        {
            return (this.PublicKeyToken == publicKeyToken)
                ? this
                : new AssemblyId(this) { PublicKeyToken = publicKeyToken };
        }

        /// <summary>
        /// Returns a new <see cref="AssemblyId"/> with the same <see cref="Name"/>,
        /// <see cref="Culture"/>, and <see cref="PublicKeyToken"/> as this, but with
        /// the specified <see cref="Version"/>.
        /// </summary>
        public AssemblyId WithVersion(Version? newVersion)
        {
            return (this.Version == newVersion)
                ? this
                : new AssemblyId(this) { Version = newVersion };
        }
    }
}
