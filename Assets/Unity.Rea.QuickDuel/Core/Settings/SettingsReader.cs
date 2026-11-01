using System;
using System.Collections.Generic;
using System.Globalization;

namespace Unity.Rea.QuickDuel.Core
{
    // Reads one JSON object of a data file and validates it as it goes. Every key must be present, of the
    // right type and in range; a key nobody asked for is an error too (a typo would otherwise be silently
    // ignored). Keys that start with an underscore are notes for people ("_source") and are skipped.
    // Problems are collected, not thrown, so one load reports all of them, each as "file: key ... (line n)".
    // A value that fails gives back a harmless fallback so reading can go on.
    public sealed class SettingsReader
    {
        private readonly JsonValue obj;
        private readonly string path;
        private readonly List<string> errors;
        private readonly HashSet<string> read = new HashSet<string>();

        internal SettingsReader(JsonValue value, string path, List<string> errors)
        {
            obj = value;
            this.path = path;
            this.errors = errors;
        }

        // The nested object key ("duel").
        public SettingsReader Section(string key)
        {
            var value = Take(key, JsonKind.Object, "an object");
            return new SettingsReader(value, $"{path}.{key}", errors);
        }

        // Every member of this object as a nested object, in document order (the named profiles). A member
        // that is not an object is reported.
        public List<KeyValuePair<string, SettingsReader>> Sections()
        {
            var sections = new List<KeyValuePair<string, SettingsReader>>();
            if (obj == null)
                return sections;
            foreach (var member in obj.Members)
            {
                if (member.Key.StartsWith("_", StringComparison.Ordinal))
                    continue;
                read.Add(member.Key);
                if (member.Value.Kind != JsonKind.Object)
                {
                    Fail(member.Key, member.Value, $"must be an object (is {Describe(member.Value)})");
                    continue;
                }
                sections.Add(new KeyValuePair<string, SettingsReader>(
                    member.Key, new SettingsReader(member.Value, $"{path}.{member.Key}", errors)));
            }
            return sections;
        }

        // A finite number in [min, max].
        public double Number(string key, double min = double.MinValue, double max = double.MaxValue)
        {
            var value = Take(key, JsonKind.Number, "a number");
            if (value == null)
                return Fallback(min, max);

            var number = value.Number;
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                Fail(key, value, $"must be a finite number (is {Show(number)})");
                return Fallback(min, max);
            }
            if (number < min || number > max)
            {
                Fail(key, value, $"must be {Range(min, max)} (is {Show(number)})");
                return Math.Min(Math.Max(number, min), max);
            }
            return number;
        }

        // A whole number in [min, max].
        public int Integer(string key, int min = int.MinValue, int max = int.MaxValue)
        {
            var value = Take(key, JsonKind.Number, "a whole number");
            if (value == null)
                return (int)Fallback(min, max);

            var number = value.Number;
            if (Math.Floor(number) != number || number < int.MinValue || number > int.MaxValue)
            {
                Fail(key, value, $"must be a whole number (is {Show(number)})");
                return (int)Fallback(min, max);
            }
            if (number < min || number > max)
            {
                Fail(key, value, $"must be {Range(min, max)} (is {Show(number)})");
                return (int)Math.Min(Math.Max(number, min), max);
            }
            return (int)number;
        }

        public string Text(string key)
        {
            var value = Take(key, JsonKind.String, "text");
            if (value == null)
                return string.Empty;
            if (value.Text.Length == 0)
                Fail(key, value, "must not be empty");
            return value.Text;
        }

        // A non-empty array of non-empty strings.
        public string[] Texts(string key)
        {
            var value = Take(key, JsonKind.Array, "a list of text");
            if (value == null)
                return new string[0];
            if (value.Items.Count == 0)
                Fail(key, value, "must hold at least one line");

            var items = new List<string>();
            foreach (var item in value.Items)
            {
                if (item.Kind != JsonKind.String)
                    errors.Add($"{path}: '{key}' must hold only text (has {Describe(item)}) (line {item.Line})");
                else if (item.Text.Length == 0)
                    errors.Add($"{path}: '{key}' must not hold an empty line (line {item.Line})");
                else
                    items.Add(item.Text);
            }
            return items.ToArray();
        }

        // A cross-field check: records message against key when ok is false.
        public void Check(bool ok, string key, string message)
        {
            if (!ok)
                Fail(key, obj?.Get(key), message);
        }

        // Reports every key of this object that nothing read. Call once its keys have all been read.
        public void Done()
        {
            if (obj == null)
                return;
            foreach (var member in obj.Members)
            {
                if (!read.Contains(member.Key) && !member.Key.StartsWith("_", StringComparison.Ordinal))
                    errors.Add($"{path}: unknown key '{member.Key}' (line {member.Value.Line}); check its spelling");
            }
        }

        private JsonValue Take(string key, JsonKind kind, string what)
        {
            read.Add(key);
            if (obj == null)
                return null; // the enclosing object is missing, already reported

            var value = obj.Get(key);
            if (value == null)
            {
                errors.Add($"{path}: '{key}' is missing (expected {what})");
                return null;
            }
            if (value.Kind != kind)
            {
                Fail(key, value, $"must be {what} (is {Describe(value)})");
                return null;
            }
            return value;
        }

        private void Fail(string key, JsonValue value, string message)
        {
            var where = value != null ? $" (line {value.Line})" : string.Empty;
            errors.Add($"{path}: '{key}' {message}{where}");
        }

        private static double Fallback(double min, double max) => min > 0 ? min : Math.Min(0, max);

        private static string Describe(JsonValue value)
        {
            switch (value.Kind)
            {
                case JsonKind.Null: return "null";
                case JsonKind.Bool: return value.Bool ? "true" : "false";
                case JsonKind.Number: return Show(value.Number);
                case JsonKind.String: return $"\"{value.Text}\"";
                case JsonKind.Array: return "an array";
                default: return "an object";
            }
        }

        private static string Range(double min, double max)
        {
            var hasMin = min > double.MinValue;
            var hasMax = max < double.MaxValue;
            if (hasMin && hasMax)
                return $"between {Show(min)} and {Show(max)}";
            return hasMin ? $"at least {Show(min)}" : $"at most {Show(max)}";
        }

        private static string Show(double number) => number.ToString("G6", CultureInfo.InvariantCulture);
    }
}
