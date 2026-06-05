namespace Conductor.Core.Database
{
    /// <summary>
    /// SQL dialect choices for request analytics query generation.
    /// </summary>
    public enum RequestAnalyticsSqlDialect
    {
        /// <summary>
        /// SQLite dialect.
        /// </summary>
        Sqlite,

        /// <summary>
        /// MySQL dialect.
        /// </summary>
        MySql,

        /// <summary>
        /// PostgreSQL dialect.
        /// </summary>
        PostgreSql,

        /// <summary>
        /// SQL Server dialect.
        /// </summary>
        SqlServer
    }
}
