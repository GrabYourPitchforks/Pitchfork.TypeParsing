using System;
using System.Collections.Generic;
using System.Text;

namespace Pitchfork.TypeParsing
{
    internal readonly ref struct SplitResult<T>
    {
        private readonly ReadOnlySpan<T> _left;
        private readonly ReadOnlySpan<T> _right;
        public SplitResult(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        {
            _left = left;
            _right = right;
        }

        public void Deconstruct(out ReadOnlySpan<T> left, out ReadOnlySpan<T> right)
        {
            left = _left;
            right = _right;
        }
    }
}
