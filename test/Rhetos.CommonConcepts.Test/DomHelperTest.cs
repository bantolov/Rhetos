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

namespace Rhetos.CommonConcepts.Test
{
    [TestClass]
    public class DomHelperTest
    {
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
