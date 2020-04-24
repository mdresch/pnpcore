using PnP.Core.Model;
using PnP.Core.QueryModel.OData;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace PnP.Core.QueryModel.Query
{
    internal class DataModelQueryTranslator<TModel> : ExpressionVisitor
    {
        private ODataQuery<TModel> query = new ODataQuery<TModel>();
        private readonly List<List<ODataFilter>> filtersStack = new List<List<ODataFilter>>();

        internal DataModelQueryTranslator()
        {
            filtersStack.Add(query.Filters);
        }

        internal ODataQuery<TModel> Translate(Expression expression)
        {
            this.Visit(expression);
            return this.query;
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            if (m.Method.DeclaringType == typeof(Queryable) ||
                m.Method.DeclaringType == typeof(BaseDataModelExtensions))
            {
                switch (m.Method.Name)
                {
                    case "Load":
                        VisitLoad(m);
                        return m;
                    case "OrderBy":
                    case "ThenBy":
                        VisitOrderBy(m);
                        return m;
                    case "OrderByDescending":
                    case "ThenByDescending":
                        VisitOrderBy(m, ascending: false);
                        return m;
                    case "Where":
                        VisitWhere(m);
                        return m;
                    case "Take":
                        VisitTake(m);
                        return m;
                    case "Skip":
                        VisitSkip(m);
                        return m;
                    case "Include":
                        VisitInclude(m);
                        return m;
                }
            }
            throw new NotSupportedException(string.Format("The method '{0}' is not supported", m.Method.Name));
        }

        private void VisitLoad(MethodCallExpression m)
        {
            this.Visit(m.Arguments[0]);
            var propertySelector = m.Arguments[1] as UnaryExpression;
            if (propertySelector != null)
            {
                var lambda = propertySelector.Operand as LambdaExpression;
                if (lambda != null)
                {
                    switch (lambda.Body)
                    {
                        case UnaryExpression unary:
                            var unaryMember = unary.Operand as MemberExpression;
                            if (unaryMember != null)
                            {
                                this.query.Select.Add(unaryMember.Member.Name);
                            }
                            break;
                        case MemberExpression member:
                            this.query.Select.Add(member.Member.Name);
                            break;
                    }
                }
            }
        }

        private void VisitOrderBy(MethodCallExpression m, bool ascending = true)
        {
            this.Visit(m.Arguments[0]);
            var propertySelector = m.Arguments[1] as UnaryExpression;
            if (propertySelector != null)
            {
                var lambda = propertySelector.Operand as LambdaExpression;
                if (lambda != null)
                {
                    switch (lambda.Body)
                    {
                        case UnaryExpression unary:
                            var unaryMember = unary.Operand as MemberExpression;
                            if (unaryMember != null)
                            {
                                this.query.OrderBy.Add(new OrderByItem
                                {
                                    Field = unaryMember.Member.Name,
                                    Direction = ascending ? OrderByDirection.Asc : OrderByDirection.Desc
                                });
                            }
                            break;
                        case MemberExpression member:
                            this.query.OrderBy.Add(new OrderByItem
                            {
                                Field = member.Member.Name,
                                Direction = ascending ? OrderByDirection.Asc : OrderByDirection.Desc
                            });
                            break;
                    }
                }
            }
        }

        private void VisitWhere(MethodCallExpression m)
        {
            this.Visit(m.Arguments[0]);
            LambdaExpression lambda = (LambdaExpression)m.Arguments[1].StripQuotes();

            switch (lambda.Body)
            {
                case BinaryExpression binary:
                    this.AddFilter(binary);
                    break;

                case MethodCallExpression methodCall:
                    if (methodCall.Type != typeof(bool))
                    {
                        throw new NotSupportedException($"Expression {methodCall} is not valid because it must return a boolean result");
                    }

                    string methodField = GetFilterField(methodCall);
                    // Should never happen
                    if (methodField == null)
                    {
                        throw new NotSupportedException($"Expression {methodCall} is not valid");
                    }

                    this.AddFilterToStack(new FilterItem
                    {
                        Field = methodField,
                        Criteria = FilteringCriteria.Equal,
                        Value = true
                    });

                    break;
            }
        }

        private void VisitTake(MethodCallExpression m)
        {
            Visit(m.Arguments[0]);
            this.query.Top = GetConstantValue<int>(m.Arguments[1]);
        }

        private void VisitSkip(MethodCallExpression m)
        {
            Visit(m.Arguments[0]);
            this.query.Skip = GetConstantValue<int>(m.Arguments[1]);
        }

        private void VisitInclude(MethodCallExpression m)
        {
            this.Visit(m.Arguments[0]);
            var propertySelector = m.Arguments[1] as UnaryExpression;
            if (propertySelector != null)
            {
                var lambda = propertySelector.Operand as LambdaExpression;
                if (lambda != null)
                {
                    switch (lambda.Body)
                    {
                        case UnaryExpression unary:
                            var unaryMember = unary.Operand as MemberExpression;
                            if (unaryMember != null)
                            {
                                this.query.Expand.Add(unaryMember.Member.Name);
                            }
                            break;
                        case MemberExpression member:
                            this.query.Expand.Add(member.Member.Name);
                            break;
                    }
                }
            }
        }

        private void AddFilter(BinaryExpression expression)
        {
            // Create a new group
            var filtersGroup = new FiltersGroup();
            // Now filters must be added into the current group
            AddFilterToStack(filtersGroup);

            string filterField = GetFilterField(expression.Left);
            object filterValue = GetFilterValue(expression.Right);

            this.RemoveFilterStack();

            // If field and value are not null it means that they are primitive values
            if (filterField != null && filterValue != null)
            {
                this.AddFilterToStack(new FilterItem
                {
                    Field = filterField,
                    Criteria =
                        (FilteringCriteria)Enum.Parse(typeof(FilteringCriteria), expression.NodeType.ToString()),
                    Value = filterValue
                });
            }
            else
            {
                // A new FiltersGroup has been added
                FilteringConcatOperator concat;
                switch (expression.NodeType)
                {
                    case ExpressionType.AndAlso:
                    case ExpressionType.And:
                        concat = FilteringConcatOperator.AND;
                        break;
                    case ExpressionType.OrElse:
                    case ExpressionType.Or:
                        concat = FilteringConcatOperator.OR;
                        break;
                    default:
                        throw new NotSupportedException($"Node of type {expression.NodeType} of expression {expression} is not supported");
                }

                // Set the contact operators to the filters (should be two)
                foreach (ODataFilter filter in filtersGroup.Filters)
                {
                    filter.ConcatOperator = concat;
                }
            }
        }

        private string GetFilterField(Expression expression)
        {
            string memberName;
            string functionCall;

            switch (expression)
            {
                case BinaryExpression binary:
                    AddFilter(binary);
                    return null;
                case MemberExpression member:

                    // Get the target member name, if any
                    memberName = GetMemberName(member.Expression.StripQuotes(), false);

                    // Member is null, probably expression is a normal property access
                    if (memberName == null)
                    {
                        break;
                    }

                    // Try to get the OData function
                    if (FunctionMapping.TryMapMember(member.Member, memberName, Array.Empty<object>(), out functionCall))
                    {
                        return functionCall;
                    }

                    // Raise an error
                    var validMembers = FunctionMapping.SupportedMembers.Where(m => !(m is MethodInfo)).Select(m => $"{m.DeclaringType.Name}.{m.Name}");
                    var validMembersString = String.Join(", ", validMembers);
                    throw new NotSupportedException($"Expression {expression} is invalid. Only calls to members {validMembersString} are supported");

                case MethodCallExpression methodCall:
                    // Get the target member name, if any
                    memberName = GetMemberName(methodCall.Object.StripQuotes());

                    // Member is null, probably expression is a normal property access
                    if (memberName == null)
                    {
                        break;
                    }

                    // Convert all method call arguments to objects
                    object[] arguments = methodCall.Arguments.Select(p => p.GetConstantValue()).ToArray();

                    // Try to get the OData function
                    if (FunctionMapping.TryMapMember(methodCall.Method, memberName, arguments, out functionCall))
                    {
                        return functionCall;
                    }

                    // Raise an error
                    var validMethods = FunctionMapping.SupportedMembers.OfType<MethodInfo>().Select(m => $"{m.DeclaringType.Name}.{m.Name}");
                    var validMethodsString = String.Join(", ", validMethods);
                    throw new NotSupportedException($"Expression {expression} is invalid. Only calls to methods {validMethodsString} are supported");
            }

            // Try with default resolver
            return GetMemberName(expression.StripQuotes());
        }

        private void AddFilterToStack(ODataFilter filter)
        {
            // Add to the last filters group
            filtersStack.Last().Add(filter);

            if (filter is FiltersGroup fg)
            {
                // Add a new list to the end of the stack
                filtersStack.Add(fg.Filters);
            }
        }

        private void RemoveFilterStack()
        {
            if (filtersStack.Count > 1)
            {
                List<ODataFilter> filters = filtersStack[filtersStack.Count - 1];

                // No filters has been added thus is useless
                if (filters.Count == 0)
                {
                    // We can remove this filters groups from parent
                    filtersStack[filtersStack.Count - 2].RemoveAll(f => f is FiltersGroup fg && fg.Filters == filters);
                }
                // Remove latest filters group
                filtersStack.RemoveAt(filtersStack.Count - 1);
            }
        }

        private string GetMemberName(Expression expression, bool raiseError = true)
        {
            switch (expression)
            {
                case MemberExpression member:
                    return member.Member.Name;
                case MethodCallExpression methodCall:
                    // Support for Fields member
                    if (methodCall.Method.DeclaringType == typeof(TransientDictionary))
                    {
                        return GetConstantValue<string>(methodCall.Arguments[0]);
                    }

                    if (raiseError)
                    {
                        throw new NotSupportedException($"Expression {expression} is not supported. Only calls to {typeof(Dictionary<string, object>)} indexer are supported");
                    }

                    break;
            }
            if (raiseError)
            {
                throw new NotSupportedException($"Expression {expression} is not supported. Only {typeof(MemberExpression)} and {typeof(MethodCallExpression)} are supported");
            }

            return null;
        }

        private object GetFilterValue(Expression expression)
        {
            expression = expression.StripQuotes();
            if (expression is BinaryExpression binary)
            {
                AddFilter(binary);
                return null;
            }

            return expression.GetConstantValue();
        }

        private static T GetConstantValue<T>(Expression expression)
        {
            object value = expression.GetConstantValue();
            try
            {
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (FormatException fe)
            {
                throw new NotSupportedException($"Constant {expression} is of a non supported type ({typeof(T)})", fe);
            }
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            throw new NotSupportedException(
                string.Format(
                    "The unary operator '{0}' is not supported",
                    u.NodeType));
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            return c;
        }

    }
}