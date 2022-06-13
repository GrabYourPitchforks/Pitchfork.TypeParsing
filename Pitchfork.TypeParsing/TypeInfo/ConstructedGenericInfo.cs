using System;
using System.Diagnostics;

namespace Pitchfork.TypeParsing.TypeInfo
{
    internal sealed class ConstructedGenericInfo : ComplexTypeInfoBase, IRandomizedHashCode
    {
        private readonly TypeId[] _genericArgs;

        internal ConstructedGenericInfo(TypeId[] genericArgs, TypeId elementalType)
            : base(elementalType)
        {
            Debug.Assert(genericArgs.Length > 0);
            Debug.Assert(elementalType.IsElementalType, "Generic type definition must be an elemental type.");

            _genericArgs = (TypeId[])genericArgs.Clone();
        }

        public ReadOnlySpan<TypeId> GenericArguments => _genericArgs;

        public override bool Equals(object? obj)
        {
            return obj is ConstructedGenericInfo other
                && this.GenericArguments.SequenceEqual(other.GenericArguments)
                && this.ElementalType == other.ElementalType;
        }

        public override int GetHashCode()
        {
            RandomizedHashCode hashCode = new RandomizedHashCode(RandomizedHashCode.Caller.ConstructedGenericInfo);
            foreach (TypeId typeId in GenericArguments)
            {
                hashCode.Add(typeId);
            }
            hashCode.Add(ElementalType);
            return hashCode.ToHashCode();
        }
    }
}
