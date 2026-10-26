using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DSVigia.Nucleo
{
    /// <summary>Pedaços de resposta do servidor de e-mail (IMAP). Só número e data: o assunto nunca é pedido.</summary>
    public static class ImapTexto
    {
        /// <summary>"* SEARCH 4 7 9" → [4,7,9].</summary>
        public static List<int> Busca(IEnumerable<string> linhas)
        {
            var r = new List<int>();
            foreach (var l in linhas)
            {
                if (!l.StartsWith("* SEARCH", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var p in l.Substring(8).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    if (int.TryParse(p, out var n)) r.Add(n);
            }
            return r;
        }

        /// <summary>INTERNALDATE "24-Oct-2026 09:15:02 -0300" → UTC.</summary>
        public static DateTime? DataInterna(IEnumerable<string> linhas)
        {
            foreach (var l in linhas)
            {
                var m = Regex.Match(l, "INTERNALDATE \"([^\"]+)\"", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                var s = m.Groups[1].Value.Trim();
                if (DateTimeOffset.TryParseExact(s, new[] { "d-MMM-yyyy HH:mm:ss zzz", "dd-MMM-yyyy HH:mm:ss zzz" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    return d.UtcDateTime;
                var semFuso = Regex.Replace(s, @"\s([+-]\d\d)(\d\d)$", " $1:$2");
                if (DateTimeOffset.TryParseExact(semFuso, new[] { "d-MMM-yyyy HH:mm:ss zzz", "dd-MMM-yyyy HH:mm:ss zzz" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                    return d.UtcDateTime;
            }
            return null;
        }

        /// <summary>Usuário e senha entre aspas, com \ e " escapados, como o IMAP pede.</summary>
        public static string Aspas(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
