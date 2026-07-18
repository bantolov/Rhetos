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

using Rhetos.Persistence;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Rhetos.Dom.DefaultConcepts
{
    public static class EFExpression
    {
        /// <summary>
        /// Optimized alternative to LINQ operation: <c>query.Where(item => ids.Contains(memberSelector))</c>.
        /// The same optimization is applied by <see cref="OptimizeContains"/> method, with a different syntax.
        /// </summary>
        /// <remarks>
        /// Entity Framework 6.1.3 has performance issues with Contains method: it does not cache the compiled SQL
        /// if the LINQ query has Contains method, and the compilation can take significant amount on time on complex queries.
        /// </remarks>
        public static IQueryable<T> WhereContains<T>(this IQueryable<T> query, List<Guid> ids, Expression<Func<T, Guid>> memberSelector)
        {
            Expression<Func<List<Guid>>> idsLambda = () => ids;
            var idsContainsExpression = (Expression<Func<T, bool>>)Expression.Lambda(
                Expression.Call(
                    idsLambda.Body,
                    ListOfGuidContainsMethod,
                    memberSelector.Body),
                memberSelector.Parameters.Single());
            return query.Where(OptimizeContains(idsContainsExpression));
        }

        /// <summary>
        /// Optimizes LINQ expression <c>item => ids.Contains(memberSelector)</c>.
        /// The same optimization is applied by <see cref="WhereContains{T}(IQueryable{T}, List{Guid}, Expression{Func{T, Guid}})"/> method, with a different syntax.
        /// </summary>
        /// <remarks>
        /// Entity Framework 6.1.3 has performance issues with Contains method: it does not cache the compiled SQL
        /// if the LINQ query has Contains method, and the compilation can take significant amount on time on complex queries.
        /// </remarks>
        public static Expression<Func<T, bool>> OptimizeContains<T>(Expression<Func<T, bool>> expressionToOptimize)
        {
            return (Expression<Func<T, bool>>)new ReplaceContainsVisitor().Visit(expressionToOptimize);
        }

        /// <summary>
        /// Untyped version of <see cref="OptimizeContains{T}(Expression{Func{T, bool}})"/> method,
        /// for when it is not convenient the use the generic method.
        /// </summary>
        public static Expression OptimizeContains(Expression expressionToOptimize)
        {
            return new ReplaceContainsVisitor().Visit(expressionToOptimize);
        }

        static readonly MethodInfo ListOfGuidContainsMethod = typeof(List<Guid>).GetMethod("Contains");

        static readonly MethodInfo ListOfNullableGuidContainsMethod = typeof(List<Guid?>).GetMethod("Contains");

        static readonly MethodInfo EnumerableOfGuidContainsMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "Contains" && m.GetParameters().Length == 2).MakeGenericMethod(typeof(Guid));

        static readonly MethodInfo EnumerableOfNullableGuidContainsMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "Contains" && m.GetParameters().Length == 2).MakeGenericMethod(typeof(Guid?));

        // C# 14 "first-class spans" language feature binds guidArray.Contains(id) to MemoryExtensions.Contains method with an implicit cast
        // of the array to ReadOnlySpan, instead of the Enumerable.Contains method that was used by earlier C# versions.
        // A Guid array binds to the 2-parameter overload, while a nullable Guid array binds to the 3-parameter overload with IEqualityComparer
        // (Guid? does not satisfy the IEquatable<T> constraint on the 2-parameter overload).

        static readonly MethodInfo SpanOfGuidContainsMethod = typeof(MemoryExtensions).GetMethods()
            .Single(m => m.Name == "Contains" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2
                && m.GetParameters()[0].ParameterType.IsGenericType
                && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>))
            .MakeGenericMethod(typeof(Guid));

        static readonly MethodInfo SpanOfNullableGuidContainsMethod = typeof(MemoryExtensions).GetMethods()
            .Single(m => m.Name == "Contains" && m.IsGenericMethodDefinition && m.GetParameters().Length == 3
                && m.GetParameters()[0].ParameterType.IsGenericType
                && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>)
                && m.GetParameters()[2].ParameterType.IsGenericType
                && m.GetParameters()[2].ParameterType.GetGenericTypeDefinition() == typeof(IEqualityComparer<>))
            .MakeGenericMethod(typeof(Guid?));

        private sealed class ReplaceContainsVisitor : ExpressionVisitor
        {
            static readonly MethodInfo ContainsIdsMethod = typeof(EFExpression).GetMethod(
                nameof(ContainsIds),
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new Type[] { typeof(Guid), typeof(string) },
                null);

            static readonly MethodInfo ContainsIdNullableMethod = typeof(EFExpression).GetMethod(
                nameof(ContainsIds),
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new Type[] { typeof(Guid?), typeof(string) },
                null);

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Object != null && node.Object.NodeType == ExpressionType.MemberAccess && node.Object is MemberExpression memberExpression)
                {
                    FieldInfo innerField = memberExpression.Member as FieldInfo;
                    ConstantExpression ce = memberExpression.Expression as ConstantExpression;

                    if (innerField == null || ce == null)
                        return base.VisitMethodCall(node);

                    if (node.Method == ListOfGuidContainsMethod && innerField.GetValue(ce.Value) is List<Guid> listOfGuid)
                        return CreateOptimizeExpressionForListOfGuid(listOfGuid, node.Arguments[0]);

                    if (node.Method == ListOfNullableGuidContainsMethod && innerField.GetValue(ce.Value) is List<Guid?> listOfNullableGuid)
                        return CreateOptimizeExpressionForListOfNullableGuid(listOfNullableGuid, node.Arguments[0]);
                }
                else if (node.Object == null && node.Arguments.Count == 2 && node.Arguments[0].NodeType == ExpressionType.Constant)
                {
                    ConstantExpression ce = node.Arguments[0] as ConstantExpression;

                    if (ce == null)
                        return base.VisitMethodCall(node);

                    if (node.Method == EnumerableOfGuidContainsMethod && ce.Value is IList<Guid> listOfGuid)
                        return CreateOptimizeExpressionForListOfGuid(listOfGuid, node.Arguments[1]);

                    if (node.Method == EnumerableOfNullableGuidContainsMethod && ce.Value is IList<Guid?> listOfNullableGuid)
                        return CreateOptimizeExpressionForListOfNullableGuid(listOfNullableGuid, node.Arguments[1]);
                }
                else if (node.Object == null && node.Arguments.Count == 2 && node.Arguments[0].NodeType == ExpressionType.MemberAccess)
                {
                    FieldInfo innerField = (node.Arguments[0] as MemberExpression)?.Member as FieldInfo;
                    ConstantExpression ce = (node.Arguments[0] as MemberExpression)?.Expression as ConstantExpression;

                    if (innerField == null || ce == null)
                        return base.VisitMethodCall(node);

                    if (node.Method == EnumerableOfGuidContainsMethod && innerField.GetValue(ce.Value) is IList<Guid> listOfGuid)
                        return CreateOptimizeExpressionForListOfGuid(listOfGuid, node.Arguments[1]);

                    if (node.Method == EnumerableOfNullableGuidContainsMethod && innerField.GetValue(ce.Value) is IList<Guid?> listOfNullableGuid)
                        return CreateOptimizeExpressionForListOfNullableGuid(listOfNullableGuid, node.Arguments[1]);
                }
                // C# 14 "first-class spans" language feature binds guidArray.Contains(id) to MemoryExtensions.Contains method
                // (see SpanOfGuidContainsMethod above), which Entity Framework translates to SQL query with inline literals
                // instead of the parameterized ContainsIds function. The expression is normalized here back to the
                // Enumerable.Contains format, and then processed by the optimizations above.
                else if (node.Object == null && node.Method == SpanOfGuidContainsMethod
                    && GetArrayFromReadOnlySpanCast<Guid>(node.Arguments[0]) is Expression guidArray)
                {
                    return VisitMethodCall(Expression.Call(EnumerableOfGuidContainsMethod, guidArray, node.Arguments[1]));
                }
                else if (node.Object == null && node.Method == SpanOfNullableGuidContainsMethod
                    && node.Arguments[2] is ConstantExpression comparer && comparer.Value == null
                    && GetArrayFromReadOnlySpanCast<Guid?>(node.Arguments[0]) is Expression nullableGuidArray)
                {
                    return VisitMethodCall(Expression.Call(EnumerableOfNullableGuidContainsMethod, nullableGuidArray, node.Arguments[1]));
                }

                return base.VisitMethodCall(node);
            }

            /// <summary>
            /// If the given expression is an implicit cast of an array to ReadOnlySpan, generated by the C# 14 "first-class spans"
            /// language feature, returns the underlying array expression, otherwise null.
            /// </summary>
            static Expression GetArrayFromReadOnlySpanCast<TElement>(Expression expression)
            {
                if (expression is MethodCallExpression castCall
                    && castCall.Method.Name == "op_Implicit"
                    && castCall.Method.DeclaringType == typeof(ReadOnlySpan<TElement>)
                    && castCall.Arguments.Count == 1
                    && typeof(IEnumerable<TElement>).IsAssignableFrom(castCall.Arguments[0].Type))
                    return castCall.Arguments[0];

                // The compiler may alternatively generate the implicit cast as a Convert node with the same conversion operator.
                if (expression is UnaryExpression castUnary
                    && castUnary.Method?.Name == "op_Implicit"
                    && castUnary.Method.DeclaringType == typeof(ReadOnlySpan<TElement>)
                    && typeof(IEnumerable<TElement>).IsAssignableFrom(castUnary.Operand.Type))
                    return castUnary.Operand;

                return null;
            }

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
            Expression CreateOptimizeExpressionForListOfGuid(IList<Guid> guids, Expression value)
            {
                var concatenatedIds = string.Join(",", guids.Distinct().Select(x => x.ToString()));
                Expression<Func<string>> idsLambda = () => concatenatedIds;

                return Expression.Call(ContainsIdsMethod, value, idsLambda.Body);
            }
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

            Expression CreateOptimizeExpressionForListOfNullableGuid(IList<Guid?> guids, Expression value)
            {
                var concatenatedIds = string.Join(",", guids.Where(x => x != null).Distinct().Select(x => x.ToString()));
                Expression<Func<string>> idsLambda = () => concatenatedIds;

                Expression optimizedContainsExpression = Expression.Call(ContainsIdNullableMethod, value, idsLambda.Body);

                // EF would where add here "AND argument IS NOT NULL", if UseDatabaseNullSemantics=false,
                // but we have removed that condition because:
                // 1. it does not change the result in the database, and
                // 2. there is no clean way to use the system configuration here (from static methods WhereContains and OptimizeContains).

                if (guids.Any(x => x == null))
                    optimizedContainsExpression = Expression.Or(
                        optimizedContainsExpression,
                        Expression.Equal(value, Expression.Constant(null)));

                return optimizedContainsExpression;
            }
        }

        public const string ContainsIdsFunction = "InterceptContainsIds";

        [DbFunction(EntityFrameworkMapping.StorageModelNamespace, ContainsIdsFunction)]
        private static bool ContainsIds(Guid id, string guids)
        {
            return !string.IsNullOrEmpty(guids)
                && guids.Split(',').Select(guid => new Guid(guid)).Contains(id);
        }

        [DbFunction(EntityFrameworkMapping.StorageModelNamespace, ContainsIdsFunction)]
        private static bool ContainsIds(Guid? id, string guids)
        {
            return id != null
                && !string.IsNullOrEmpty(guids)
                && guids.Split(',').Select(guid => new Guid(guid)).Contains(id.Value);
        }
    }
}
