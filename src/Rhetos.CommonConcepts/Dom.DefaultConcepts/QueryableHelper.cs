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
using System.Linq;

namespace Rhetos.Dom.DefaultConcepts
{
    /// <summary>
    /// Information on a single execution of an <see cref="InterpretedQueryable{T}"/> query,
    /// reported to <see cref="QueryableHelper.Telemetry"/> if it is set.
    /// </summary>
    public sealed class InterpretedQueryTelemetry
    {
        public InterpretedQueryTelemetry(string expressionShape, int sourceCount, TimeSpan overhead, bool interpreted)
        {
            ExpressionShape = expressionShape;
            SourceCount = sourceCount;
            Overhead = overhead;
            Interpreted = interpreted;
        }

        /// <summary>
        /// The executed query expression.
        /// </summary>
        public string ExpressionShape { get; }

        /// <summary>
        /// Number of records in the source collection when the query was executed.
        /// </summary>
        public int SourceCount { get; }

        /// <summary>
        /// The per-execution fixed cost of the query: the expression analysis and rewrite, and the interpreter setup
        /// (on the standard queryable fallback, only the rewrite). It excludes the deferred per-record enumeration
        /// of queries that return a sequence (and, on the fallback, the deferred expression compilation and execution).
        /// Queries that return a scalar value (e.g. <c>Count</c> or <c>First</c>) are measured completely.
        /// </summary>
        public TimeSpan Overhead { get; }

        /// <summary>
        /// True if the query was executed by the expression interpreter,
        /// false if it was executed by the standard <see cref="EnumerableQuery{T}"/> class.
        /// </summary>
        public bool Interpreted { get; }
    }

    /// <summary>
    /// Helper methods for optimizing in-memory queries.
    /// </summary>
    /// <remarks>
    /// The <b>empty queries created by the framework</b> are executed with the expression interpreter
    /// (see <see cref="EmptyInterpreted{T}"/> and <see cref="InterpretedQueryable{T}"/>):
    /// the generated <c>Common.OrmRepositoryBase.Filter(query, ids)</c> method for an empty list of IDs,
    /// and the deny-all row permissions filter in <see cref="FilterExpression{T}.OptimizedWhere"/>.
    /// </remarks>
    public static class QueryableHelper
    {
        /// <summary>
        /// Optional diagnostics callback, called on each execution of an <see cref="InterpretedQueryable{T}"/> query.
        /// It is null by default, and it should be left null in production, since it is called on each query execution.
        /// The callback may be invoked concurrently from multiple threads, so it must be thread-safe
        /// (for example, collect the records into a concurrent collection, or use locking).
        /// </summary>
        public static Action<InterpretedQueryTelemetry> Telemetry { get; set; }

        /// <summary>
        /// Returns an empty query that is executed by the expression interpreter, instead of compiling the query's
        /// expression tree to IL code on each execution. It is intended to be used by the framework and the generated
        /// code, instead of <c>Array.Empty&lt;T&gt;().AsQueryable()</c>.
        /// </summary>
        /// <remarks>
        /// With zero records in the source, the interpreted execution returns exactly the same result as the standard
        /// <see cref="EnumerableQuery{T}"/> and is strictly cheaper, because the expression compilation cost is paid
        /// per query execution, not per record. Query expressions that cannot be interpreted still fall back
        /// to the standard behavior, see <see cref="InterpretedQueryable{T}"/>.
        /// <para>
        /// A single instance is cached for each element type: the returned query has no observable mutable state
        /// (its only mutable state is an internal lazily initialized cache, which is thread-safe), it reads the source
        /// collection on each execution, and <c>Array.Empty&lt;T&gt;()</c> is a shared singleton instance.
        /// Composing additional query operators over the returned instance creates new instances,
        /// it does not modify the cached one.
        /// </para>
        /// </remarks>
        public static IQueryable<T> EmptyInterpreted<T>() => EmptyInterpretedQuery<T>.Instance;

        private static class EmptyInterpretedQuery<T>
        {
            /// <summary>
            /// The threshold is 1, so that the empty source (0 records) is always below the threshold,
            /// and the query is always interpreted.
            /// </summary>
            public static readonly InterpretedQueryable<T> Instance = new InterpretedQueryable<T>(Array.Empty<T>(), 1);
        }
    }
}
