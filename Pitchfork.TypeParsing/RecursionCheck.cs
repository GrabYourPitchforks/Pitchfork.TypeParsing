using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Pitchfork.TypeParsing.Resources;

namespace Pitchfork.TypeParsing
{
    // WARNING - mutable struct
    [DebuggerDisplay("Current depth: {_currentDepth} of {_recursiveDepthLimit} (max observed = {_maxObservedDepth})")]
    internal struct RecursionCheck
    {
        private int _currentDepth;
        private int _maxObservedDepth;
        private int _recursiveDepthLimit;

        internal RecursionCheck(int recursiveDepthLimit)
        {
            Debug.Assert(recursiveDepthLimit > 0);
            _recursiveDepthLimit = recursiveDepthLimit;
            _currentDepth = 0;
            _maxObservedDepth = 0;
        }

        public int CurrentDepth
        {
            readonly get => _currentDepth;
            set
            {
                Debug.Assert(0 <= value && value <= _recursiveDepthLimit);
                _currentDepth = value;
                UpdateMaxObservedDepth();
            }
        }

        public int MaxObservedDepth
        {
            readonly get => _maxObservedDepth;
            set
            {
                Debug.Assert(0 <= value && value <= _recursiveDepthLimit);
                _maxObservedDepth = value;
                UpdateMaxObservedDepth();
            }
        }

        public void Dive()
        {
            Debug.Assert(0 <= _currentDepth && _currentDepth <= _recursiveDepthLimit);
            if (_currentDepth >= _recursiveDepthLimit)
            {
                ThrowMaxDepthExceededException(); // move out of inlineable method
            }
            _currentDepth++;
            UpdateMaxObservedDepth();
        }

        public void Surface()
        {
            _currentDepth--;
            Debug.Assert(0 <= _currentDepth && _currentDepth < _recursiveDepthLimit);
        }

        [DoesNotReturn]
        [StackTraceHidden]
        private readonly void ThrowMaxDepthExceededException()
        {
            throw new InvalidOperationException(
                message: string.Format(CultureInfo.CurrentCulture, SR.RecursionCheck_MaxDepthExceeded, _recursiveDepthLimit));
        }

        private void UpdateMaxObservedDepth()
        {
            if (_maxObservedDepth < _currentDepth)
            {
                _maxObservedDepth = _currentDepth;
            }
        }
    }
}
