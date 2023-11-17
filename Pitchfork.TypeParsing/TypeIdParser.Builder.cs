using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Pitchfork.TypeParsing
{
    internal ref partial struct TypeIdParser
    {
        private struct ResultBuilder
        {
            private const string EndOfTypeNameDelimiters = "[]&*,";

#if NET8_0_OR_GREATER
            private static readonly SearchValues<char> _endOfTypeNameDelimitersSearchValues = SearchValues.Create(EndOfTypeNameDelimiters);
#endif

            private List<TypeId>? _genericArgs;
            private readonly ParseOptions _options;
            private List<TypeIdDecorator>? _decorators;

            internal ResultBuilder(ParseOptions options)
            {
                _options = options;
            }

            public AssemblyId? AssemblyId { get; private set; }

            private List<TypeIdDecorator> DecoratorsNotNull => _decorators ??= new();

            [MemberNotNullWhen(true, nameof(_genericArgs))]
            public bool HasGenericTypeArguments => _genericArgs is { Count: > 0 };

            public string? TypeName { get; private set; }

            public void AddGenericTypeArgument(TypeId typeArg)
            {
                (_genericArgs ??= new()).Add(typeArg);
            }

            public TypeId Construct()
            {
                Debug.Assert(TypeName is not null, "ConsumeTypeName should've been called first.");

                // Name & assembly go first, then generics, then decorators

                TypeId typeId = TypeId.CreateElementalTypeWithoutValidation(TypeName, AssemblyId);
                if (HasGenericTypeArguments)
                {
                    typeId = typeId.MakeGenericType(_genericArgs.ToArray());
                }
                typeId = _decorators.ApplyAllDecoratorsOnto(typeId);

                return typeId;
            }

            public void ConsumeAssemblyName(ref ReadOnlySpan<char> input)
            {
                Debug.Assert(AssemblyId is null, "Assembly name shouldn't have been read yet.");

                // The only delimiter which can terminate an assembly name is ']'.
                // Otherwise EOL serves as the terminator.

                int assemblyNameLength = (int)Math.Min((uint)input.IndexOf(']'), (uint)input.Length);
                AssemblyId = AssemblyId.Parse(input.Slice(0, assemblyNameLength), _options);
                input = input.Slice(assemblyNameLength);
            }

            public void ConsumeTypeName(ref ReadOnlySpan<char> input)
            {
                Debug.Assert(TypeName is null, "Type name shouldn't have been read yet.");

                input = input.TrimStartSpacesOnly(); // spaces at beginning are ok
                int offset = GetOffsetOfEndOfTypeName(input);

                string candidate = input.Slice(0, offset).ToString();
                IdentifierRestrictor.ThrowIfDisallowedTypeName(candidate, _options);

                TypeName = candidate;
                input = input.Slice(offset);
            }

            // Normalizes "not found" to input length, since caller is expected to slice.
            private static int GetOffsetOfEndOfTypeName(ReadOnlySpan<char> input)
            {
                // NET 6+ guarantees that MemoryExtensions.IndexOfAny has worst-case complexity
                // O(m * i) if a match is found, or O(m * n) if a match is not found, where:
                //   i := index of match position
                //   m := number of needles
                //   n := length of search space (haystack)
                //
                // Downlevel versions of .NET do not make this guarantee, instead having a
                // worst-case complexity of O(m * n) even if a match occurs at the beginning of
                // the search space. Since we're running this in a loop over untrusted user
                // input, that makes the total loop complexity potentially O(m * n^2), where
                // 'n' is adversary-controlled. To avoid DoS issues here, we'll loop manually.

#if NET8_0_OR_GREATER
                int offset = input.IndexOfAny(_endOfTypeNameDelimitersSearchValues);
#elif NET6_0_OR_GREATER
                int offset = input.IndexOfAny(EndOfTypeNameDelimiters);
#else
                int offset;
                for (offset = 0; offset < input.Length; offset++)
                {
                    if (EndOfTypeNameDelimiters.IndexOf(input[offset]) >= 0) { break; }
                }
#endif

                return (int)Math.Min((uint)offset, (uint)input.Length);
            }

            public bool TryConsumeSingleDecorator(ref ReadOnlySpan<char> input)
            {
                // Then try pulling a single decorator.
                // Whitespace cannot precede the decorator, but it can follow the decorator.

                ReadOnlySpan<char> originalInput = input; // so we can restore on 'false' return

                if (SpanUtil.TryStripFirstCharAndTrailingSpaces(ref input, '*'))
                {
                    DecoratorsNotNull.Add(TypeIdDecorator.UnmanagedPointer);
                    return true;
                }

                if (SpanUtil.TryStripFirstCharAndTrailingSpaces(ref input, '&'))
                {
                    DecoratorsNotNull.Add(TypeIdDecorator.ManagedPointer);
                    return true;
                }

                if (SpanUtil.TryStripFirstCharAndTrailingSpaces(ref input, '['))
                {
                    // SZArray := []
                    // MDArray := [*] or [,] or [,,,, ...]

                    int rank = 1;
                    bool hasSeenAsterisk = false;

                ReadNextArrayToken:

                    if (SpanUtil.TryStripFirstCharAndTrailingSpaces(ref input, ']'))
                    {
                        // End of array marker
                        DecoratorsNotNull.Add((rank == 1 && !hasSeenAsterisk)
                            ? TypeIdDecorator.SzArray
                            : TypeIdDecorator.MdArray(rank));
                        return true;
                    }

                    if (!hasSeenAsterisk)
                    {
                        if (rank == 1 && SpanUtil.TryStripFirstCharAndTrailingSpaces(ref input, '*'))
                        {
                            // [*]
                            hasSeenAsterisk = true;
                            goto ReadNextArrayToken;
                        }
                        else if (SpanUtil.TryStripFirstCharAndTrailingSpaces(ref input, ','))
                        {
                            // [,,, ...]
                            checked { rank++; }
                            goto ReadNextArrayToken;
                        }
                    }

                    // Don't know what this token is.
                    // Fall through to 'return false' statement.
                }

                input = originalInput; // ensure 'ref input' not mutated
                return false;
            }
        }
    }
}
