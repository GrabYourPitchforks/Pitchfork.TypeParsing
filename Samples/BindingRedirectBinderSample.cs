using System;
using System.Runtime.Serialization;
using Pitchfork.TypeParsing;
using Pitchfork.TypeParsing.Serialization;

#pragma warning disable SYSLIB0011 // IFormatter.Deserialize is obsolete

namespace Samples
{
    internal static class BindingRedirectBinderSample
    {
        public static void Run()
        {
            // This sample shows how to use a binding redirect binder.
            // Useful for if you use a different strong name (different PKT) for
            // validation vs. production builds.
            //
            // TODO:
            // - Update the lines at the top of MyBindingRedirectVisitor to contain
            //   your assembly name and PKT values, or read them dynamically from
            //   config or the ambient environment.

            Stream inputStream = default; // replace this with the stream of your choosing
            IFormatter formatter = default; // replace this with the formatter of your choosing
            formatter.Binder = new MyBindingRedirectBinder();
            object deserialized = formatter.Deserialize(inputStream);
        }

        private sealed class MyBindingRedirectBinder : TypeIdSerializationBinder
        {
            public MyBindingRedirectBinder(ParseOptions options = null)
                : base(options)
            {
            }

            public override TypeId BindToType(TypeId typeId)
            {
                return typeId.Visit(new MyBindingRedirectVisitor()); // replace all old PKTs with the new PKT
            }
        }

        private sealed class MyBindingRedirectVisitor : TypeIdVisitor
        {
            private const string AssemblyHierarchyToReplace = "MyCompany.MyAssembly";
            private static readonly PublicKeyToken PktToSearchFor = new PublicKeyToken("0011223344556677");
            private static readonly PublicKeyToken PktToReplaceWith = new PublicKeyToken("8899aabbccddeeff");

            public override AssemblyId VisitAssembly(AssemblyId assembly)
            {
                // IsAssemblyUnder also finds assemblies matching "MyCompany.MyAssembly.*"
                if (assembly.IsAssemblyUnder(AssemblyHierarchyToReplace)
                    && assembly.PublicKeyToken == PktToSearchFor)
                {
                    return assembly.WithPublicKeyToken(PktToReplaceWith);
                }
                else
                {
                    return assembly; // no change
                }
            }
        }
    }
}
