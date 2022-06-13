using System;
using System.Collections.Generic;

namespace Pitchfork.TypeParsing
{
    internal struct TypeIdParseResult
    {
        private readonly ParseOptions _parseOptions;

        internal TypeIdParseResult(ParseOptions parseOptions)
        {
            this = default;
            _parseOptions = parseOptions;
        }

        public TypeIdDecoratorStack DecoratorStack; // field of mutable struct type, provide as field instead of property

        private string? _name;
        public string Name
        {
            readonly get => _name!;
            set
            {
                IdentifierRestrictor.ThrowIfDisallowedTypeName(value, _parseOptions);
                _name = value;
            }
        }

        public AssemblyId? AssemblyId { readonly get; set; }

        public List<TypeId>? GenericArgs { readonly get; set; }

        public TypeId Construct()
        {
            TypeId constructedType = TypeId.CreateElementalTypeWithoutValidation(Name, AssemblyId);
            if (GenericArgs is not null && GenericArgs.Count != 0)
            {
                constructedType = constructedType.MakeGenericType(GenericArgs.ToArray());
            }
            return DecoratorStack.PopAllDecoratorsOnto(constructedType);
        }
    }
}
