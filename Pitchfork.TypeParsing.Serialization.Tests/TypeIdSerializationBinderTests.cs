using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Moq;
using Xunit;

#pragma warning disable SYSLIB0011 // we test BinaryFormatter-related code paths

namespace Pitchfork.TypeParsing.Serialization.Tests
{
    public class TypeIdSerializationBinderTests
    {
        [Fact]
        public void ParseOptions_IsCloned()
        {
            ParseOptions originalOptions = new ParseOptions()
            {
                AllowNonAsciiIdentifiers = true,
                MaxRecursiveDepth = 12345
            };

            var binder = new DelegatingBinder(type => type, originalOptions);
            ParseOptions newOptions = binder.ParseOptions;

            Assert.NotSame(originalOptions, newOptions);
            Assert.Equal(originalOptions.AllowNonAsciiIdentifiers, newOptions.AllowNonAsciiIdentifiers);
            Assert.Equal(originalOptions.MaxRecursiveDepth, newOptions.MaxRecursiveDepth);
        }

        [Fact]
        public void BindToType_IfReturnsNull_Throws()
        {
            var binder = new DelegatingBinder(_ => null);
            Assert.Throws<InvalidOperationException>(() => binder.BindToType("SomeAssembly", "SomeType"));
        }

        [Fact]
        public void BindToType_IfThrows_WrapsException()
        {
            Exception expectedInnerException = new Exception("Some inner exception");
            var binder = new DelegatingBinder(_ => throw expectedInnerException);
            var outerException = Assert.Throws<InvalidOperationException>(() => binder.BindToType("SomeAssembly", "SomeType"));
            Assert.Same(expectedInnerException, outerException.InnerException);
        }

        [Fact]
        public void BindToType_IsPassedAppropriateTypeId()
        {
            TypeId expectedTypeId = TypeId.CreateFromExisting(typeof(Dictionary<string, object>));
            TypeId actualTypeId = null;

            var binder = new DelegatingBinder(passedTypeId =>
            {
                actualTypeId = passedTypeId;
                return passedTypeId;
            });

            Type returnedType = binder.BindToType(typeof(Dictionary<,>).Assembly.FullName, typeof(Dictionary<string, object>).FullName);
            Assert.Equal(expectedTypeId, actualTypeId);
            Assert.Equal(typeof(Dictionary<string, object>), returnedType);
        }

        [Fact]
        public void BindToType_HonorsReturnedValueInsteadOfOriginalValue()
        {
            var binder = new DelegatingBinder(_ => TypeId.CreateFromExisting(typeof(int[]))); // ignore provided type
            Type boundType = binder.BindToType("SomeAssembly", "SomeType");
            Assert.Equal(typeof(int[]), boundType);
        }

        [Fact]
        public void BindToType_HonorsParseOptions()
        {
            bool binderWasCalled = false;
            var binder = new DelegatingBinder(
                _ => { binderWasCalled = true; throw new Exception("This should not get hit."); },
                new ParseOptions() { MaxRecursiveDepth = 1 });
            Assert.Throws<InvalidOperationException>(() => binder.BindToType("SomeAssembly", "SomeType[]")); // arrays will be disallowed by MaxDepth=1
            Assert.False(binderWasCalled, "We should have failed before invoking the main binder logic.");
        }

        [Fact]
        public void RoundTrip_ReplaceDictionaryWithCustomDictionary()
        {
            // Serialize

            var originalDictionary = new Dictionary<string, int>
            {
                ["Hello"] = 42,
                ["Goodbye"] = 100
            };

            MemoryStream ms = new MemoryStream();
            new BinaryFormatter().Serialize(ms, originalDictionary);

            // Deserialize, swapping type during deserialization

            ms.Position = 0;
            var formatter = new BinaryFormatter()
            {
                Binder = new DelegatingBinder(typeId =>
                {
                    // Dictionary -> CustomDictionary

                    TypeId dictionaryTypeFromSerializedBlob = TypeId.CreateFromExisting(typeof(Dictionary<,>), followTypeForwards: true);
                    TypeId customDictionaryType = TypeId.CreateFromExisting(typeof(CustomDictionary<,>));

                    var mock = new Mock<TypeIdVisitor>() { CallBase = true };
                    mock.Setup(o => o.VisitElementalType(dictionaryTypeFromSerializedBlob)).Returns(customDictionaryType);
                    TypeIdVisitor visitor = mock.Object;

                    return typeId.Visit(visitor);
                })
            };

            var deserializedObject = formatter.Deserialize(ms);
            var castObj = Assert.IsType<CustomDictionary<string, int>>(deserializedObject);
            Assert.Equal(2, castObj.Count);
            Assert.Equal(42, castObj["Hello"]);
            Assert.Equal(100, castObj["Goodbye"]);
        }

        private sealed class DelegatingBinder : TypeIdSerializationBinder
        {
            private readonly Func<TypeId, TypeId> _bindToTypeImpl;

            public DelegatingBinder(Func<TypeId, TypeId> bindToTypeImpl, ParseOptions options = null)
                : base(options)
            {
                _bindToTypeImpl = bindToTypeImpl;
            }

            public override TypeId BindToType(TypeId typeId) => _bindToTypeImpl(typeId);
        }

        [Serializable]
        private sealed class CustomDictionary<TKey, TValue> : Dictionary<TKey, TValue>
        {
            private CustomDictionary(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }

            public override void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                throw new NotImplementedException("I'm a deserialize-only type and should never be called.");
            }
        }
    }
}
