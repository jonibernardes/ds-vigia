using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DSVigia.Nucleo
{
    /// <summary>Tira o número dos textos lidos. Nenhum texto sai daqui: só o número.</summary>
    public static class Contagem
    {
        static readonly Dictionary<string, Regex> cache = new Dictionary<string, Regex>();

        public static Regex Re(string padrao)
        {
            if (string.IsNullOrEmpty(padrao)) return null;
            lock (cache)
            {
                if (cache.TryGetValue(padrao, out var r)) return r;
                try { r = new Regex(padrao, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)); }
                catch (ArgumentException) { r = null; }
                cache[padrao] = r;
                return r;
            }
        }

        /// <summary>
        /// Procura o rótulo nos textos. Com grupo de número: vale o maior. Sem grupo: conta quantas vezes
        /// apareceu. Devolve null quando o rótulo não aparece em lugar nenhum (o programa não está na tela).
        /// </summary>
        public static int? PorRotulo(IEnumerable<string> textos, string padrao)
        {
            var re = Re(padrao);
            if (re == null) return null;
            bool achou = false;
            int maior = 0, vezes = 0;
            foreach (var bruto in textos)
            {
                if (string.IsNullOrEmpty(bruto)) continue;
                var t = Regex.Replace(bruto, @"\s+", " ").Trim();
                if (t.Length == 0 || t.Length > 160) continue;
                Match m;
                try { m = re.Match(t); } catch (RegexMatchTimeoutException) { continue; }
                if (!m.Success) continue;
                achou = true;
                vezes++;
                if (m.Groups.Count > 1 && int.TryParse(m.Groups[1].Value, out var n) && n > maior) maior = n;
            }
            if (!achou) return null;
            return re.GetGroupNumbers().Length > 1 ? maior : vezes;
        }

        /// <summary>"(3) WhatsApp" → 3 · "WhatsApp" → 0.</summary>
        public static int PeloTitulo(string titulo)
        {
            if (string.IsNullOrEmpty(titulo)) return 0;
            var m = Regex.Match(titulo, @"\((\d{1,4})\)");
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        /// <summary>
        /// O número do ícone que o programa mostra na barra de tarefas. O Windows guarda como
        /// &lt;badge value="5"/&gt;; "alert", "attention" e afins viram 1, "none" vira 0.
        /// </summary>
        public static int? DoBadge(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return null;
            var m = Regex.Match(xml, "value\\s*=\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            var v = m.Groups[1].Value.Trim().ToLowerInvariant();
            if (int.TryParse(v, out var n)) return Math.Max(0, n);
            if (v == "none" || v == "") return 0;
            return 1;
        }

        /// <summary>Junta as leituras das regras do mesmo canal: vale o maior; null se nenhuma achou o programa.</summary>
        public static int? Juntar(IEnumerable<int?> leituras)
        {
            int? r = null;
            foreach (var l in leituras)
                if (l.HasValue) r = r.HasValue ? Math.Max(r.Value, l.Value) : l.Value;
            return r;
        }

        /// <summary>FILETIME do banco de notificações do Windows → UTC.</summary>
        public static DateTime? DeFileTime(long ft)
        {
            if (ft <= 0) return null;
            try { return DateTime.FromFileTimeUtc(ft); } catch { return null; }
        }
    }
}
