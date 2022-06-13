using System;
using System.Diagnostics;
using System.Globalization;
using Pitchfork.TypeParsing.Resources;

namespace Pitchfork.TypeParsing
{
    /// <summary>
    /// A visitor that allows analyzing or replacing all components
    /// of a <see cref="TypeId"/>.
    /// </summary>
    public abstract class TypeIdVisitor
    {
        /// <summary>
        /// Visits a <see cref="TypeId"/> which represents an array type
        /// (<see cref="TypeId.IsArrayType"/> returns true).
        /// </summary>
        /// <remarks>
        /// The default implementation of this method dispatches to
        /// <see cref="VisitSzArrayType"/> or <see cref="VisitVariableBoundArrayType"/>.
        /// </remarks>
        public virtual TypeId VisitArrayType(TypeId type)
        {
            if (!type.IsArrayType)
            {
                throw new ArgumentException(
                    paramName: nameof(type),
                    message: string.Format(CultureInfo.CurrentCulture, SR.TypeIdVisitor_UnexpectedType_ExpectedArrayType, type));
            }

            if (type.IsSzArrayType)
            {
                return VisitSzArrayType(type);
            }
            else
            {
                Debug.Assert(type.IsVariableBoundArrayType);
                return VisitVariableBoundArrayType(type);
            }
        }

        /// <summary>
        /// Visits an <see cref="AssemblyId"/> instance from an elemental <see cref="TypeId"/>.
        /// </summary>
        public virtual AssemblyId VisitAssembly(AssemblyId assembly)
        {
            return assembly;
        }

        /// <summary>
        /// Visits a <see cref="TypeId"/> which represents a constructed generic type
        /// (<see cref="TypeId.IsConstructedGenericType"/> returns true).
        /// </summary>
        /// <remarks>
        /// The default implementation of this method visits the underlying type
        /// (see <see cref="TypeId.GetGenericTypeDefinition"/>) and each generic type
        /// argument (see <see cref="TypeId.GetGenericParameters"/>).
        /// </remarks>
        public virtual TypeId VisitConstructedGenericType(TypeId type)
        {
            if (!type.IsConstructedGenericType)
            {
                throw new ArgumentException(
                    paramName: nameof(type),
                    message: string.Format(CultureInfo.CurrentCulture, SR.TypeIdVisitor_UnexpectedType_ExpectedConstructedGenericType, type));
            }

            TypeId[] genericParameters = type.GetGenericParameters(); // returns a copy
            bool wereAnyGenericParametersModified = false;
            for (int i = 0; i < genericParameters.Length; i++)
            {
                TypeId existingArg = genericParameters[i];
                TypeId newArg = VisitType(existingArg);
                if (!ReferenceEquals(existingArg, newArg))
                {
                    wereAnyGenericParametersModified = true;
                    genericParameters[i] = newArg;
                }
            }

            TypeId oldUnderlyingType = type.GetUnderlyingType()!;
            TypeId newUnderlyingType = VisitType(oldUnderlyingType);

            return ReferenceEquals(oldUnderlyingType, newUnderlyingType) && !wereAnyGenericParametersModified
                ? type
                : newUnderlyingType.MakeGenericType(genericParameters);
        }

        /// <summary>
        /// Visits a <see cref="TypeId"/> which represents an elemental type
        /// (<see cref="TypeId.IsElementalType"/> returns true).
        /// </summary>
        /// <remarks>
        /// The default implementation of this method visits the <see cref="TypeId"/>'s
        /// assembly (see <see cref="TypeId.Assembly"/>) if not null.
        /// </remarks>
        public virtual TypeId VisitElementalType(TypeId type)
        {
            if (!type.IsElementalType)
            {
                throw new ArgumentException(
                    paramName: nameof(type),
                    message: string.Format(CultureInfo.CurrentCulture, SR.TypeIdVisitor_UnexpectedType_ExpectedElementalType, type));
            }

            AssemblyId? oldAssembly = type.Assembly;
            if (oldAssembly is not null)
            {
                type = type.WithAssembly(VisitAssembly(oldAssembly));
            }
            return type;
        }

        /// <summary>
        /// Visits a <see cref="TypeId"/> which represents a managed pointer type
        /// (<see cref="TypeId.IsManagedPointerType"/> returns true).
        /// </summary>
        /// <remarks>
        /// The default implementation of this method visits the underlying type
        /// (see <see cref="TypeId.GetUnderlyingType"/>).
        /// </remarks>
        public virtual TypeId VisitManagedPointerType(TypeId type)
        {
            if (!type.IsManagedPointerType)
            {
                throw new ArgumentException(
                    paramName: nameof(type),
                    message: string.Format(CultureInfo.CurrentCulture, SR.TypeIdVisitor_UnexpectedType_ExpectedManagedPointerType, type));
            }

            TypeId oldUnderlyingType = type.GetUnderlyingType()!;
            TypeId newUnderlyingType = VisitType(oldUnderlyingType);

            return ReferenceEquals(oldUnderlyingType, newUnderlyingType)
                ? type
                : newUnderlyingType.MakeManagedPointerType();
        }

        /// <summary>
        /// Visits a <see cref="TypeId"/> which represents a szarray type
        /// (<see cref="TypeId.IsSzArrayType"/> returns true).
        /// </summary>
        /// <remarks>
        /// The default implementation of this method visits the underlying type
        /// (see <see cref="TypeId.GetUnderlyingType"/>).
        /// </remarks>
        public virtual TypeId VisitSzArrayType(TypeId type)
        {
            if (!type.IsSzArrayType)
            {
                throw new ArgumentException(
                    paramName: nameof(type),
                    message: string.Format(CultureInfo.CurrentCulture, SR.TypeIdVisitor_UnexpectedType_ExpectedSzArrayType, type));
            }

            TypeId oldUnderlyingType = type.GetUnderlyingType()!;
            TypeId newUnderlyingType = VisitType(oldUnderlyingType);

            return ReferenceEquals(oldUnderlyingType, newUnderlyingType)
                ? type
                : newUnderlyingType.MakeSzArrayType();
        }

        /// <summary>
        /// Visits a <see cref="TypeId"/>, dispatching to one of the more specialized
        /// visitor methods.
        /// </summary>
        public virtual TypeId VisitType(TypeId type)
        {
            if (type.IsElementalType)
            {
                return VisitElementalType(type);
            }
            else if (type.IsArrayType)
            {
                return VisitArrayType(type);
            }
            else if (type.IsConstructedGenericType)
            {
                return VisitConstructedGenericType(type);
            }
            else if (type.IsManagedPointerType)
            {
                return VisitManagedPointerType(type);
            }
            else if (type.IsUnmanagedPointerType)
            {
                return VisitUnmanagedPointerType(type);
            }
            else
            {
                Debug.Fail("We should never get here.");
                return type;
            }
        }

        /// <summary>
        /// Visits a <see cref="TypeId"/> which represents an unmanaged pointer type
        /// (<see cref="TypeId.IsUnmanagedPointerType"/> returns true).
        /// </summary>
        /// <remarks>
        /// The default implementation of this method visits the underlying type
        /// (see <see cref="TypeId.GetUnderlyingType"/>).
        /// </remarks>
        public virtual TypeId VisitUnmanagedPointerType(TypeId type)
        {
            if (!type.IsUnmanagedPointerType)
            {
                throw new ArgumentException(
                    paramName: nameof(type),
                    message: string.Format(CultureInfo.CurrentCulture, SR.TypeIdVisitor_UnexpectedType_ExpectedUnmanagedPointerType, type));
            }

            TypeId oldUnderlyingType = type.GetUnderlyingType()!;
            TypeId newUnderlyingType = VisitType(oldUnderlyingType);

            return ReferenceEquals(oldUnderlyingType, newUnderlyingType)
                ? type
                : newUnderlyingType.MakeUnmanagedPointerType();
        }

        /// <summary>
        /// Visits a <see cref="TypeId"/> which represents a variable-bound array type
        /// (<see cref="TypeId.IsVariableBoundArrayType"/> returns true).
        /// </summary>
        /// <remarks>
        /// The default implementation of this method visits the underlying type
        /// (see <see cref="TypeId.GetUnderlyingType"/>).
        /// </remarks>
        public virtual TypeId VisitVariableBoundArrayType(TypeId type)
        {
            if (!type.IsVariableBoundArrayType)
            {
                throw new ArgumentException(
                    paramName: nameof(type),
                    message: string.Format(CultureInfo.CurrentCulture, SR.TypeIdVisitor_UnexpectedType_ExpectedVariableBoundArrayType, type));
            }

            TypeId oldUnderlyingType = type.GetUnderlyingType()!;
            TypeId newUnderlyingType = VisitType(oldUnderlyingType);

            return ReferenceEquals(oldUnderlyingType, newUnderlyingType)
                ? type
                : newUnderlyingType.MakeVariableBoundArrayType(type.GetArrayRank());
        }
    }
}
