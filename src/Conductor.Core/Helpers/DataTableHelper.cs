namespace Conductor.Core.Helpers
{
    using System;
    using System.Data;

    /// <summary>
    /// Helper methods for extracting values from DataRow objects.
    /// </summary>
    public static class DataTableHelper
    {
        /// <summary>
        /// Optional callback invoked with a warning message when an enum column holds a value that is not defined by the
        /// target enum (for example an integer written by a newer version of Conductor). The default value is returned in
        /// that case. Null (the default) disables the warning. The callback may be invoked concurrently from multiple
        /// threads, so it must be thread safe.
        /// </summary>
        public static Action<string> UnknownEnumValueWarning { get; set; } = null;

        /// <summary>
        /// Get a string value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>String value or null.</returns>
        public static string GetStringValue(DataRow row, string columnName)
        {
            if (row == null) return null;
            if (String.IsNullOrEmpty(columnName)) return null;
            if (!row.Table.Columns.Contains(columnName)) return null;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return null;
            return row[columnName].ToString();
        }

        /// <summary>
        /// Get a boolean value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Boolean value or false.</returns>
        public static bool GetBooleanValue(DataRow row, string columnName)
        {
            if (row == null) return false;
            if (String.IsNullOrEmpty(columnName)) return false;
            if (!row.Table.Columns.Contains(columnName)) return false;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return false;

            object val = row[columnName];
            if (val is bool boolVal) return boolVal;
            if (val is int intVal) return intVal != 0;
            if (val is long longVal) return longVal != 0;

            string strVal = val.ToString();
            if (Boolean.TryParse(strVal, out bool result)) return result;
            if (strVal == "1") return true;
            if (strVal == "0") return false;

            return false;
        }

        /// <summary>
        /// Get an integer value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Integer value or 0.</returns>
        public static int GetIntValue(DataRow row, string columnName)
        {
            if (row == null) return 0;
            if (String.IsNullOrEmpty(columnName)) return 0;
            if (!row.Table.Columns.Contains(columnName)) return 0;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return 0;

            object val = row[columnName];
            if (val is int intVal) return intVal;
            if (val is long longVal) return (int)longVal;

            if (Int32.TryParse(val.ToString(), out int result)) return result;
            return 0;
        }

        /// <summary>
        /// Get a long value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Long value or 0.</returns>
        public static long GetLongValue(DataRow row, string columnName)
        {
            if (row == null) return 0;
            if (String.IsNullOrEmpty(columnName)) return 0;
            if (!row.Table.Columns.Contains(columnName)) return 0;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return 0;

            object val = row[columnName];
            if (val is long longVal) return longVal;
            if (val is int intVal) return intVal;

            if (Int64.TryParse(val.ToString(), out long result)) return result;
            return 0;
        }

        /// <summary>
        /// Get a nullable long value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Long value or null.</returns>
        public static long? GetNullableLongValue(DataRow row, string columnName)
        {
            if (row == null) return null;
            if (String.IsNullOrEmpty(columnName)) return null;
            if (!row.Table.Columns.Contains(columnName)) return null;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return null;

            object val = row[columnName];
            if (val is long longVal) return longVal;
            if (val is int intVal) return intVal;

            if (Int64.TryParse(val.ToString(), out long result)) return result;
            return null;
        }

        /// <summary>
        /// Get a decimal value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Decimal value or 0.</returns>
        public static decimal GetDecimalValue(DataRow row, string columnName)
        {
            if (row == null) return 0;
            if (String.IsNullOrEmpty(columnName)) return 0;
            if (!row.Table.Columns.Contains(columnName)) return 0;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return 0;

            object val = row[columnName];
            if (val is decimal decVal) return decVal;
            if (val is double dblVal) return (decimal)dblVal;
            if (val is float fltVal) return (decimal)fltVal;

            if (Decimal.TryParse(val.ToString(), out decimal result)) return result;
            return 0;
        }

        /// <summary>
        /// Get a nullable decimal value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Decimal value or null.</returns>
        public static decimal? GetNullableDecimalValue(DataRow row, string columnName)
        {
            if (row == null) return null;
            if (String.IsNullOrEmpty(columnName)) return null;
            if (!row.Table.Columns.Contains(columnName)) return null;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return null;

            object val = row[columnName];
            if (val is decimal decVal) return decVal;
            if (val is double dblVal) return (decimal)dblVal;
            if (val is float fltVal) return (decimal)fltVal;

            if (Decimal.TryParse(val.ToString(), out decimal result)) return result;
            return null;
        }

        /// <summary>
        /// Get a nullable integer value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>Integer value or null.</returns>
        public static int? GetNullableIntValue(DataRow row, string columnName)
        {
            if (row == null) return null;
            if (String.IsNullOrEmpty(columnName)) return null;
            if (!row.Table.Columns.Contains(columnName)) return null;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return null;

            object val = row[columnName];
            if (val is int intVal) return intVal;
            if (val is long longVal) return (int)longVal;

            if (Int32.TryParse(val.ToString(), out int result)) return result;
            return null;
        }

        /// <summary>
        /// Get a DateTime value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>DateTime value or DateTime.UtcNow.</returns>
        public static DateTime GetDateTimeValue(DataRow row, string columnName)
        {
            if (row == null) return DateTime.UtcNow;
            if (String.IsNullOrEmpty(columnName)) return DateTime.UtcNow;
            if (!row.Table.Columns.Contains(columnName)) return DateTime.UtcNow;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return DateTime.UtcNow;

            object val = row[columnName];
            if (val is DateTime dtVal) return dtVal;

            if (DateTime.TryParse(val.ToString(), out DateTime result)) return result;
            return DateTime.UtcNow;
        }

        /// <summary>
        /// Get a nullable DateTime value from a DataRow.
        /// </summary>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <returns>DateTime value or null.</returns>
        public static DateTime? GetNullableDateTimeValue(DataRow row, string columnName)
        {
            if (row == null) return null;
            if (String.IsNullOrEmpty(columnName)) return null;
            if (!row.Table.Columns.Contains(columnName)) return null;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return null;

            object val = row[columnName];
            if (val is DateTime dtVal) return dtVal;

            if (DateTime.TryParse(val.ToString(), out DateTime result)) return result;
            return null;
        }

        /// <summary>
        /// Get an enum value from a DataRow.
        /// </summary>
        /// <typeparam name="T">Enum type.</typeparam>
        /// <param name="row">DataRow.</param>
        /// <param name="columnName">Column name.</param>
        /// <param name="defaultValue">Default value if not found.</param>
        /// <returns>Enum value.</returns>
        public static T GetEnumValue<T>(DataRow row, string columnName, T defaultValue) where T : struct, Enum
        {
            if (row == null) return defaultValue;
            if (String.IsNullOrEmpty(columnName)) return defaultValue;
            if (!row.Table.Columns.Contains(columnName)) return defaultValue;
            if (row[columnName] == null || row[columnName] == DBNull.Value) return defaultValue;

            object val = row[columnName];
            string text = val.ToString();
            if (Int64.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long numeric))
            {
                if (numeric >= Int32.MinValue && numeric <= Int32.MaxValue)
                {
                    object candidate = Enum.ToObject(typeof(T), (int)numeric);
                    if (Enum.IsDefined(typeof(T), candidate)) return (T)candidate;
                }

                WarnUnknownEnumValue<T>(columnName, text, defaultValue);
                return defaultValue;
            }

            if (Enum.TryParse<T>(text, true, out T result) && Enum.IsDefined(typeof(T), result)) return result;
            WarnUnknownEnumValue<T>(columnName, text, defaultValue);
            return defaultValue;
        }

        private static void WarnUnknownEnumValue<T>(string columnName, string value, T defaultValue) where T : struct, Enum
        {
            Action<string> handler = UnknownEnumValueWarning;
            if (handler == null) return;

            try
            {
                handler("column '" + columnName + "' holds value '" + value + "', which is not defined by " + typeof(T).Name + "; using '" + defaultValue + "'");
            }
            catch (Exception)
            {
                // A failing warning callback must never break a database read.
            }
        }
    }
}
