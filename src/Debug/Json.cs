using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class Json
    {
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            Append(sb, value);
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    AppendString(sb, s);
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case float f:
                    AppendNumber(sb, f);
                    break;
                case double d:
                    AppendNumber(sb, d);
                    break;
                case int or long or byte or uint or ulong or short:
                    sb.Append(System.Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
                case Vector3 v:
                    sb.Append('[');
                    AppendNumber(sb, v.x);
                    sb.Append(',');
                    AppendNumber(sb, v.y);
                    sb.Append(',');
                    AppendNumber(sb, v.z);
                    sb.Append(']');
                    break;
                case IDictionary dictionary:
                    sb.Append('{');
                    bool first = true;
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        AppendString(sb, entry.Key.ToString());
                        sb.Append(':');
                        Append(sb, entry.Value);
                    }
                    sb.Append('}');
                    break;
                case IEnumerable list:
                    sb.Append('[');
                    bool firstItem = true;
                    foreach (object item in list)
                    {
                        if (!firstItem) sb.Append(',');
                        firstItem = false;
                        Append(sb, item);
                    }
                    sb.Append(']');
                    break;
                default:
                    AppendString(sb, value.ToString());
                    break;
            }
        }

        private static void AppendNumber(StringBuilder sb, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) sb.Append("null");
            else sb.Append(System.Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static void AppendString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
