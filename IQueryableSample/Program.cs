using Dapper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace IQueryableSample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // 設置 EntityServerModeSource
            var connectionString = "your_connection_string_here";
            var connection = new SqlConnection();
            var provider = new SqlQueryProvider(connection);
            var users = new SqlQueryable<User>(provider);

            users.Where(u => u.Name == "Alice").ToList();
        }
    }
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }


    public class SqlQueryProvider : IQueryProvider
    {
        private readonly SqlConnection _connection;

        public SqlQueryProvider(SqlConnection connection)
        {
            _connection = connection;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetGenericArguments().First();
            try
            {
                var queryableType = typeof(SqlQueryable<>).MakeGenericType(elementType);
                return (IQueryable)Activator.CreateInstance(queryableType, this, expression);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Could not create queryable", e);
            }
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new SqlQueryable<TElement>(this, expression);
        }

        public object Execute(Expression expression)
        {
            return Execute<IEnumerable<object>>(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            var sql = TranslateExpressionToSql(expression);
            return _connection.Query<TResult>(sql).SingleOrDefault();
        }

        private string TranslateExpressionToSql(Expression expression)
        {
            // 簡單的 LINQ 表達式轉換為 SQL 查詢的邏輯
            if (expression is MethodCallExpression methodCall)
            {
                var methodName = methodCall.Method.Name;

                if (methodName == "Where")
                {
                    var lambda = (LambdaExpression)((UnaryExpression)methodCall.Arguments[1]).Operand;
                    var body = (BinaryExpression)lambda.Body;
                    var member = (MemberExpression)body.Left;
                    var constant = (ConstantExpression)body.Right;

                    return $"SELECT * FROM Users WHERE {member.Member.Name} LIKE '{constant.Value}%'";
                }
            }

            return "SELECT * FROM Users"; // 默認查詢
        }
    }

    public class SqlQueryable<T> : IQueryable<T>
    {
        private readonly SqlQueryProvider _provider;
        private readonly Expression _expression;

        public SqlQueryable(SqlQueryProvider provider)
        {
            _provider = provider;
            _expression = Expression.Constant(this);
        }

        public SqlQueryable(SqlQueryProvider provider, Expression expression)
        {
            _provider = provider;
            _expression = expression;
        }

        public Type ElementType => typeof(T);
        public Expression Expression => _expression;
        public IQueryProvider Provider => _provider;

        public IEnumerator<T> GetEnumerator()
        {
            return _provider.Execute<IEnumerable<T>>(_expression).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
