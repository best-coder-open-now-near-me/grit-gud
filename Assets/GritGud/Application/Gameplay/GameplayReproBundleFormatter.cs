using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace GritGud.Application.Gameplay
{
    /// <summary>
    /// Produces a deterministic, dependency-free JSON artifact for bug reports,
    /// cross-machine inspection, and cold-storage repro capture. The hot replay
    /// path continues to consume the strongly typed semantic bundle.
    /// </summary>
    public static class GameplayReproBundleFormatter
    {
        public static string Format(GameplayReproBundle bundle)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            var text = new StringBuilder(16 * 1024);
            var visiting = new HashSet<object>(ReferenceComparer.Instance);
            text.Append('{');
            AppendName(text, "format");
            AppendString(text, "grit-gud-semantic-repro");
            text.Append(',');
            AppendName(text, "bundle");
            AppendValue(text, bundle, visiting, depth: 0);
            text.Append('}');
            return text.ToString();
        }

        internal static string FormatCanonicalValue(object value)
        {
            var text = new StringBuilder(4 * 1024);
            AppendValue(
                text,
                value,
                new HashSet<object>(ReferenceComparer.Instance),
                depth: 0);
            return text.ToString();
        }

        private static void AppendValue(
            StringBuilder text,
            object value,
            ISet<object> visiting,
            int depth)
        {
            if (depth > 128)
                throw new InvalidOperationException(
                    "Portable repro values exceed the supported nesting depth.");
            if (value == null)
            {
                text.Append("null");
                return;
            }

            Type type = value.GetType();
            if (value is string stringValue)
            {
                AppendString(text, stringValue);
                return;
            }
            if (value is char character)
            {
                AppendString(text, character.ToString());
                return;
            }
            if (value is bool boolean)
            {
                text.Append(boolean ? "true" : "false");
                return;
            }
            if (type.IsEnum)
            {
                AppendString(text, value.ToString());
                return;
            }
            if (value is float single)
            {
                GameplayNumericPolicy.RequireFinite(single, nameof(value));
                text.Append(GameplayNumericPolicy.FormatCanonical(single));
                return;
            }
            if (value is double number)
            {
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new InvalidOperationException(
                        "Portable repro values must be finite.");
                text.Append(number.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value is decimal decimalValue)
            {
                text.Append(decimalValue.ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (IsInteger(type))
            {
                text.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            bool trackReference = !type.IsValueType;
            if (trackReference && !visiting.Add(value))
                throw new InvalidOperationException(
                    $"Portable repro values contain a cycle at '{type.FullName}'.");
            try
            {
                if (value is IDictionary dictionary)
                {
                    AppendDictionary(text, dictionary, visiting, depth + 1);
                    return;
                }
                if (value is IEnumerable sequence)
                {
                    AppendSequence(text, sequence, visiting, depth + 1);
                    return;
                }
                AppendObject(text, value, visiting, depth + 1);
            }
            finally
            {
                if (trackReference) visiting.Remove(value);
            }
        }

        private static void AppendDictionary(
            StringBuilder text,
            IDictionary dictionary,
            ISet<object> visiting,
            int depth)
        {
            var entries = new List<DictionaryEntry>();
            foreach (DictionaryEntry entry in dictionary)
                entries.Add(entry);
            entries.Sort((left, right) => StringComparer.Ordinal.Compare(
                Convert.ToString(left.Key, CultureInfo.InvariantCulture),
                Convert.ToString(right.Key, CultureInfo.InvariantCulture)));
            text.Append('{');
            for (int index = 0; index < entries.Count; index++)
            {
                if (index > 0) text.Append(',');
                AppendName(
                    text,
                    Convert.ToString(
                        entries[index].Key,
                        CultureInfo.InvariantCulture));
                AppendValue(text, entries[index].Value, visiting, depth);
            }
            text.Append('}');
        }

        private static void AppendSequence(
            StringBuilder text,
            IEnumerable sequence,
            ISet<object> visiting,
            int depth)
        {
            text.Append('[');
            bool first = true;
            foreach (object item in sequence)
            {
                if (!first) text.Append(',');
                AppendValue(text, item, visiting, depth);
                first = false;
            }
            text.Append(']');
        }

        private static void AppendObject(
            StringBuilder text,
            object value,
            ISet<object> visiting,
            int depth)
        {
            Type type = value.GetType();
            PropertyInfo[] properties = type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(properties, (left, right) =>
                StringComparer.Ordinal.Compare(left.Name, right.Name));
            text.Append('{');
            AppendName(text, "$type");
            AppendString(text, type.FullName ?? type.Name);
            foreach (PropertyInfo property in properties)
            {
                if (!property.CanRead
                    || property.GetIndexParameters().Length != 0)
                    continue;
                object propertyValue;
                try
                {
                    propertyValue = property.GetValue(value, null);
                }
                catch (TargetInvocationException exception)
                {
                    throw new InvalidOperationException(
                        $"Portable repro property '{type.FullName}.{property.Name}' failed.",
                        exception.InnerException ?? exception);
                }
                text.Append(',');
                AppendName(text, property.Name);
                AppendValue(text, propertyValue, visiting, depth);
            }
            text.Append('}');
        }

        private static bool IsInteger(Type type) =>
            type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong);

        private static void AppendName(StringBuilder text, string name)
        {
            AppendString(text, name ?? string.Empty);
            text.Append(':');
        }

        private static void AppendString(StringBuilder text, string value)
        {
            text.Append('"');
            foreach (char character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\b': text.Append("\\b"); break;
                    case '\f': text.Append("\\f"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (character < ' ')
                        {
                            text.Append("\\u");
                            text.Append(((int)character).ToString(
                                "x4",
                                CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            text.Append(character);
                        }
                        break;
                }
            }
            text.Append('"');
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance =
                new ReferenceComparer();

            public new bool Equals(object left, object right) =>
                ReferenceEquals(left, right);

            public int GetHashCode(object value) =>
                RuntimeHelpers.GetHashCode(value);
        }
    }
}
