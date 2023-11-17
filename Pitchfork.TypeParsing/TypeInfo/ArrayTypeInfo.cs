using System;
using System.Diagnostics;
using Pitchfork.Common;
using Pitchfork.TypeParsing.Resources;

namespace Pitchfork.TypeParsing.TypeInfo
{
    internal sealed class ArrayTypeInfo : ComplexTypeInfoBase
    {
        internal ArrayTypeInfo(bool isSzArray, int rank, TypeId elementalType)
            : base(elementalType)
        {
            Debug.Assert(!isSzArray || rank == 1, "SzArray types must have rank 1.");

            if (!MiscUtil.IsBetweenInclusive(rank, 1, 32))
            {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(rank),
                    message: SR.Common_RankOutOfRange);
            }

            IsSzArray = isSzArray;
            Rank = rank;
        }

        public bool IsSzArray { get; }

        public int Rank { get; }

        public override bool Equals(object? obj)
        {
            return obj is ArrayTypeInfo other
                && this.IsSzArray == other.IsSzArray
                && this.Rank == other.Rank
                && this.ElementalType == other.ElementalType;
        }

        public override int GetHashCode()
        {
            RandomizedHashCode hashCode = new RandomizedHashCode(RandomizedHashCode.Caller.ArrayTypeInfo);
            hashCode.Add(IsSzArray);
            hashCode.Add(Rank);
            hashCode.Add(ElementalType);
            return hashCode.ToHashCode();
        }
    }
}
