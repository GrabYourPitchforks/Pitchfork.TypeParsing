using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Pitchfork.TypeParsing.Resources;

namespace Pitchfork.TypeParsing
{
    // WARNING - mutable struct
    [DebuggerDisplay("Current depth: {_currentDepth} of {_recursiveDepthLimit}")]
    internal struct RecursionCheck
    {
        private int _currentDepth;
        private readonly int _recursiveDepthLimit;

        internal RecursionCheck(int recursiveDepthLimit)
        {
            Debug.Assert(recursiveDepthLimit > 0);
            _recursiveDepthLimit = recursiveDepthLimit;
            _currentDepth = 0;
        }

        public readonly int CurrentDepth => _currentDepth;

        public void Dive()
        {
            Debug.Assert(0 <= _currentDepth && _currentDepth <= _recursiveDepthLimit);
            if (_currentDepth >= _recursiveDepthLimit)
            {
                ThrowMaxDepthExceededException(); // move out of inlineable method
            }
            _currentDepth++;
        }

        [DoesNotReturn]
        [StackTraceHidden]
        private readonly void ThrowMaxDepthExceededException()
        {
            throw new InvalidOperationException(
                message: string.Format(CultureInfo.CurrentCulture, SR.RecursionCheck_MaxDepthExceeded, _recursiveDepthLimit));
        }
    }
}
