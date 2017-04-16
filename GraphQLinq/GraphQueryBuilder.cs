using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GraphQLinq
{
    class GraphQueryBuilder<T>
    {
        private const string QueryTemplate = @"query {0} {{ {1}: {2} {3} {{ {4} }}}}";
        internal const string ResultAlias = "result";

        public GraphQLQuery BuildQuery(GraphQuery<T> graphQuery, List<IncludeDetails> includes)
        {
            var selectClause = "";

            if (graphQuery.Selector != null)
            {
                var body = graphQuery.Selector.Body;

                var padding = new string(' ', 4);

                if (body.NodeType == ExpressionType.MemberAccess)
                {
                    var member = ((MemberExpression)body).Member;
                    selectClause = BuildMemberAccessSelectClause(body, selectClause, padding, member.Name);
                }

                if (body.NodeType == ExpressionType.New)
                {
                    var newExpression = (NewExpression)body;

                    var fields = new List<string>();

                    foreach (var argument in newExpression.Arguments.OfType<MemberExpression>())
                    {
                        var selectField = BuildMemberAccessSelectClause(argument, selectClause, padding, argument.Member.Name);
                        fields.Add(selectField);
                    }
                    selectClause = string.Join(Environment.NewLine, fields);
                }
            }
            else
            {
                selectClause = BuildSelectClauseForType(typeof(T), includes);
            }

            selectClause = Environment.NewLine + selectClause + Environment.NewLine;

            var passedArguments = graphQuery.Arguments.Where(pair => pair.Value != null).ToList();

            var queryParameters = passedArguments.Any() ? $"({string.Join(", ", passedArguments.Select(pair => $"{pair.Key}: ${pair.Key}"))})" : "";
            var queryParameterTypes = passedArguments.Any() ? $"({string.Join(", ", passedArguments.Select(pair => $"${pair.Key}: {pair.Value.GetType().ToGraphQlType()}"))})" : "";

            var queryVariables = passedArguments.ToDictionary(pair => pair.Key, pair => pair.Value);
            var graphQLQuery = string.Format(QueryTemplate, queryParameterTypes, ResultAlias, graphQuery.QueryName.ToLower(), queryParameters, selectClause);

            var dictionary = new Dictionary<string, object> { { "query", graphQLQuery }, { "variables", queryVariables } };

            var json = JsonConvert.SerializeObject(dictionary, new StringEnumConverter());

            return new GraphQLQuery(graphQLQuery, queryVariables, json);
        }

        private static string BuildMemberAccessSelectClause(Expression body, string selectClause, string padding, string alias)
        {
            if (body.NodeType == ExpressionType.MemberAccess)
            {
                var member = ((MemberExpression)body).Member as PropertyInfo;

                if (member != null)
                {
                    if (string.IsNullOrEmpty(selectClause))
                    {
                        selectClause = $"{padding}{alias}: {member.Name.ToCamelCase()}";

                        if (!member.PropertyType.GetTypeOrListType().IsPrimitiveOrString())
                        {
                            var fieldForProperty = BuildSelectClauseForType(member.PropertyType.GetTypeOrListType(), 3);
                            selectClause = $"{selectClause} {{{Environment.NewLine}{fieldForProperty}{Environment.NewLine}{padding}}}";
                        }
                    }
                    else
                    {
                        selectClause = $"{member.Name.ToCamelCase()} {{ {Environment.NewLine}{selectClause}}}";
                    }
                    return BuildMemberAccessSelectClause(((MemberExpression)body).Expression, selectClause, padding, "");
                }
                return selectClause;
            }
            return selectClause;
        }

        private static string BuildSelectClauseForType(Type targetType, int depth = 1)
        {
            var propertyInfos = targetType.GetProperties();

            var propertiesToInclude = propertyInfos.Where(info => !info.PropertyType.HasNestedProperties());

            var selectClause = string.Join(Environment.NewLine, propertiesToInclude.Select(info => new string(' ', depth * 2) + info.Name.ToCamelCase()));

            return selectClause;
        }

        private static string BuildSelectClauseForType(Type targetType, IEnumerable<IncludeDetails> includes)
        {
            var selectClause = BuildSelectClauseForType(targetType);

            foreach (var include in includes)
            {
                var fieldsFromInclude = BuildSelectClauseForInclude(targetType, include);
                selectClause = selectClause + Environment.NewLine + fieldsFromInclude;
            }

            return selectClause;
        }

        private static string BuildSelectClauseForInclude(Type targetType, IncludeDetails includeDetails, int depth = 1, int index = 0)
        {
            var include = includeDetails.Path;
            if (string.IsNullOrEmpty(include))
            {
                return BuildSelectClauseForType(targetType, depth);
            }
            var leftPadding = new string(' ', depth * 2);

            var dotIndex = include.IndexOf(".", StringComparison.InvariantCultureIgnoreCase);

            var currentIncludeName = dotIndex >= 0 ? include.Substring(0, dotIndex) : include;

            Type propertyType;
            var propertyInfo = targetType.GetProperty(currentIncludeName);

            var includeName = currentIncludeName.ToCamelCase();

            var includeMethodInfo = includeDetails.MethodIncludes[index].Method;
            var includeByMethod = currentIncludeName == includeMethodInfo.Name && propertyInfo.PropertyType == includeMethodInfo.ReturnType;

            if (includeByMethod)
            {
                var methodDetails = includeDetails.MethodIncludes[index];
                index++;

                propertyType = methodDetails.Method.ReturnType.GetTypeOrListType();

                var includeMethodParams = methodDetails.Parameters.Where(pair => pair.Value != null).ToList();
                includeName = methodDetails.Method.Name.ToCamelCase();

                if (includeMethodParams.Any())
                {
                    var includeParameters = string.Join(", ", includeMethodParams.Select(pair => pair.Key + ": $" + pair.Key + index));
                    includeName = $"{includeName}({includeParameters})";
                }
            }
            else
            {
                propertyType = propertyInfo.PropertyType.GetTypeOrListType();
            }

            if (propertyType.IsPrimitiveOrString())
            {
                return leftPadding + includeName;
            }

            var restOfTheInclude = new IncludeDetails(includeDetails.MethodIncludes) { Path = dotIndex >= 0 ? include.Substring(dotIndex + 1) : "" };

            var fieldsFromInclude = BuildSelectClauseForInclude(propertyType, restOfTheInclude, depth + 1, index);
            fieldsFromInclude = $"{leftPadding}{includeName} {{{Environment.NewLine}{fieldsFromInclude}{Environment.NewLine}{leftPadding}}}";
            return fieldsFromInclude;
        }
    }

    class GraphQLQuery
    {
        public GraphQLQuery(string query, IReadOnlyDictionary<string, object> variables, string fullQuery)
        {
            Query = query;
            Variables = variables;
            FullQuery = fullQuery;
        }

        public string Query { get; }
        public string FullQuery { get; }
        public IReadOnlyDictionary<string, object> Variables { get; }
    }
}