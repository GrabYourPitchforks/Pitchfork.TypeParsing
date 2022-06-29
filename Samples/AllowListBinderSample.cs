using System;
using System.Runtime.Serialization;
using Pitchfork.TypeParsing;
using Pitchfork.TypeParsing.Serialization;

#pragma warning disable SYSLIB0011 // IFormatter.Deserialize is obsolete

namespace Samples
{
    internal static class AllowListBinderSample
    {
        public static void Run()
        {
            // This sample shows how to instantiate and configure an allow-list binder.
            // The binder will reject all incoming types which are not on the allow list.
            // n.b. this still allows nested generics (e.g., List<List<List<int>>>)
            //      and arrays (e.g., List<int[]>[]).
            //
            // WARNING:
            // Use of a binder does not make unsafe serializers like BinaryFormatter safe
            // for untrusted inputs. But, since many developers insist on using this
            // pattern anyway, here's an example of how to apply the pattern using the
            // facilities exposed by this library.
            //
            // TODO:
            // - Set the stream and formatter below to non-null values.
            // - Update the MyAllowListingVisitor.GetAllowedTypes method to contain
            //   your list of allowed types.

            Stream inputStream = default; // replace this with the stream of your choosing
            IFormatter formatter = default; // replace this with the formatter of your choosing
            formatter.Binder = new MyAllowListingBinder();
            object deserialized = formatter.Deserialize(inputStream);
        }

        private sealed class MyAllowListingBinder : TypeIdSerializationBinder
        {
            public MyAllowListingBinder(ParseOptions options = null)
                : base(options)
            {
            }

            public override TypeId BindToType(TypeId typeId)
            {
                return typeId.Visit(new MyAllowListingVisitor()); // throw if any types not on the allow list
            }
        }

        private sealed class MyAllowListingVisitor : TypeIdVisitor
        {
            private static readonly Dictionary<string, TypeId> _allowedTypes =
                GetAllowedTypes().ToDictionary(type => type.FullName, type => TypeId.CreateFromExisting(type));

            private static IEnumerable<Type> GetAllowedTypes()
            {
                yield return typeof(int);
                yield return typeof(long);
                yield return typeof(string);
                yield return typeof(object);
                yield return typeof(Uri);
                yield return typeof(Guid);
                yield return typeof(List<>); // generic type definitions also work
                yield return typeof(Dictionary<,>); // generic type definitions also work
            }

            public override TypeId VisitElementalType(TypeId type)
            {
                // Discard the assembly information, using only the type name to
                // query the allow list. If the type exists in the allow list, we
                // throw out the incoming TypeId object and replace it with the
                // one that was present in the allow list.

                if (_allowedTypes.TryGetValue(type.Name, out TypeId actualTypeId))
                {
                    // This type is in the allow-list; let it through.
                    return actualTypeId;
                }
                else
                {
                    // This type is not in the allow list; deny it.
                    throw new InvalidOperationException($"Type {type} is disallowed.");
                }
            }
        }
    }
}
