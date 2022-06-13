using System;
using System.Diagnostics;

namespace Pitchfork.TypeParsing.TypeInfo
{
    internal abstract class ComplexTypeInfoBase : IRandomizedHashCode
    {
        protected ComplexTypeInfoBase(TypeId elementalType)
        {
            ElementalType = elementalType;
        }

        public TypeId ElementalType { get; }

        public override int GetHashCode()
        {
            Debug.Fail("This method should have been overridden.");
            throw new NotImplementedException();
        }
    }
}
