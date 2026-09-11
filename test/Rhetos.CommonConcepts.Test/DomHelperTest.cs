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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rhetos.CommonConcepts.Test
{
    [TestClass]
    public class DomHelperTest
    {
        [TestMethod]
        public void EmptyQueryableUsesOneEnumerableQueryPerElementType()
        {
            var query = DomHelper.EmptyQueryable<string>();

            Assert.AreSame(query, DomHelper.EmptyQueryable<string>());
            Assert.AreNotSame(query, DomHelper.EmptyQueryable<object>());
            Assert.IsInstanceOfType<EnumerableQuery<string>>(query);
            Assert.IsTrue(DomHelper.IsKnownEmpty(query));
            IEnumerable<string> enumerable = query;
            Assert.IsTrue(DomHelper.IsKnownEmpty(enumerable));
            Assert.AreEqual(0, enumerable.ToArray().Length);
        }

        [TestMethod]
        public void IsKnownEmptyDoesNotInspectOrEnumerateOtherSources()
        {
            Assert.IsFalse(DomHelper.IsKnownEmpty(Array.Empty<string>()));
            Assert.IsFalse(DomHelper.IsKnownEmpty(Array.Empty<string>().AsQueryable()));
            Assert.IsFalse(DomHelper.IsKnownEmpty(new[] { "item" }.AsQueryable()));
            Assert.IsFalse(DomHelper.IsKnownEmpty(ThrowingSource()));
            Assert.IsFalse(DomHelper.IsKnownEmpty(ThrowingSource().AsQueryable()));
            Assert.IsFalse(DomHelper.IsKnownEmpty<string>(null));
        }

        [TestMethod]
        public void IsKnownEmptyDoesNotRecognizeComposedQueries()
        {
            var empty = DomHelper.EmptyQueryable<string>();
            var filtered = empty.Where(item => item.Length > 0);
            var withDefault = empty.DefaultIfEmpty("default");
            var withAdditionalItem = empty.Concat(new[] { "added" });

            Assert.IsFalse(DomHelper.IsKnownEmpty(filtered));
            Assert.IsFalse(DomHelper.IsKnownEmpty(withDefault));
            Assert.IsFalse(DomHelper.IsKnownEmpty(withAdditionalItem));
            Assert.AreEqual("default", withDefault.Single());
            Assert.AreEqual("added", withAdditionalItem.Single());
            Assert.IsTrue(DomHelper.IsKnownEmpty(empty));
            Assert.AreEqual(0, empty.AsEnumerable().ToArray().Length);
        }

        [TestMethod]
        public void EmptyQueryableSupportsConcurrentDirectEnumeration()
        {
            var query = DomHelper.EmptyQueryable<Guid>();

            Parallel.For(0, 100, _ =>
            {
                Assert.AreSame(query, DomHelper.EmptyQueryable<Guid>());
                using var first = query.GetEnumerator();
                using var second = query.GetEnumerator();
                Assert.IsFalse(first.MoveNext());
                Assert.IsFalse(second.MoveNext());
            });
        }

        [TestMethod]
        public void ToListOrEmptyExecutesQueryWithItems()
        {
            var query = new[] { "b1", "a1", "a2" }.AsQueryable()
                .Where(name => name.StartsWith('a'))
                .Select(name => new { Name = name });

            var result = DomHelper.ToListOrEmpty(query, hasItems: true);

            Assert.AreEqual("a1, a2", TestUtility.Dump(result.Select(item => item.Name)));
        }

        [TestMethod]
        public void ToListOrEmptyDoesNotExecuteQueryWithoutItems()
        {
            // The generated Save method uses ToListOrEmpty with a query that is known in advance to return
            // no records. The query must not be executed in that case, to avoid the standard in-memory query
            // behavior that compiles the query expression to IL code on each execution.
            var query = ThrowingSource().AsQueryable().Select(name => new { Name = name });

            var result = DomHelper.ToListOrEmpty(query, hasItems: false);

            Assert.AreEqual(0, result.Count);
        }

        private static IEnumerable<string> ThrowingSource()
        {
            throw new InvalidOperationException("The query source must not be enumerated.");
#pragma warning disable CS0162 // Unreachable code. The yield statement makes this method a lazy iterator, so the exception above is thrown on enumeration instead of on the method call.
            yield break;
#pragma warning restore CS0162
        }
    }
}
