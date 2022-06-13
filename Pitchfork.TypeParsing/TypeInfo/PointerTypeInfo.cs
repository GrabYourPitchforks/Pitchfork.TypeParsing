using System;

namespace Pitchfork.TypeParsing.TypeInfo
{
    internal sealed class PointerTypeInfo : ComplexTypeInfoBase, IRandomizedHashCode
    {
        internal PointerTypeInfo(bool isManagedPointer, TypeId elementalType)
            : base(elementalType)
        {
            IsManagedPointer = isManagedPointer;
        }

        public bool IsManagedPointer { get; }

        public override bool Equals(object? obj)
        {
            return obj is PointerTypeInfo other
                && this.IsManagedPointer == other.IsManagedPointer
                && this.ElementalType == other.ElementalType;
        }

        public override int GetHashCode()
        {
            RandomizedHashCode hashCode = new RandomizedHashCode(RandomizedHashCode.Caller.PointerTypeInfo);
            hashCode.Add(IsManagedPointer);
            hashCode.Add(ElementalType);
            return hashCode.ToHashCode();
        }
    }
}
