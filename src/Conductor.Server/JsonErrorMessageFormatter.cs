namespace Conductor.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Rewrites System.Text.Json conversion errors for enum-typed properties into messages a client can act on: the
    /// property name and the list of valid values, without internal .NET type names. All members are static, side-effect
    /// free and thread safe.
    /// </summary>
    public static class JsonErrorMessageFormatter
    {
        /// <summary>
        /// Build a client-facing message for a deserialization error.
        /// </summary>
        /// <param name="exception">The deserialization error. Null returns null.</param>
        /// <param name="rootType">The type being deserialized. Null returns null.</param>
        /// <returns>The rewritten message, or null when the error is not an invalid enum value (callers then keep the original).</returns>
        public static string Format(JsonException exception, Type rootType)
        {
            if (exception == null || rootType == null || String.IsNullOrEmpty(exception.Path)) return null;
            if (exception.Message == null || exception.Message.IndexOf("could not be converted", StringComparison.OrdinalIgnoreCase) < 0) return null;

            Type enumType = ResolvePathType(rootType, exception.Path);
            if (enumType == null) return null;
            enumType = Nullable.GetUnderlyingType(enumType) ?? enumType;
            if (!enumType.IsEnum) return null;

            string propertyName = exception.Path.Split('.').Last();
            int bracket = propertyName.IndexOf('[');
            if (bracket >= 0) propertyName = propertyName.Substring(0, bracket);
            return "The value of '" + propertyName + "' is not valid. Valid values: " + String.Join(", ", Enum.GetNames(enumType)) + ".";
        }

        private static Type ResolvePathType(Type rootType, string path)
        {
            // Paths look like $.ApiType or $.Settings.Mode or $.Items[0].Kind.
            string trimmed = path.StartsWith("$", StringComparison.Ordinal) ? path.Substring(1) : path;
            Type current = rootType;
            foreach (string rawSegment in trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                string segment = rawSegment;
                bool indexed = false;
                int bracket = segment.IndexOf('[');
                if (bracket >= 0)
                {
                    indexed = true;
                    segment = segment.Substring(0, bracket);
                }

                PropertyInfo property = FindProperty(current, segment);
                if (property == null) return null;
                current = property.PropertyType;
                if (indexed) current = GetElementType(current);
                if (current == null) return null;
            }

            return current == rootType ? null : current;
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                JsonPropertyNameAttribute attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
                string jsonName = attribute != null ? attribute.Name : property.Name;
                if (String.Equals(jsonName, name, StringComparison.OrdinalIgnoreCase)) return property;
            }

            return null;
        }

        private static Type GetElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            Type enumerable = type.GetInterfaces().Concat(new[] { type })
                .FirstOrDefault(item => item.IsGenericType && item.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumerable?.GetGenericArguments()[0];
        }
    }
}
