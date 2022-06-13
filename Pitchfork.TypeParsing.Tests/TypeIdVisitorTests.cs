using System;
using System.Collections.Generic;
using Moq;
using Xunit;

namespace Pitchfork.TypeParsing.Tests
{
    public class TypeIdVisitorTests
    {
        private static AssemblyId asmId_Foo = AssemblyId.Parse("AssemblyFoo");
        private static AssemblyId asmId_Bar = AssemblyId.Parse("AssemblyBar");

        private static TypeId typeId_Int32 = TypeId.CreateFromExisting(typeof(int));
        private static TypeId typeId_Int32SzArr = typeId_Int32.MakeSzArrayType();
        private static TypeId typeId_Int32MdArrRank1 = typeId_Int32.MakeVariableBoundArrayType(1);
        private static TypeId typeId_Int32MdArrRank10 = typeId_Int32.MakeVariableBoundArrayType(10);
        private static TypeId typeId_Int32ByRef = typeId_Int32.MakeManagedPointerType();
        private static TypeId typeId_Int32Ptr = typeId_Int32.MakeUnmanagedPointerType();

        private static TypeId typeId_Int64 = TypeId.CreateFromExisting(typeof(long));
        private static TypeId typeId_Int64SzArr = typeId_Int64.MakeSzArrayType();
        private static TypeId typeId_Int64MdArrRank10 = typeId_Int64.MakeVariableBoundArrayType(10);
        private static TypeId typeId_Int64ByRef = typeId_Int64.MakeManagedPointerType();
        private static TypeId typeId_Int64Ptr = typeId_Int64.MakeUnmanagedPointerType();

        private static TypeId typeId_Dummy_NoAsm = TypeId.ParseAssemblyQualifiedName("Dummy");
        private static TypeId typeId_Dummy_AsmFoo = typeId_Dummy_NoAsm.WithAssembly(asmId_Foo);
        private static TypeId typeId_Dummy_AsmBar = typeId_Dummy_NoAsm.WithAssembly(asmId_Bar);

        private static TypeId typeId_Generic2_NoAsm = TypeId.ParseAssemblyQualifiedName("Generic`2");
        private static TypeId typeId_Generic2_AsmFoo = typeId_Generic2_NoAsm.WithAssembly(asmId_Foo);
        private static TypeId typeId_Generic2_AsmBar = typeId_Generic2_NoAsm.WithAssembly(asmId_Bar);

        [Fact]
        public void VisitArrayType_IsNotArrayType_Throws()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitArrayType(It.IsAny<TypeId>())).CallBase();
            TypeIdVisitor visitor = mock.Object;

            // Act & assert

            Assert.Throws<ArgumentException>("type", () => visitor.VisitArrayType(typeId_Int32));
        }

        [Fact]
        public void VisitArrayType_WithSzArrayType_CallsVisitSzArrayType_AndForwardsReturnValue()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitArrayType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitSzArrayType(typeId_Int32SzArr)).Returns(typeId_Dummy_NoAsm);

            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitArrayType(typeId_Int32SzArr);

            // Assert

            Assert.Same(typeId_Dummy_NoAsm, retVal);
        }

        [Fact]
        public void VisitArrayType_WithVariableBoundArrayType_CallsVisitVariableBoundArrayType_AndForwardsReturnValue()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitArrayType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitVariableBoundArrayType(typeId_Int32MdArrRank10)).Returns(typeId_Dummy_NoAsm);

            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitArrayType(typeId_Int32MdArrRank10);

            // Assert

            Assert.Same(typeId_Dummy_NoAsm, retVal);
        }

        [Fact]
        public void VisitAssembly_ReturnsInput_AndCallsNothingElse()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitAssembly(It.IsAny<AssemblyId>())).CallBase();
            TypeIdVisitor visitor = mock.Object;

            AssemblyId assemblyId = AssemblyId.CreateFromExisting(typeof(TypeIdVisitorTests).Assembly);

            // Act

            AssemblyId retVal = visitor.VisitAssembly(assemblyId);

            // Assert

            Assert.Same(assemblyId, retVal);
        }

        [Fact]
        public void VisitConstructedGenericType_IsNotConstructedGenericType_Throws()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitConstructedGenericType(It.IsAny<TypeId>())).CallBase();
            TypeIdVisitor visitor = mock.Object;

            // Act & assert

            Assert.Throws<ArgumentException>("type", () => visitor.VisitConstructedGenericType(typeId_Dummy_NoAsm));
        }

        [Fact]
        public void VisitConstructedGenericType_WithConstructedGenericType_VisitsUnderlyingTypeAndGenericArgs_WithChangesToUnderlyingType()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitConstructedGenericType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitType(typeId_Generic2_NoAsm)).Returns(typeId_Generic2_AsmFoo);
            mock.Setup(o => o.VisitType(typeId_Dummy_AsmFoo)).Returns(typeId_Dummy_AsmFoo);
            mock.Setup(o => o.VisitType(typeId_Dummy_AsmBar)).Returns(typeId_Dummy_AsmBar);
            TypeIdVisitor visitor = mock.Object;

            TypeId constructedGenericType = typeId_Generic2_NoAsm.MakeGenericType(typeId_Dummy_AsmFoo, typeId_Dummy_AsmBar);
            TypeId expectedRetVal = typeId_Generic2_AsmFoo.MakeGenericType(typeId_Dummy_AsmFoo, typeId_Dummy_AsmBar);

            // Act

            TypeId retVal = visitor.VisitConstructedGenericType(constructedGenericType);

            // Assert

            Assert.Equal(expectedRetVal, retVal);
        }

        [Fact]
        public void VisitConstructedGenericType_WithConstructedGenericType_VisitsUnderlyingTypeAndGenericArgs_WithChangesToFirstGenericArg()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitConstructedGenericType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitType(typeId_Generic2_NoAsm)).Returns(typeId_Generic2_NoAsm);
            mock.Setup(o => o.VisitType(typeId_Dummy_AsmFoo)).Returns(typeId_Int32);
            mock.Setup(o => o.VisitType(typeId_Dummy_AsmBar)).Returns(typeId_Dummy_AsmBar);
            TypeIdVisitor visitor = mock.Object;

            TypeId constructedGenericType = typeId_Generic2_NoAsm.MakeGenericType(typeId_Dummy_AsmFoo, typeId_Dummy_AsmBar);
            TypeId expectedRetVal = typeId_Generic2_NoAsm.MakeGenericType(typeId_Int32, typeId_Dummy_AsmBar);

            // Act

            TypeId retVal = visitor.VisitConstructedGenericType(constructedGenericType);

            // Assert

            Assert.Equal(expectedRetVal, retVal);
        }

        [Fact]
        public void VisitConstructedGenericType_WithConstructedGenericType_VisitsUnderlyingTypeAndGenericArgs_WithChangesToSecondGenericArg()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitConstructedGenericType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitType(typeId_Generic2_NoAsm)).Returns(typeId_Generic2_NoAsm);
            mock.Setup(o => o.VisitType(typeId_Dummy_AsmFoo)).Returns(typeId_Dummy_AsmFoo);
            mock.Setup(o => o.VisitType(typeId_Dummy_AsmBar)).Returns(typeId_Int64);
            TypeIdVisitor visitor = mock.Object;

            TypeId constructedGenericType = typeId_Generic2_NoAsm.MakeGenericType(typeId_Dummy_AsmFoo, typeId_Dummy_AsmBar);
            TypeId expectedRetVal = typeId_Generic2_NoAsm.MakeGenericType(typeId_Dummy_AsmFoo, typeId_Int64);

            // Act

            TypeId retVal = visitor.VisitConstructedGenericType(constructedGenericType);

            // Assert

            Assert.Equal(expectedRetVal, retVal);
        }

        [Fact]
        public void VisitConstructedGenericType_WithConstructedGenericType_VisitsUnderlyingTypeAndGenericArgs_WithoutChanges()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitConstructedGenericType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitType(typeId_Generic2_NoAsm)).Returns(typeId_Generic2_NoAsm);
            mock.Setup(o => o.VisitType(typeId_Dummy_AsmFoo)).Returns(typeId_Dummy_AsmFoo);
            mock.Setup(o => o.VisitType(typeId_Dummy_AsmBar)).Returns(typeId_Dummy_AsmBar);
            TypeIdVisitor visitor = mock.Object;

            TypeId constructedGenericType = typeId_Generic2_NoAsm.MakeGenericType(typeId_Dummy_AsmFoo, typeId_Dummy_AsmBar);

            // Act

            TypeId retVal = visitor.VisitConstructedGenericType(constructedGenericType);

            // Assert

            Assert.Same(constructedGenericType, retVal);
        }

        [Fact]
        public void VisitElementalType_IsNotElementalType_Throws()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitElementalType(It.IsAny<TypeId>())).CallBase();
            TypeIdVisitor visitor = mock.Object;

            // Act & assert

            Assert.Throws<ArgumentException>("type", () => visitor.VisitElementalType(typeId_Int32SzArr));
        }

        [Fact]
        public void VisitElementalType_WithElementalType_AndAssembly_VisitsTheAssembly_WithChanges()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitElementalType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitAssembly(asmId_Foo)).Returns(asmId_Bar);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitElementalType(typeId_Dummy_AsmFoo);

            // Assert

            Assert.Equal(typeId_Dummy_AsmBar, retVal);
        }

        [Fact]
        public void VisitElementalType_WithElementalType_AndAssembly_VisitsTheAssembly_WithoutChanges()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitElementalType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitAssembly(asmId_Foo)).CallBase();
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitElementalType(typeId_Dummy_AsmFoo);

            // Assert

            Assert.Same(typeId_Dummy_AsmFoo, retVal);
        }

        [Fact]
        public void VisitElementalType_WithElementalType_AndNoAssembly_ReflectsInput_AndCallsNothingElse()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitElementalType(It.IsAny<TypeId>())).CallBase();
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitElementalType(typeId_Dummy_NoAsm);

            // Assert

            Assert.Same(typeId_Dummy_NoAsm, retVal);
        }

        [Fact]
        public void VisitManagedPointerType_IsNotManagedPointerType_Throws()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitManagedPointerType(It.IsAny<TypeId>())).CallBase();
            TypeIdVisitor visitor = mock.Object;

            // Act & assert

            Assert.Throws<ArgumentException>("type", () => visitor.VisitManagedPointerType(typeId_Int32Ptr));
        }

        [Fact]
        public void VisitManagedPointerType_WithManagedPointerType_VisitsUnderlyingType_WithChanges()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitManagedPointerType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitType(typeId_Int32)).Returns(typeId_Int64);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitManagedPointerType(typeId_Int32ByRef);

            // Assert

            Assert.Equal(typeId_Int64ByRef, retVal);
        }

        [Fact]
        public void VisitManagedPointerType_WithManagedPointerType_VisitsUnderlyingType_WithoutChanges()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitManagedPointerType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitType(typeId_Int32)).Returns(typeId_Int32);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitManagedPointerType(typeId_Int32ByRef);

            // Assert

            Assert.Same(typeId_Int32ByRef, retVal);
        }

        [Fact]
        public void VisitSzArrayType_IsNotSzArrayType_Throws()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitSzArrayType(It.IsAny<TypeId>())).CallBase();
            TypeIdVisitor visitor = mock.Object;

            // Act & assert

            Assert.Throws<ArgumentException>("type", () => visitor.VisitSzArrayType(typeId_Int32MdArrRank1));
        }

        [Fact]
        public void VisitSzArrayType_WithSzArrayType_VisitsUnderlyingType_WithChanges()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitSzArrayType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitType(typeId_Int32)).Returns(typeId_Int64);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitSzArrayType(typeId_Int32SzArr);

            // Assert

            Assert.Equal(typeId_Int64SzArr, retVal);
        }

        [Fact]
        public void VisitSzArrayType_WithSzArrayType_VisitsUnderlyingType_WithoutChanges()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitSzArrayType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitType(typeId_Int32)).Returns(typeId_Int32);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitSzArrayType(typeId_Int32SzArr);

            // Assert

            Assert.Same(typeId_Int32SzArr, retVal);
        }

        [Fact]
        public void VisitType_WithConstructedGenericType_CallsVisitConstructedGenericType_AndForwardsReturnValue()
        {
            // Arrange

            TypeId constructedGenericType = typeId_Generic2_NoAsm.MakeGenericType(typeId_Int32, typeId_Int64);

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitConstructedGenericType(constructedGenericType)).Returns(typeId_Dummy_NoAsm);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitType(constructedGenericType);

            // Assert

            Assert.Same(typeId_Dummy_NoAsm, retVal);
        }

        [Fact]
        public void VisitType_WithElementalType_CallsVisitElementalType_AndForwardsReturnValue()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitElementalType(typeId_Int32)).Returns(typeId_Dummy_NoAsm);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitType(typeId_Int32);

            // Assert

            Assert.Same(typeId_Dummy_NoAsm, retVal);
        }

        [Fact]
        public void VisitType_WithManagedPointerType_CallsVisitManagedPointerType_AndForwardsReturnValue()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitManagedPointerType(typeId_Int32ByRef)).Returns(typeId_Dummy_NoAsm);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitType(typeId_Int32ByRef);

            // Assert

            Assert.Same(typeId_Dummy_NoAsm, retVal);
        }

        [Fact]
        public void VisitType_WithMdArrayType_CallsVisitArrayType_AndForwardsReturnValue()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitArrayType(typeId_Int32MdArrRank1)).Returns(typeId_Dummy_NoAsm);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitType(typeId_Int32MdArrRank1);

            // Assert

            Assert.Same(typeId_Dummy_NoAsm, retVal);
        }

        [Fact]
        public void VisitType_WithSzArrayType_CallsVisitArrayType_AndForwardsReturnValue()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitArrayType(typeId_Int32SzArr)).Returns(typeId_Dummy_NoAsm);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitType(typeId_Int32SzArr);

            // Assert

            Assert.Same(typeId_Dummy_NoAsm, retVal);
        }

        [Fact]
        public void VisitType_WithUnmanagedPointerType_CallsVisitUnmanagedPointerType_AndForwardsReturnValue()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitUnmanagedPointerType(typeId_Int32Ptr)).Returns(typeId_Dummy_NoAsm);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitType(typeId_Int32Ptr);

            // Assert

            Assert.Same(typeId_Dummy_NoAsm, retVal);
        }

        [Fact]
        public void VisitUnmanagedPointerType_IsNotUnmanagedPointerType_Throws()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitUnmanagedPointerType(It.IsAny<TypeId>())).CallBase();
            TypeIdVisitor visitor = mock.Object;

            // Act & assert

            Assert.Throws<ArgumentException>("type", () => visitor.VisitUnmanagedPointerType(typeId_Int32ByRef));
        }

        [Fact]
        public void VisitUnmanagedPointerType_WithUnmanagedPointerType_VisitsUnderlyingType_WithChanges()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitUnmanagedPointerType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitType(typeId_Int32)).Returns(typeId_Int64);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitUnmanagedPointerType(typeId_Int32Ptr);

            // Assert

            Assert.Equal(typeId_Int64Ptr, retVal);
        }

        [Fact]
        public void VisitUnmanagedPointerType_WithUnmanagedPointerType_VisitsUnderlyingType_WithoutChanges()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitUnmanagedPointerType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitType(typeId_Int32)).Returns(typeId_Int32);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitUnmanagedPointerType(typeId_Int32Ptr);

            // Assert

            Assert.Same(typeId_Int32Ptr, retVal);
        }

        [Fact]
        public void VisitVariableBoundArrayType_IsNotVariableBoundArrayType_Throws()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitVariableBoundArrayType(It.IsAny<TypeId>())).CallBase();
            TypeIdVisitor visitor = mock.Object;

            // Act & assert

            Assert.Throws<ArgumentException>("type", () => visitor.VisitVariableBoundArrayType(typeId_Int32SzArr));
        }

        [Fact]
        public void VisitVariableBoundArrayType_WithVariableBoundArrayType_VisitsUnderlyingType_WithChanges()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitVariableBoundArrayType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitType(typeId_Int32)).Returns(typeId_Int64);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitVariableBoundArrayType(typeId_Int32MdArrRank10);

            // Assert

            Assert.Equal(typeId_Int64MdArrRank10, retVal);
        }

        [Fact]
        public void VisitVariableBoundArrayType_WithVariableboundArrayType_VisitsUnderlyingType_WithoutChanges()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitVariableBoundArrayType(It.IsAny<TypeId>())).CallBase();
            mock.Setup(o => o.VisitType(typeId_Int32)).Returns(typeId_Int32);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = visitor.VisitVariableBoundArrayType(typeId_Int32MdArrRank10);

            // Assert

            Assert.Same(typeId_Int32MdArrRank10, retVal);
        }

        [Fact]
        public void TypeId_Visit_CallsVisitType()
        {
            // Arrange

            var mock = new Mock<TypeIdVisitor>(MockBehavior.Strict);
            mock.Setup(o => o.VisitType(typeId_Int32SzArr)).Returns(typeId_Dummy_NoAsm);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = typeId_Int32SzArr.Visit(visitor);

            // Assert

            Assert.Same(typeId_Dummy_NoAsm, retVal);
        }

        [Fact]
        public void TypeId_Visit_Battery()
        {
            // Arrange

            TypeId original = TypeId.CreateFromExisting(typeof(Action<int, List<int>, Dictionary<int, int?[,]>>));
            TypeId expected = TypeId.CreateFromExisting(typeof(Action<long, List<long>, Dictionary<long, long?[,]>>));

            var mock = new Mock<TypeIdVisitor>() { CallBase = true };
            mock.Setup(o => o.VisitElementalType(typeId_Int32)).Returns(typeId_Int64);
            TypeIdVisitor visitor = mock.Object;

            // Act

            TypeId retVal = original.Visit(visitor); // replace int with long everywhere

            // Assert

            Assert.Equal(expected, retVal);
        }
    }
}
