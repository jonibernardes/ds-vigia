using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DSVigia.Nucleo
{
    /// <summary>
    /// Limpa o nome que o navegador expõe para cada guia antes de casar com as regras. O Chrome em português
    /// põe o texto do cartão de memória ("Uso da memória em WhatsApp Business (fixada): 292 MB") e o título da
    /// janela é o único lugar com o número da guia ativa ("(1) WhatsApp Business - Google Chrome").
    /// </summary>
    public static class GuiasNav
    {
        static readonly Regex Prefixo = new Regex(@"^\s*(?:uso\s+d[ae]\s+mem[oó]ria\s+(?:em|-|:)\s*|memory\s+usage\s+(?:in|-|:)\s*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex SufixoMem = new Regex(@"\s*(?::\s*[\d.,]+\s*(?:KB|MB|GB)?|\s+-\s+(?:uso\s+d[ae]\s+mem[oó]ria|memory\s+usage)\b.*)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex Marcador = new Regex(@"\s*\((?:fixad[ao]|pinned)\)\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex Navegador = new Regex(@"\s+[-\u2014]\s+(?:Google Chrome|Chromium|(?:[^-\u2014]+\s+[-\u2014]\s+)?Microsoft\W*Edge)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex MaisPaginas = new Regex(@"\s+(?:e mais \d+ p[aá]ginas?|and \d+ more pages?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex Numero = new Regex(@"^\(\d{1,4}\+?\)\s*");

        /// <summary>"Uso da memória em WhatsApp Business (fixada): 292 MB" → "WhatsApp Business".</summary>
        public static string Normalizar(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "";
            var s = nome.Replace(' ', ' ').Trim();
            s = Prefixo.Replace(s, "");
            s = SufixoMem.Replace(s, "");
            s = Marcador.Replace(s, " ");
            s = Regex.Replace(s, @"\s{2,}", " ").Trim();
            // cartão cortado no meio ("Nova sessão - Claude (") : tira o parêntese aberto do fim
            s = Regex.Replace(s, @"\s*\($", "").Trim();
            return s;
        }

        /// <summary>Título da janela sem o nome do navegador: "(1) WhatsApp Business - Google Chrome" → "(1) WhatsApp Business".</summary>
        public static string TituloSemNavegador(string titulo)
        {
            if (string.IsNullOrWhiteSpace(titulo)) return "";
            var t = Navegador.Replace(titulo.Replace("\u200B", "").Trim(), "").Trim();
            return MaisPaginas.Replace(t, "").Trim();
        }

        /// <summary>
        /// Guias limpas + o título da janela, sem contar a guia ativa duas vezes: quando o título e uma guia são a
        /// mesma página (iguais tirando o "(n)"), fica uma só, a que tem o número.
        /// </summary>
        public static List<string> Juntar(IEnumerable<string> guias, string tituloJanela)
        {
            var r = new List<string>();
            foreach (var g in guias ?? Enumerable.Empty<string>())
            {
                var n = Normalizar(g);
                if (n != "") r.Add(n);
            }
            var t = Normalizar(TituloSemNavegador(tituloJanela));
            if (t == "") return r;
            var base_ = SemNumero(t);
            var i = r.FindIndex(x => string.Equals(SemNumero(x), base_, StringComparison.CurrentCultureIgnoreCase));
            if (i < 0) r.Add(t);
            else if (Numero.IsMatch(t) && !Numero.IsMatch(r[i])) r[i] = t;
            return r;
        }

        static string SemNumero(string s) => Numero.Replace(s ?? "", "").Trim();

        /// <summary>--user-data-dir da linha de comando do processo. Sem a opção: null (é a pasta padrão).</summary>
        public static string PastaDaLinha(string linha)
        {
            if (string.IsNullOrEmpty(linha)) return null;
            var m = Regex.Match(linha, "--user-data-dir=(?:\"([^\"]+)\"|(\\S+))", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            var v = (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Trim().TrimEnd('\\', '/');
            return v == "" ? null : v;
        }

        /// <summary>
        /// Chave do perfil: só a pasta do perfil ("Profile 1") na pasta padrão do navegador; com outra pasta de
        /// dados, "pasta|perfil", para dois Chrome separados não virarem o mesmo perfil.
        /// </summary>
        public static string ChavePerfil(string pastaDados, string pastaPadrao, string perfil)
        {
            if (string.IsNullOrEmpty(perfil)) return "";
            if (string.IsNullOrEmpty(pastaDados) || string.Equals(pastaDados.TrimEnd('\\', '/'), (pastaPadrao ?? "").TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                return perfil;
            return pastaDados.TrimEnd('\\', '/') + "|" + perfil;
        }
    }
}
