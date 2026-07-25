/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Rhetos.Dom.DefaultConcepts
{
    /// <summary>
    /// Caches the reflection lookup of the 'ToSimple' methods in the generated 'QueryExtensions' class,
    /// used by the generated <c>GenericToSimple</c> method.
    /// </summary>
    /// <remarks>
    /// The generated 'QueryExtensions' class contains one 'ToSimple' overload for each queryable data structure
    /// in the application, so searching for the matching overload on each call is expensive on large applications.
    /// </remarks>
    public static class ToSimpleMethodCache
    {
        /// <summary>
        /// The first key element is the generated 'QueryExtensions' class, the second key element is the concrete
        /// run-time type of the query (the ORM query implementation type, not the IQueryable interface).
        /// </summary>
        private static readonly ConcurrentDictionary<(Type QueryExtensionsType, Type QueryableType), MethodInfo> _toSimpleMethods = new();

        /// <summary>
        /// Returns the 'ToSimple' method from <paramref name="queryExtensionsType"/> that accepts
        /// the <paramref name="queryableType"/> argument. The result is cached for later calls.
        /// </summary>
        /// <param name="queryExtensionsType">The generated 'QueryExtensions' class that contains the 'ToSimple' methods.</param>
        /// <param name="queryableType">
        /// The concrete run-time type of the query argument (for example the ORM query implementation type),
        /// not the IQueryable interface, to keep the overload resolution same as on a direct method call.
        /// </param>
        /// <exception cref="FrameworkException">Thrown if there is no matching 'ToSimple' method. The error is not cached.</exception>
        public static MethodInfo GetToSimpleMethod(Type queryExtensionsType, Type queryableType)
        {
            return _toSimpleMethods.GetOrAdd((queryExtensionsType, queryableType), FindToSimpleMethod);
        }

        private static MethodInfo FindToSimpleMethod((Type QueryExtensionsType, Type QueryableType) key)
        {
            var method = key.QueryExtensionsType.GetMethod("ToSimple", BindingFlags.Static | BindingFlags.Public, null, new Type[] { key.QueryableType }, null);
            if (method == null)
                throw new FrameworkException("Cannot find 'ToSimple' method for argument type '" + key.QueryableType.ToString() + "'.");
            return method;
        }
    }
}
