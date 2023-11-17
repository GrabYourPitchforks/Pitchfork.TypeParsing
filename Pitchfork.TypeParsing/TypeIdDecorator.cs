namespace Pitchfork.TypeParsing
{
    internal abstract record class TypeIdDecorator
    {
        public static TypeIdDecorator ConstructedGeneric(TypeId[] genericArgs) => new ConstructedGenericDecorator(genericArgs);

        public static TypeIdDecorator ManagedPointer { get; } = new ManagedPointerDecorator();

        public static TypeIdDecorator MdArray(int rank) => new MdArrayDecorator(rank);

        public static TypeIdDecorator SzArray { get; } = new SzArrayDecorator();

        public static TypeIdDecorator UnmanagedPointer { get; } = new UnmanagedPointerDecorator();

        public abstract TypeId ApplyDecoratorOnto(TypeId typeId);

        private sealed record class ConstructedGenericDecorator(TypeId[] GenericArgs) : TypeIdDecorator
        {
            public override TypeId ApplyDecoratorOnto(TypeId typeId) => typeId.MakeGenericType(GenericArgs);
        }

        private sealed record class ManagedPointerDecorator : TypeIdDecorator
        {
            public override TypeId ApplyDecoratorOnto(TypeId typeId) => typeId.MakeManagedPointerType();
        }

        private sealed record class MdArrayDecorator(int Rank) : TypeIdDecorator
        {
            public override TypeId ApplyDecoratorOnto(TypeId typeId) => typeId.MakeVariableBoundArrayType(Rank);
        }

        private sealed record class SzArrayDecorator : TypeIdDecorator
        {
            public override TypeId ApplyDecoratorOnto(TypeId typeId) => typeId.MakeSzArrayType();
        }

        private sealed record class UnmanagedPointerDecorator : TypeIdDecorator
        {
            public override TypeId ApplyDecoratorOnto(TypeId typeId) => typeId.MakeUnmanagedPointerType();
        }
    }
}
