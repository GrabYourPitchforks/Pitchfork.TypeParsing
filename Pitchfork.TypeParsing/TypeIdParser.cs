using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Pitchfork.TypeParsing
{
    internal static class TypeIdParser
    {
        public static TypeId Parse(ReadOnlySpan<char> inputString, bool allowFullyQualifiedName, ParseOptions parseOptions)
        {
            Worker worker = new Worker(inputString, parseOptions);

            // ReadNextTypeId should consume the entire string. No trailing data is
            // allowed, not even trivia like whitespace. This also matches the
            // behavior of Type.GetType(), which allows leading whitespace but
            // forbids trailing whitespace.

            TypeId retVal = worker.ReadNextTypeId(allowFullyQualifiedName);
            if (!worker.RemainingInput.IsEmpty)
            {
                ThrowHelper.ThrowArgumentException_TypeId_InvalidTypeString();
            }

            return retVal;
        }

        private ref struct Worker
        {
            private readonly ParseOptions _parseOptions;
            private ReadOnlySpan<char> _inputString;
            private RecursionCheck _recursionCheck;

            internal Worker(ReadOnlySpan<char> inputString, ParseOptions parseOptions)
            {
                _inputString = inputString;
                _recursionCheck = new RecursionCheck(parseOptions.MaxRecursiveDepth);
                _parseOptions = parseOptions;
            }

            internal readonly ReadOnlySpan<char> RemainingInput => _inputString;

            internal TypeId ReadNextTypeId(bool allowFullyQualifiedName)
            {
                _recursionCheck.Dive();

                int idxOfNextDelimiter = (allowFullyQualifiedName)
                    ? _inputString.IndexOfAny('[', ']', ',')
                    : _inputString.IndexOfAny('[', ']');

                TypeIdParseResult parseResult = new(_parseOptions);
                bool discoveredAssemblyName = false;

                char delimiterChar;
                if (idxOfNextDelimiter < 0)
                {
                    idxOfNextDelimiter = _inputString.Length;
                    delimiterChar = ']';
                }
                else
                {
                    delimiterChar = _inputString[idxOfNextDelimiter];
                }

                if (delimiterChar == ']')
                {
                    // Easy case: simple name, no generics, not fully-qualified.

                    parseResult.DecoratorStack = CreateDecoratorStack(_inputString.Slice(0, idxOfNextDelimiter).TrimStartSpacesOnly(), ref _recursionCheck, out ReadOnlySpan<char> friendlyName);
                    parseResult.Name = friendlyName.ToString();
                    _inputString = _inputString.Slice(idxOfNextDelimiter); // don't consume the delimiter
                }
                else if (delimiterChar == ',')
                {
                    // Medium-complexity case: no generics, but type name is fully-qualified.

                    Debug.Assert(allowFullyQualifiedName);

                    parseResult.DecoratorStack = CreateDecoratorStack(_inputString.Slice(0, idxOfNextDelimiter).TrimStartSpacesOnly(), ref _recursionCheck, out ReadOnlySpan<char> friendlyName);
                    parseResult.Name = friendlyName.ToString();
                    _inputString = _inputString.Slice(idxOfNextDelimiter + 1); // skip over ',' delimiter
                    discoveredAssemblyName = true;
                }
                else
                {
                    // Complex case: we found an open bracket, which could represent a constructed
                    // generic type (List`1[[A],[B],...]), an array (Int32[]), or even both (List`1[[...]][]).
                    // Constructed generic types always come before other decorators like arrays,
                    // so we'll handle that case first.

                    parseResult.Name = _inputString.Slice(0, idxOfNextDelimiter).TrimStartSpacesOnly().ToString();
                    _inputString = _inputString.Slice(idxOfNextDelimiter); // don't skip over delimiter

                    int bracketDepthLevel = 1;
                    bool isLegalToProcessGenericArgsHere = true;
                    int maxObservedDepthAfterGenericProcessing = -1; // sentinel meaning no generic processing took place

                    int i;
                    for (i = 1; i < _inputString.Length; i++)
                    {
                        switch (_inputString[i])
                        {
                            case ' ':
                                // skip space chars; they'll be consumed as part of the type name if needed
                                continue;

                            case '[' when bracketDepthLevel == 1 && isLegalToProcessGenericArgsHere:
                                {
                                    // parse generics
                                    _inputString = _inputString.Slice(i + 1); // skip delimiter
                                    List<TypeId> genericArgs = new List<TypeId>();

                                    // Temporarily reset the global max observed depth so that it instead
                                    // keeps track of the local (for this generic instantiation) observed
                                    // max depth. This allows us to handle a scenario like:
                                    // >> Tuple<T[], U[][], V[]>[]
                                    //
                                    // If we didn't temporarily reset the depth, we'd end up inadvertently
                                    // adding all of the observed depths together, resulting in an
                                    // incorrect "global" max observed depth of 5, when it really should be 3.

                                    int originalMaxObservedDepthBeforeGenericProcessing = _recursionCheck.MaxObservedDepth;
                                    _recursionCheck.MaxObservedDepth = _recursionCheck.CurrentDepth;

                                ReadNextGenericArg:

                                    // always allow FQN for generic args
                                    TypeId nextTypeId = ReadNextTypeId(allowFullyQualifiedName: true);
                                    genericArgs.Add(nextTypeId);

                                    // delimiter ']' must immediately follow type name (leading spaces disallowed)
                                    if (!_inputString.StartsWith(']'))
                                    {
                                        ThrowHelper.ThrowArgumentException_TypeId_InvalidTypeString();
                                    }

                                    _inputString = _inputString.Slice(1).TrimStartSpacesOnly();
                                    if (_inputString.StartsWith(','))
                                    {
                                        _inputString = _inputString.Slice(1).TrimStartSpacesOnly();

                                        // delimiter '[' must follow comma (leading spaces allowed)
                                        if (!_inputString.StartsWith('['))
                                        {
                                            ThrowHelper.ThrowArgumentException_TypeId_InvalidTypeString();
                                        }

                                        _inputString = _inputString.Slice(1).TrimStartSpacesOnly();
                                        goto ReadNextGenericArg;
                                    }
                                    else if (!_inputString.StartsWith(']'))
                                    {
                                        // saw something other than a comma or closing bracket
                                        ThrowHelper.ThrowArgumentException_TypeId_InvalidTypeString();
                                    }

                                    Debug.Assert(genericArgs.Count > 0);
                                    parseResult.GenericArgs = genericArgs;
                                    _inputString = _inputString.Slice(1).TrimStartSpacesOnly(); // swallow final ']'
                                    bracketDepthLevel = 0; // we swallowed the ']'
                                    isLegalToProcessGenericArgsHere = false; // already saw generic args
                                    i = -1; // reset indexer for outermost loop

                                    // Restore max observed depth to global max if needed.

                                    maxObservedDepthAfterGenericProcessing = _recursionCheck.MaxObservedDepth;
                                    _recursionCheck.MaxObservedDepth = Math.Max(maxObservedDepthAfterGenericProcessing, originalMaxObservedDepthBeforeGenericProcessing);
                                }
                                continue;

                            case '[':
                                checked { bracketDepthLevel++; }
                                continue;

                            case ']' when bracketDepthLevel > 0:
                                bracketDepthLevel--;
                                continue;

                            case ']':
                            case ',' when bracketDepthLevel == 0:
                                goto FoundEndOfDecoratedTypeName;

                            default:
                                isLegalToProcessGenericArgsHere = false;
                                continue; // any other chars will be consumed in decorator
                        }
                    }

                FoundEndOfDecoratedTypeName:

                    // Since we've unwound the generic processing stack, the current
                    // depth counter doesn't account for the full complexity. For
                    // example, List<Person[][][]>[] won't see the 3 decorators within
                    // the generic arg if we're only looking at the current depth.
                    // So we'll quickly fudge the current depth to account for the
                    // local max depth we saw (basically undoing the stack pop) before
                    // looking for decorators, then we'll switch the current depth
                    // back to the real value. Note: we want the decorator function
                    // to update the max observed depth, so we won't reset that property.

                    int originalDepthBeforeCreateDecoratorStack = _recursionCheck.CurrentDepth;
                    if (maxObservedDepthAfterGenericProcessing >= 0)
                    {
                        Debug.Assert(maxObservedDepthAfterGenericProcessing > originalDepthBeforeCreateDecoratorStack);
                        _recursionCheck.CurrentDepth = maxObservedDepthAfterGenericProcessing;
                    }
                    parseResult.DecoratorStack = CreateDecoratorStack(_inputString.Slice(0, i), ref _recursionCheck, out ReadOnlySpan<char> spacesBetweenTypeNameAndDecorator);
                    _recursionCheck.CurrentDepth = originalDepthBeforeCreateDecoratorStack;

                    if (!spacesBetweenTypeNameAndDecorator.TrimSpacesOnly().IsEmpty)
                    {
                        // Found an unrecognized decorator after the type name
                        ThrowHelper.ThrowArgumentException_TypeId_InvalidTypeString();
                    }

                    _inputString = _inputString.Slice(i); // don't consume the delimiter yet
                    if (allowFullyQualifiedName && _inputString.StartsWith(','))
                    {
                        // Everything from here forward is the assembly name
                        _inputString = _inputString.Slice(1); // skip ',' delimiter
                        discoveredAssemblyName = true;
                    }
                }

                if (discoveredAssemblyName)
                {
                    // If we discovered an assembly name, process it now.
                    // We should already have skipped over the ',' delimiter.

                    Debug.Assert(allowFullyQualifiedName);

                    int idxOfEndOfAssemblyName = _inputString.IndexOf(']');
                    if (idxOfEndOfAssemblyName < 0)
                    {
                        idxOfEndOfAssemblyName = _inputString.Length;
                    }
                    parseResult.AssemblyId = AssemblyId.Parse(_inputString.Slice(0, idxOfEndOfAssemblyName), _parseOptions);
                    _inputString = _inputString.Slice(idxOfEndOfAssemblyName); // let caller handle ']' delimiter
                }

                _recursionCheck.Surface();
                return parseResult.Construct();
            }

            private static TypeIdDecoratorStack CreateDecoratorStack(ReadOnlySpan<char> input, ref RecursionCheck recursionCheck, out ReadOnlySpan<char> leftoverData)
            {
                TypeIdDecoratorStack decoratorStack = default;
                int originalRecursionDepth = recursionCheck.CurrentDepth;
                bool isFirstLoopIteration = true;
                ReadOnlySpan<char> inputGoingInToLastIteration;
                while (true)
                {
                    // If the trimmed input is empty, there's nothing for us to do.

                    inputGoingInToLastIteration = input;
                    input = input.TrimEndSpacesOnly();
                    if (input.IsEmpty)
                    {
                        goto Return;
                    }

                    char ch = input[^1];

                    if (ch == '&' && isFirstLoopIteration)
                    {
                        // Found a managed pointer (byref), only valid as the first transform

                        recursionCheck.Dive();
                        decoratorStack.PushMakeManagedPointerType();
                        input = input[..^1];
                    }
                    else if (ch == '*')
                    {
                        // Found an unmanaged pointer

                        recursionCheck.Dive();
                        decoratorStack.PushMakeUnmanagedPointerType();
                        input = input[..^1];
                    }
                    else if (ch == ']')
                    {
                        // Found an array, but not yet sure of rank

                        recursionCheck.Dive();
                        int idxOfOpenBracket = input.LastIndexOf('[');
                        if (idxOfOpenBracket < 0)
                        {
                            // bad input: no matching open bracket
                            goto Return;
                        }
                        ReadOnlySpan<char> contentsWithinBrackets = input[(idxOfOpenBracket + 1)..^1].TrimSpacesOnly();

                        if (contentsWithinBrackets.IsEmpty)
                        {
                            // szarray, rank 1
                            decoratorStack.PushMakeSzArrayType();
                        }
                        else if (contentsWithinBrackets.Length == 1 && contentsWithinBrackets[0] == '*')
                        {
                            // variable-bound array, rank 1
                            decoratorStack.PushMakeVariableBoundArrayType(rank: 1);
                        }
                        else
                        {
                            // variable-bound array, rank n
                            // we expect only spaces and commas; count number of commas to determine rank
                            int rank = 1;
                            foreach (char innerCh in contentsWithinBrackets)
                            {
                                if (innerCh == ',')
                                {
                                    checked { rank++; }
                                }
                                else if (innerCh != ' ')
                                {
                                    goto Return; // unrecognized data
                                }
                            }
                            decoratorStack.PushMakeVariableBoundArrayType(rank);
                        }

                        input = input[..idxOfOpenBracket];
                    }
                    else
                    {
                        goto Return; // unrecognized data
                    }

                    isFirstLoopIteration = false; // constructs like byrefs are no longer valid
                }

            Return:

                recursionCheck.CurrentDepth = originalRecursionDepth;
                leftoverData = inputGoingInToLastIteration;
                return decoratorStack;
            }
        }
    }
}
