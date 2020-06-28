using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EFDatatable.Data
{
    /// <summary>
    /// Includes ToDataResult extension
    /// </summary>
    public static class DatatableExtensions
    {
        /// <summary>
        /// Executes IQueryable list and returns DataResult includes data
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="request"></param>
        /// <returns>DataResult</returns>
        public static DataResult<T> ToDataResult<T>(this IQueryable<T> query, DataRequest request) where T : class
        {
            var result = new DataResult<T>
            {
                draw = request.draw
            };
            result.recordsTotal = result.recordsFiltered = query.Count();

            foreach (var item in request.filters)
            {
                var exp = GetExpression<T>((Operand)item.Operand, item.Field, item.Value);
                if (exp != null) query = query.Where(exp);
            }

            if (!string.IsNullOrEmpty(request.search?.value))
            {
                Expression currentexp = null;
                ParameterExpression param = Expression.Parameter(typeof(T), "t");

                foreach (var item in request.columns.Where(a => a.searchable))
                {
                    MemberExpression member = Expression.Property(param, item.data);
                    var isString = member.Type == typeof(string);
                    var operand = isString ? Operand.Contains : Operand.Equal;

                    var words = request.search.value.Split(' ').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    if (isString && words.Count > 1 && item.data.Contains("Name"))
                    {
                        Expression exp = null;

                        foreach (var word in words)
                        {
                            var filter = new FilterDefinition { Operand = operand, Field = item.data, Value = word };

                            var expin = ExpressionBuilder.GetExpression<T>(param, filter);

                            if (exp == null)
                            {
                                exp = expin;
                            }
                            else
                            {
                                exp = Expression.And(exp, expin);
                            }
                        }

                        if (currentexp == null)
                        {
                            currentexp = exp;
                        }
                        else
                        {
                            currentexp = Expression.Or(currentexp, exp);
                        }
                    }
                    else
                    {
                        var filter = new FilterDefinition { Operand = operand, Field = item.data, Value = request.search.value };
                        var expin = ExpressionBuilder.GetExpression<T>(param, filter);
                        if (currentexp == null)
                        {
                            currentexp = expin;
                        }
                        else
                        {
                            currentexp = Expression.Or(currentexp, expin);
                        }
                    }
                }
                var exp2= Expression.Lambda<Func<T, bool>>(currentexp, param);

                if (exp2 != null) query = query.Where(exp2);
            }

            if (!string.IsNullOrEmpty(request.search?.value) || request.filters.Any())
            {
                result.recordsFiltered = query.Count();
            }

            if (request.draw > 0)
            {
                if (!request.order.Any())
                {
                    query = query.OrderBy(request.columns[0].data);
                }
                else
                {
                    foreach (var item in request.order)
                    {
                        if (item.dir == "asc")
                        {
                            query = query.OrderBy(request.columns[item.column].data);
                        }
                        else
                        {
                            query = query.OrderByDescending(request.columns[item.column].data);
                        }
                    }
                }
                query = query.Skip(request.start).Take(request.length);
            }

            result.data = query.ToList();
            return result;
        }

        private static Expression<Func<T, bool>> GetExpression<T>(Operand operand, string field, string value)
        {
            return ExpressionBuilder
                .GetExpression<T>(new FilterDefinition
                {
                    Operand = operand,
                    Field = field,
                    Value = value
                });
        }

        private static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> query, string memberName)
        {
            return OrderByCreate(query, memberName, "OrderBy");
        }

        private static IOrderedQueryable<T> OrderByDescending<T>(this IQueryable<T> query, string memberName)
        {
            return OrderByCreate(query, memberName, "OrderByDescending");
        }

        private static IOrderedQueryable<T> OrderByCreate<T>(this IQueryable<T> query, string memberName, string direction)
        {
            var typeParams = new ParameterExpression[] { Expression.Parameter(typeof(T), "") };
            var pi = typeof(T).GetProperty(memberName);
            return (IOrderedQueryable<T>)query.Provider.CreateQuery(
                Expression.Call(
                    typeof(Queryable),
                    direction,
                    new Type[] { typeof(T), pi.PropertyType },
                    query.Expression,
                    Expression.Lambda(Expression.Property(typeParams[0], pi), typeParams))
            );
        }
    }
}
