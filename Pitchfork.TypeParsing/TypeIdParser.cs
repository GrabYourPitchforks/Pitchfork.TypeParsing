using System;

namespace Pitchfork.TypeParsing
{
    internal ref partial struct TypeIdParser
    {
        private readonly ParseOptions _parseOptions;
        private ReadOnlySpan<char> _inputString;
        private RecursionCheck _recursionCheck;

        private TypeIdParser(ReadOnlySpan<char> inputString, ParseOptions parseOptions)
        {
            _inputString = inputString;
            _recursionCheck = new RecursionCheck(parseOptions.MaxRecursiveDepth);
            _parseOptions = parseOptions;
        }

        public static TypeId Parse(ReadOnlySpan<char> inputString, bool allowFullyQualifiedName, ParseOptions parseOptions)
        {
            TypeIdParser parser = new TypeIdParser(inputString, parseOptions);

            // ReadNextTypeId should consume the entire string. No trailing data is
            // allowed, not even trivia like whitespace. This also matches the
            // behavior of Type.GetType(), which allows leading whitespace but
            // forbids trailing whitespace.

            TypeId retVal = parser.ReadNextTypeId(allowFullyQualifiedName);
            if (!parser._inputString.IsEmpty)
            {
                ThrowHelper.ThrowArgumentException_TypeId_InvalidTypeString();
            }

            return retVal;
        }

        internal TypeId ReadNextTypeId(bool allowFullyQualifiedName)
        {
            _recursionCheck.Dive();

            // The expected format of a reflection type name is:
            // [Type] [Generic args] [Decorators] [Assembly]
            //
            // So we'll pull out the type name first.

            ResultBuilder builder = new ResultBuilder(_parseOptions);
            builder.ConsumeTypeName(ref _inputString);

            // Are there any captured generic args? We'll look for "[[".
            // There are no spaces allowed before the first '[', but spaces are allowed
            // after that. The check slices _inputString, so we'll capture it into
            // a local so we can restore it later if needed.

            ReadOnlySpan<char> capturedBeforeGenericProcessing = _inputString;
            if (SpanUtil.TryStripFirstCharAndTrailingSpaces(ref _inputString, '[')
                && SpanUtil.TryStripFirstCharAndTrailingSpaces(ref _inputString, '['))
            {
                // Closed generic type found!
                //
                // The recursion check requires some special handling here since we're
                // about to make some recursive method calls. Basically, we want to
                // keep track of the max depth we've seen from any of the recursive
                // method calls we're about to make, then we want to reset the current
                // depth to whatever the max depth was. This allows the decorator
                // parsing routine to see an accurate *total* depth even though it
                // runs independently of the generic args parsing.
                //
                // For example, assume given "List<int[][][], string>[]".
                // The max depth seen while processing the generic is 5.
                // >> List (1) + int[][][] (4) = 5 <-- max
                // >> List (1) + string (1) = 2
                // Then the [] decorator at the very end bumps this up to 6.
                // If we didn't keep track of the max depth, we'd accidentally
                // interpret the max depth of "List<...>[]" as:
                // >> List(1) + [] (1) = 2 <-- incorrect

                RecursionCheck startingRecursionCheck = _recursionCheck;
                RecursionCheck maxObservedRecursionCheck = _recursionCheck;

            ParseAnotherGenericArg:

                _recursionCheck = startingRecursionCheck;
                TypeId genericArg = ReadNextTypeId(allowFullyQualifiedName: true); // generic args always allow AQNs
                if (_recursionCheck.CurrentDepth > maxObservedRecursionCheck.CurrentDepth)
                {
                    maxObservedRecursionCheck = _recursionCheck;
                }

                // There had better be a ']' after the type name.

                if (!SpanUtil.TryStripFirstCharAndTrailingSpaces(ref _inputString, ']'))
                {
                    ThrowHelper.ThrowArgumentException_TypeId_InvalidTypeString();
                }

                builder.AddGenericTypeArgument(genericArg);

                // Is there a ',[' indicating another generic type arg?

                if (SpanUtil.TryStripFirstCharAndTrailingSpaces(ref _inputString, ','))
                {
                    if (!SpanUtil.TryStripFirstCharAndTrailingSpaces(ref _inputString, '['))
                    {
                        ThrowHelper.ThrowArgumentException_TypeId_InvalidTypeString();
                    }

                    goto ParseAnotherGenericArg;
                }

                // The only other allowable character is ']', indicating the end of
                // the generic type arg list.

                if (!SpanUtil.TryStripFirstCharAndTrailingSpaces(ref _inputString, ']'))
                {
                    ThrowHelper.ThrowArgumentException_TypeId_InvalidTypeString();
                }

                // And now that we're at the end, restore the max observed recursion count.

                _recursionCheck = maxObservedRecursionCheck;
            }

            // If there was an error stripping the generic args, back up to
            // before we started processing them, and let the decorator
            // parser try handling it.

            if (!builder.HasGenericTypeArguments)
            {
                _inputString = capturedBeforeGenericProcessing;
            }

            // Strip off decorators one at a time, bumping the recursive depth each time.

            while (builder.TryConsumeSingleDecorator(ref _inputString))
            {
                _recursionCheck.Dive();
            }

            // Is an assembly name present?

            if (allowFullyQualifiedName && SpanUtil.TryStripFirstCharAndTrailingSpaces(ref _inputString, ','))
            {
                builder.ConsumeAssemblyName(ref _inputString);
            }

            // And that's it!

            return builder.Construct();
        }
    }
}
