using System.Collections.Generic;

namespace Pitchfork.TypeParsing
{
    internal static class EnumerableExtensions
    {
        // The enumerable arg is allowed to be null, in which case nothing will happen.
        public static TypeId ApplyAllDecoratorsOnto(this IEnumerable<TypeIdDecorator>? transforms, TypeId typeId)
        {
            if (transforms is not null)
            {
                foreach (var transform in transforms)
                {
                    typeId = transform.ApplyDecoratorOnto(typeId);
                }
            }
            return typeId;
        }
    }
}
