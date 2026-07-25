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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.Dom.DefaultConcepts;
using Rhetos.TestCommon;
using System;
using System.Linq;

namespace Rhetos.CommonConcepts.Test
{
    [TestClass]
    public class ToSimpleMethodCacheTest
    {
        [TestMethod]
        public void ResolveOverloadByRuntimeQueryableType()
        {
            // The cache key is the concrete run-time type of the query (for example EnumerableQuery<T> or
            // EntityQueryable<T>), not the IQueryable<T> interface, same as in the generated GenericToSimple method.
            var method = ToSimpleMethodCache.GetToSimpleMethod(typeof(TestQueryExtensions), RuntimeQueryableType<QueryableA>());

            Assert.AreEqual(typeof(IQueryable<QueryableA>), method.GetParameters().Single().ParameterType);
            Assert.AreEqual(typeof(IQueryable<SimpleA>), method.ReturnType);

            var query = new[] { new QueryableA(), new QueryableA() }.AsQueryable();
            var simpleItems = (IQueryable<SimpleA>)method.Invoke(null, new object[] { query });
            Assert.AreEqual(2, simpleItems.Count());
        }

        [TestMethod]
        public void ResolveDifferentQueryableTypesIndependently()
        {
            var methodA = ToSimpleMethodCache.GetToSimpleMethod(typeof(TestQueryExtensions), RuntimeQueryableType<QueryableA>());
            var methodB = ToSimpleMethodCache.GetToSimpleMethod(typeof(TestQueryExtensions), RuntimeQueryableType<QueryableB>());

            Assert.AreEqual(typeof(IQueryable<QueryableA>), methodA.GetParameters().Single().ParameterType);
            Assert.AreEqual(typeof(IQueryable<QueryableB>), methodB.GetParameters().Single().ParameterType);
            Assert.AreNotSame(methodA, methodB);
        }

        [TestMethod]
        public void ResolveMostSpecificOverloadForDerivedQueryableType()
        {
            // IQueryable<out T> is covariant, so EnumerableQuery<QueryableDerived> is assignable to both
            // IQueryable<QueryableA> and IQueryable<QueryableDerived>. The reflection default binder must select
            // the most specific overload, same as before the cache was introduced.
            var methodBase = ToSimpleMethodCache.GetToSimpleMethod(typeof(TestQueryExtensions), RuntimeQueryableType<QueryableA>());
            var methodDerived = ToSimpleMethodCache.GetToSimpleMethod(typeof(TestQueryExtensions), RuntimeQueryableType<QueryableDerived>());

            Assert.AreEqual(typeof(IQueryable<QueryableA>), methodBase.GetParameters().Single().ParameterType);
            Assert.AreEqual(typeof(IQueryable<QueryableDerived>), methodDerived.GetParameters().Single().ParameterType);
        }

        [TestMethod]
        public void ReturnCachedMethodOnRepeatedCalls()
        {
            var queryableType = RuntimeQueryableType<QueryableB>();

            var method1 = ToSimpleMethodCache.GetToSimpleMethod(typeof(TestQueryExtensions), queryableType);
            var method2 = ToSimpleMethodCache.GetToSimpleMethod(typeof(TestQueryExtensions), queryableType);

            Assert.AreSame(method1, method2);
        }

        [TestMethod]
        public void CacheKeyIncludesQueryExtensionsType()
        {
            var queryableType = RuntimeQueryableType<QueryableA>();

            var method1 = ToSimpleMethodCache.GetToSimpleMethod(typeof(TestQueryExtensions), queryableType);
            var method2 = ToSimpleMethodCache.GetToSimpleMethod(typeof(AlternativeTestQueryExtensions), queryableType);

            Assert.AreEqual(typeof(IQueryable<SimpleA>), method1.ReturnType);
            Assert.AreEqual(typeof(IQueryable<SimpleB>), method2.ReturnType);
            Assert.AreNotSame(method1, method2);
        }

        [TestMethod]
        public void MissingOverloadThrowsEveryTime()
        {
            var unknownQueryableType = RuntimeQueryableType<QueryableWithoutToSimple>();

            // The error is not cached, the same as in the original implementation without the cache:
            // the method resolution is repeated and the exception is thrown on each call.
            for (int repeat = 0; repeat < 2; repeat++)
                TestUtility.ShouldFail<FrameworkException>(
                    () => ToSimpleMethodCache.GetToSimpleMethod(typeof(TestQueryExtensions), unknownQueryableType),
                    "Cannot find", "ToSimple", "QueryableWithoutToSimple");
        }

        /// <summary>
        /// Returns the concrete run-time type of the query, the same as <c>i.GetType()</c> in the generated
        /// <c>GenericToSimple</c> method returns the ORM query implementation type, not the IQueryable interface.
        /// </summary>
        private static Type RuntimeQueryableType<T>() => Array.Empty<T>().AsQueryable().GetType();

        #region Test fixtures that mimic the shape of the generated QueryExtensions class

        // Note: The 'this' modifier is omitted on the ToSimple methods because C# does not allow extension methods
        // in a nested class. It makes no difference for the tested method resolution, because extension methods
        // are resolved by reflection as regular public static methods.

        private class QueryableA { }

        private sealed class QueryableDerived : QueryableA { }

        private sealed class QueryableB { }

        private sealed class QueryableWithoutToSimple { }

        private sealed class SimpleA { }

        private sealed class SimpleDerived { }

        private sealed class SimpleB { }

        private static class TestQueryExtensions
        {
            public static IQueryable<SimpleA> ToSimple(IQueryable<QueryableA> query) => query.Select(item => new SimpleA());

            public static IQueryable<SimpleDerived> ToSimple(IQueryable<QueryableDerived> query) => query.Select(item => new SimpleDerived());

            public static IQueryable<SimpleB> ToSimple(IQueryable<QueryableB> query) => query.Select(item => new SimpleB());

            // A method with a matching parameter type, but a different name, to verify that it is not resolved.
            public static IQueryable<SimpleA> OtherMethod(IQueryable<QueryableWithoutToSimple> query) => query.Select(item => new SimpleA());
        }

        private static class AlternativeTestQueryExtensions
        {
            public static IQueryable<SimpleB> ToSimple(IQueryable<QueryableA> query) => query.Select(item => new SimpleB());
        }

        #endregion
    }
}
