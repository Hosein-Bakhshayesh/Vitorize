using System.Globalization;
using System.Text;

namespace Vitorize.Web.Helpers
{
    /// <summary>
    /// Small helper for building client-side CSV exports (paired with the
    /// <c>vzAdmin.downloadText</c> JS helper). No backend endpoint involved.
    /// </summary>
    public static class AdminCsv
    {
        /// <summary>Escapes a single field for CSV output.</summary>
        public static string Field(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        /// <summary>Formats a decimal with an invariant culture (dot separator, no grouping).</summary>
        public static string Num(decimal value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>Builds a CSV document from a header row and a set of already-escaped/plain cells.</summary>
        public static string Build(IEnumerable<string> header, IEnumerable<IEnumerable<string>> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", header.Select(Field)));
            foreach (var row in rows)
                sb.AppendLine(string.Join(",", row));
            return sb.ToString();
        }
    }
}
