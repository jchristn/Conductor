namespace Conductor.Core.Enums
{
    using System;

    /// <summary>
    /// Database type enumeration.
    /// </summary>
    public enum DatabaseTypeEnum
    {
        /// <summary>
        /// SQLite database.
        /// </summary>
        Sqlite = 0,

        /// <summary>
        /// PostgreSQL database.
        /// </summary>
        PostgreSql = 1,

        /// <summary>
        /// Microsoft SQL Server database.
        /// </summary>
        SqlServer = 2,

        /// <summary>
        /// MySQL database.
        /// </summary>
        MySql = 3
    }
}
