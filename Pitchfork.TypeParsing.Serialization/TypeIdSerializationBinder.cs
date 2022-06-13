using System;
using System.Globalization;
using System.Runtime.Serialization;
using Pitchfork.TypeParsing.Serialization.Resources;

namespace Pitchfork.TypeParsing.Serialization
{
    public abstract class TypeIdSerializationBinder : SerializationBinder
    {
        public TypeIdSerializationBinder(ParseOptions? parseOptions)
        {
            // Avoid callers passing ParseOptions.GlobalDefaults here and accidentally
            // mutating them when they only intended to mutate this one instance's options.

            ParseOptions = ParseOptions.Clone(parseOptions);
        }

        public ParseOptions ParseOptions { get; }

        public sealed override Type BindToType(string assemblyName, string typeName)
        {
            // We expect both assemblyName and typeName to be provided.
            // If either is missing or malformed, the entire operation fails.

            AssemblyId assemblyId = AssemblyId.Parse(assemblyName, ParseOptions);
            TypeId typeId = TypeId.Parse(typeName, assemblyId, ParseOptions);

            TypeId? typeIdToReturn;
            try
            {
                typeIdToReturn = BindToType(typeId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    message: string.Format(CultureInfo.CurrentCulture, SR.TypeIdSerializationBinder_TypeDisallowed, typeId),
                    innerException: ex);
            }

            Type? typeToReturn = typeIdToReturn?.DangerousGetRuntimeType(throwOnError: true);
            if (typeToReturn is null) // paranoia: also traps DangerousGetRuntimeType somehow returning null
            {
                throw new InvalidOperationException(
                    message: string.Format(CultureInfo.CurrentCulture, SR.TypeIdSerializationBinder_TypeDisallowed, typeId));
            }

            return typeToReturn;
        }

        public abstract TypeId? BindToType(TypeId typeId);
    }
}
