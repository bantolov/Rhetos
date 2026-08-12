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
    /// <summary>
    /// Tests for <see cref="QueryableHelper.EmptyInterpreted{T}"/>, the interpreted empty query
    /// that the framework uses instead of <c>Array.Empty&lt;T&gt;().AsQueryable()</c>.
    /// </summary>
    [TestClass]
    public class QueryableHelperTest
    {
        //=========================================================================
        #region Test data

        public class SimpleEntity : IEntity
        {
            public Guid ID { get; set; }
            public string Name { get; set; }

            public override string ToString() => Name ?? "<null>";
        }

        #endregion
        //=========================================================================
        #region Empty interpreted query

        [TestMethod]
        public void EmptyInterpretedIsInterpretedQueryable()
        {
            var empty = QueryableHelper.EmptyInterpreted<SimpleEntity>();

            Assert.IsTrue(empty is InterpretedQueryable<SimpleEntity>, $"Unexpected result type {empty.GetType()}.");
            Assert.AreEqual("", TestUtility.Dump(empty.ToList()));
            Assert.AreEqual(0, empty.Count());
        }

        [TestMethod]
        public void EmptyInterpretedComposedQueryIsInterpreted()
        {
            var records = new List<InterpretedQueryTelemetry>();
            List<string> result;

            QueryableHelper.Telemetry = records.Add;
            try
            {
                // Same query shape as the generated Save method's LoadOldItems code.
                result = QueryableHelper.EmptyInterpreted<SimpleEntity>()
                    .Select(item => new { item.ID, item.Name })
                    .ToList()
                    .Select(item => item.Name)
                    .ToList();
            }
            finally
            {
                QueryableHelper.Telemetry = null;
            }

            Assert.AreEqual("", TestUtility.Dump(result));
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual(0, records[0].SourceCount);
            Assert.IsTrue(records[0].Interpreted, "The empty query must not fall back to the standard queryable.");
        }

        [TestMethod]
        public void EmptyInterpretedInstanceIsCachedPerElementType()
        {
            Assert.AreSame(QueryableHelper.EmptyInterpreted<SimpleEntity>(), QueryableHelper.EmptyInterpreted<SimpleEntity>());
            Assert.AreNotSame(
                (object)QueryableHelper.EmptyInterpreted<SimpleEntity>(),
                (object)QueryableHelper.EmptyInterpreted<string>());
        }

        [TestMethod]
        public void EmptyInterpretedCompositionDoesNotModifyCachedInstance()
        {
            var empty = QueryableHelper.EmptyInterpreted<SimpleEntity>();

            var composed = empty.Where(item => item.Name == "a1");

            Assert.AreNotSame(empty, composed);
            Assert.AreSame(empty, QueryableHelper.EmptyInterpreted<SimpleEntity>());
            Assert.AreEqual("", TestUtility.Dump(composed.ToList()));
            Assert.AreEqual("", TestUtility.Dump(QueryableHelper.EmptyInterpreted<SimpleEntity>().ToList()));
        }

        #endregion
    }
}
