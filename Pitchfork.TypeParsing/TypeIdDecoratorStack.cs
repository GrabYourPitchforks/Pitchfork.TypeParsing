using System;
using System.Collections.Generic;

namespace Pitchfork.TypeParsing
{
    internal struct TypeIdDecoratorStack
    {
        private Stack<Func<TypeId, TypeId>>? _decorators;

        private Stack<Func<TypeId, TypeId>> DecoratorStack => (_decorators ??= new());

        public TypeId PopAllDecoratorsOnto(TypeId underlyingType)
        {
            if (_decorators is not null)
            {
                foreach (var decorator in _decorators)
                {
                    underlyingType = decorator(underlyingType);
                }
                _decorators = null; // reset
            }
            return underlyingType;
        }

        public void PushMakeClosedGenericType(TypeId[] genericArgs)
        {
            DecoratorStack.Push(typeId => typeId.MakeGenericType(genericArgs));
        }

        public void PushMakeManagedPointerType()
        {
            DecoratorStack.Push(typeId => typeId.MakeManagedPointerType());
        }

        public void PushMakeSzArrayType()
        {
            DecoratorStack.Push(typeId => typeId.MakeSzArrayType());
        }

        public void PushMakeVariableBoundArrayType(int rank)
        {
            DecoratorStack.Push(typeId => typeId.MakeVariableBoundArrayType(rank));
        }

        public void PushMakeUnmanagedPointerType()
        {
            DecoratorStack.Push(typeId => typeId.MakeUnmanagedPointerType());
        }
    }
}
