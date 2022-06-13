using System;

namespace Pitchfork.TypeParsing
{
    // Marker interface that denotes a type which has a randomized hash code.
    // No APIs exposed; just use normal object.GetHashCode.
    internal interface IRandomizedHashCode
    {
    }
}
