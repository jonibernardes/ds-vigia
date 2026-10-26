using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DSVigia.Nucleo
{
    /// <summary>
    /// Um sinal do sinaleiro que a atendente configurou: "WhatsApp Cliente 1" = o WhatsApp Web aberto no
    /// perfil "Profile 1" do Chrome. Guarda só onde olhar; nenhum texto de conversa.
    /// </summary>
    public sealed class Farol
    {
        public string Id = "";
        public string Nome = "";
        public string Tipo = "navegador";  // navegador | app
        public string Navegador = "";      // chrome | msedge
        public string PerfilId = "";       // pasta do perfil: "Default", "Profile 1"
        public string PerfilNome = "";     // nome que a pessoa deu ao perfil no navegador
        public string RegraChave = "";     // whatsapp, gestta, npconnect, email, telegram...
        public string App = "";            // tipo app: o mesmo pedaço de id das regras de notificação (WhatsAppDesktop)

        public Farol Copia() => (Farol)MemberwiseClone();
        public override string ToString() => Nome + " (" + RegraChave + "/" + Navegador + "/" + PerfilId + ")";
    }

    /// <summary>Um título de guia visto numa janela de navegador, já com o perfil. Fica só na memória.</summary>
    public sealed class GuiaVista
    {
        public string Navegador = "";
        public string PerfilId = "";
        public string PerfilNome = "";
        public string Titulo = "";
    }

    /// <summary>Leitura de um app de computador por uma regra janela/notificação. App vazio = veio do título da janela.</summary>
    public sealed class LeituraApp
    {
        public string RegraChave = "";
        public string App = "";
        public int? Pendentes;
    }

    /// <summary>Um sistema que o vigia reconheceu aberto: uma linha da tela Configurar faróis. Só o nome do sistema.</summary>
    public sealed class Reconhecido
    {
        public string Tipo = "navegador";
        public string Navegador = "";
        public string PerfilId = "";
        public string PerfilNome = "";
        public string RegraChave = "";
        public string App = "";
        public string Sistema = "";
    }

    public static class Farois
    {
        public const int Max = 8;
        public const string Prefixo = "farol:";

        // ---------- arquivo ----------

        /// <summary>Lê o farois.json. Texto ruim vira lista vazia (o vigia volta ao automático), nunca exceção.</summary>
        public static List<Farol> Carregar(string json)
        {
            var r = new List<Farol>();
            if (string.IsNullOrWhiteSpace(json)) return r;
            try
            {
                var raiz = JsonMini.Ler(json);
                var lista = raiz as List<object>;
                if (lista == null && raiz is Dictionary<string, object> d && d.TryGetValue("farois", out var f)) lista = f as List<object>;
                if (lista == null) return r;
                foreach (var o in lista)
                {
                    if (!(o is Dictionary<string, object> x)) continue;
                    var farol = new Farol
                    {
                        Id = T(x, "Id"), Nome = T(x, "Nome").Trim(), Tipo = T(x, "Tipo") == "app" ? "app" : "navegador",
                        Navegador = T(x, "Navegador"), PerfilId = T(x, "PerfilId"), PerfilNome = T(x, "PerfilNome"),
                        RegraChave = T(x, "RegraChave"), App = T(x, "App"),
                    };
                    if (farol.Nome.Length == 0 || farol.RegraChave.Length == 0) continue;
                    if (farol.Id.Length == 0) farol.Id = Guid.NewGuid().ToString();
                    r.Add(farol);
                    if (r.Count == Max) break;
                }
            }
            catch { r.Clear(); }
            return r;
        }

        public static string Salvar(IEnumerable<Farol> farois)
        {
            var sb = new StringBuilder("{\n  \"versao\": 1,\n  \"farois\": [");
            bool primeiro = true;
            foreach (var f in (farois ?? Enumerable.Empty<Farol>()).Where(f => f != null).Take(Max))
            {
                sb.Append(primeiro ? "\n    {" : ",\n    {"); primeiro = false;
                sb.Append("\"Id\": ").Append(JsonMini.Texto(f.Id))
                  .Append(", \"Nome\": ").Append(JsonMini.Texto(f.Nome))
                  .Append(", \"Tipo\": ").Append(JsonMini.Texto(f.Tipo))
                  .Append(", \"Navegador\": ").Append(JsonMini.Texto(f.Navegador))
                  .Append(", \"PerfilId\": ").Append(JsonMini.Texto(f.PerfilId))
                  .Append(", \"PerfilNome\": ").Append(JsonMini.Texto(f.PerfilNome))
                  .Append(", \"RegraChave\": ").Append(JsonMini.Texto(f.RegraChave))
                  .Append(", \"App\": ").Append(JsonMini.Texto(f.App)).Append('}');
            }
            sb.Append(primeiro ? "]\n}\n" : "\n  ]\n}\n");
            return sb.ToString();
        }

        static string T(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is string s ? s : "";

        // ---------- leitura ----------

        /// <summary>
        /// Uma leitura por farol, na ordem dos faróis (Chave "farol:"+Id, Nome do farol). Usa a mesma Regra
        /// do sistema, só que nas guias daquele navegador + perfil. Pendentes null = não está aberto.
        /// Sem faróis devolve null: o leitor segue no automático.
        /// </summary>
        public static List<LeituraCanal> Ler(IList<Farol> farois, IList<Regra> regras, IList<GuiaVista> guias, IList<LeituraApp> apps)
        {
            if (farois == null || farois.Count == 0) return null;
            guias = guias ?? new List<GuiaVista>();
            apps = apps ?? new List<LeituraApp>();
            var saida = new List<LeituraCanal>();
            foreach (var f in farois.Take(Max))
            {
                int? n;
                if (f.Tipo == "app")
                {
                    n = Contagem.Juntar(apps.Where(a => Igual(a.RegraChave, f.RegraChave)
                        && (string.IsNullOrEmpty(f.App) || string.IsNullOrEmpty(a.App) || Igual(a.App, f.App))).Select(a => a.Pendentes));
                }
                else
                {
                    var titulos = string.IsNullOrEmpty(f.PerfilId) ? new List<string>()
                        : guias.Where(g => Igual(g.Navegador, f.Navegador) && Igual(g.PerfilId, f.PerfilId)).Select(g => g.Titulo).ToList();
                    n = Contagem.Juntar(regras.Where(r => r.Ativo && r.Metodo == "titulo-guia" && Igual(r.Chave, f.RegraChave) && Leituras.Casa(r.Processo, f.Navegador))
                        .OrderBy(r => r.Ordem).Select(r => Contagem.PorRotulo(titulos, r.Rotulo)));
                }
                saida.Add(new LeituraCanal { Chave = Prefixo + f.Id, Nome = f.Nome, Pendentes = n });
            }
            return saida;
        }

        /// <summary>Que sistemas aparecem em cada navegador + perfil, pelas regras de guia. Só o nome do sistema sai daqui.</summary>
        public static List<Reconhecido> Reconhecer(IList<Regra> regras, IList<GuiaVista> guias)
        {
            var r = new List<Reconhecido>();
            foreach (var grupo in guias.Where(g => !string.IsNullOrEmpty(g.PerfilId))
                         .GroupBy(g => g.Navegador.ToLowerInvariant() + "|" + g.PerfilId))
            {
                var g0 = grupo.First();
                var titulos = grupo.Select(g => g.Titulo).ToList();
                foreach (var rg in regras.Where(x => x.Ativo && x.Metodo == "titulo-guia" && Leituras.Casa(x.Processo, g0.Navegador)).OrderBy(x => x.Ordem))
                {
                    if (r.Any(x => x.Navegador == g0.Navegador && x.PerfilId == g0.PerfilId && x.RegraChave == rg.Chave)) continue;
                    if (Contagem.PorRotulo(titulos, rg.Rotulo) == null) continue;
                    r.Add(new Reconhecido { Navegador = g0.Navegador, PerfilId = g0.PerfilId, PerfilNome = g0.PerfilNome, RegraChave = rg.Chave, Sistema = NomeDoSistema(rg.Chave, rg.Nome, true) });
                }
            }
            return r;
        }

        /// <summary>Apps de computador abertos (WhatsApp, Telegram, Outlook): os que alguma regra janela/notificação achou.</summary>
        public static List<Reconhecido> ReconhecerApps(IList<Regra> regras, IList<LeituraApp> apps)
        {
            var r = new List<Reconhecido>();
            foreach (var chave in apps.Where(a => a.Pendentes.HasValue).Select(a => a.RegraChave).Distinct())
            {
                var notif = regras.Where(x => x.Ativo && x.Metodo == "notificacao" && x.Chave == chave && x.App != "").OrderBy(x => x.Ordem).FirstOrDefault();
                var nome = regras.Where(x => x.Chave == chave).Select(x => x.Nome).FirstOrDefault() ?? chave;
                r.Add(new Reconhecido { Tipo = "app", RegraChave = chave, App = notif?.App ?? "", Sistema = NomeDoSistema(chave, nome, false) });
            }
            return r;
        }

        public static string NomeDoSistema(string chave, string nomeDaRegra, bool web)
        {
            switch ((chave ?? "").ToLowerInvariant())
            {
                case "whatsapp": return web ? "WhatsApp Web" : "WhatsApp";
                case "telegram": return web ? "Telegram Web" : "Telegram";
                case "gestta": return "Gestta";
                case "npconnect": return "NP Connect";
                case "email": return web ? "E-mail" : "Outlook";
                default: return string.IsNullOrEmpty(nomeDaRegra) ? chave : nomeDaRegra;
            }
        }

        public static string NomeDoNavegador(string nav) =>
            Igual(nav, "msedge") ? "Edge" : Igual(nav, "chrome") ? "Chrome" : nav;

        // ---------- perfil da janela (as 3 formas, sem Windows) ----------

        /// <summary>
        /// (a) AppUserModelID da janela. O Chrome usa a base ("Chrome", "MSEdge") no perfil Default e acrescenta
        /// ".UserData.Profile1" nos outros (só letras, números e ponto). Devolve a pasta do perfil, ou null.
        /// </summary>
        public static string PerfilDoAppId(string aumid, IEnumerable<string> pastas)
        {
            if (string.IsNullOrWhiteSpace(aumid) || aumid.IndexOf("_crx_", StringComparison.OrdinalIgnoreCase) >= 0) return null;
            var i = aumid.LastIndexOf("UserData.", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return "Default";
            var limpo = aumid.Substring(i + 9);
            foreach (var p in pastas ?? Enumerable.Empty<string>())
                if (string.Equals(SoLetras(p), limpo, StringComparison.OrdinalIgnoreCase)) return p;
            var m = Regex.Match(limpo, @"^Profile(\d+)$", RegexOptions.IgnoreCase);
            if (m.Success) return "Profile " + m.Groups[1].Value;
            return limpo.Length > 0 ? limpo : null;
        }

        /// <summary>(b) Nome do botão de perfil (avatar) da barra: casa com o nome que o perfil tem no navegador.</summary>
        public static string PerfilPeloBotao(IEnumerable<string> botoes, IDictionary<string, List<string>> nomesPorPasta)
        {
            string melhor = null; int tam = 0;
            foreach (var b in botoes ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(b)) continue;
                foreach (var kv in nomesPorPasta)
                    foreach (var nome in kv.Value.Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        bool casa = string.Equals(b.Trim(), nome.Trim(), StringComparison.CurrentCultureIgnoreCase)
                                 || b.StartsWith(nome.Trim() + " ", StringComparison.CurrentCultureIgnoreCase)
                                 || b.StartsWith(nome.Trim() + ",", StringComparison.CurrentCultureIgnoreCase)
                                 || b.StartsWith(nome.Trim() + "\n", StringComparison.CurrentCultureIgnoreCase);
                        if (casa && nome.Length > tam) { melhor = kv.Key; tam = nome.Length; }
                    }
            }
            return melhor;
        }

        /// <summary>(c) --profile-directory da linha de comando do processo. Sem a opção: null.</summary>
        public static string PerfilDaLinha(string linha)
        {
            if (string.IsNullOrEmpty(linha)) return null;
            var m = Regex.Match(linha, "--profile-directory=(?:\"([^\"]+)\"|(\\S+))", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            return m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        }

        static string SoLetras(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s ?? "") if ((c < 128 && char.IsLetterOrDigit(c)) || c == '.') sb.Append(c);
            return sb.ToString();
        }

        static bool Igual(string a, string b) => string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>JSON mínimo (objeto, lista, texto, número, true/false/null) para não depender de pacote no núcleo.</summary>
    public static class JsonMini
    {
        public static object Ler(string s)
        {
            int i = 0;
            var v = Valor(s, ref i, 0);
            Espaco(s, ref i);
            if (i != s.Length) throw new FormatException("sobra texto");
            return v;
        }

        static object Valor(string s, ref int i, int nivel)
        {
            if (nivel > 32) throw new FormatException("fundo demais");
            Espaco(s, ref i);
            if (i >= s.Length) throw new FormatException("fim");
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++;
                Espaco(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    Espaco(s, ref i);
                    if (i >= s.Length || s[i] != '"') throw new FormatException("chave");
                    var k = Cadeia(s, ref i);
                    Espaco(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw new FormatException(":");
                    i++;
                    d[k] = Valor(s, ref i, nivel + 1);
                    Espaco(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; return d; }
                    throw new FormatException("}");
                }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++;
                Espaco(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Valor(s, ref i, nivel + 1));
                    Espaco(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; return l; }
                    throw new FormatException("]");
                }
            }
            if (c == '"') return Cadeia(s, ref i);
            if (Palavra(s, ref i, "true")) return true;
            if (Palavra(s, ref i, "false")) return false;
            if (Palavra(s, ref i, "null")) return null;
            int ini = i;
            while (i < s.Length && "+-.eE0123456789".IndexOf(s[i]) >= 0) i++;
            if (i == ini) throw new FormatException("valor");
            return double.Parse(s.Substring(ini, i - ini), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static bool Palavra(string s, ref int i, string p)
        {
            if (string.CompareOrdinal(s, i, p, 0, p.Length) != 0) return false;
            i += p.Length; return true;
        }

        static string Cadeia(string s, ref int i)
        {
            var sb = new StringBuilder(); i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("\\u");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); i += 4; break;
                    default: throw new FormatException("escape");
                }
            }
            throw new FormatException("texto sem fim");
        }

        static void Espaco(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        public static string Texto(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
