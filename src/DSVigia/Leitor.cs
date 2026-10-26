using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using DSVigia.Nucleo;

namespace DSVigia
{
    /// <summary>
    /// Lê, nos programas abertos, SÓ o número de conversas esperando em cada sistema. Usa o texto que o
    /// próprio Windows expõe por acessibilidade (títulos de guia e rótulos como "Pendentes 3") e o número
    /// do ícone na barra de tarefas. Nenhuma mensagem, nome ou telefone é lido ou enviado.
    /// </summary>
    public static class Leitor
    {
        /// <summary>Uma leitura de um canal, pronta para o Canal: só número e, quando dá, desde quando espera.</summary>
        public struct Leitura { public string Chave; public string Nome; public int? Pendentes; public DateTime? MaisAntiga; }

        public static List<string> UltimoDiag = new List<string>();

        public static List<Leitura> Ler(List<Regra> regras) => Ler(regras, null);

        /// <summary>Com faróis: só os faróis, 1 leitura por farol. Sem faróis: o automático de sempre.</summary>
        public static List<Leitura> Ler(List<Regra> regras, List<Farol> farois)
        {
            UltimoDiag = new List<string>();
            if (farois != null && farois.Count > 0) return LerFarois(regras, farois);
            var titulos = TitulosDeProcesso();          // processo -> títulos de janela
            foreach (var kv in titulos) foreach (var t in kv.Value) UltimoDiag.Add("janela " + kv.Key + " | " + t);
            var daArvore = TextosPorUia(regras);        // coletado uma vez por processo de navegador
            foreach (var kv in daArvore) UltimoDiag.Add("uia " + kv.Key + " guias=" + kv.Value.Guias.Count + " rotulos=" + kv.Value.Rotulos.Count);
            var diag = UltimoDiag;
            var guias = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in daArvore) guias[kv.Key] = kv.Value.Guias;
            var saida = Leituras.Automatico(regras, titulos, guias, Notificacoes.Badge, diag.Add)
                .Select(l => new Leitura { Chave = l.Chave, Nome = l.Nome, Pendentes = l.Pendentes }).ToList();

            // desde quando espera: só pelas notificações, que o Windows carimba. Título de janela não tem hora.
            for (int i = 0; i < saida.Count; i++)
            {
                var app = regras.FirstOrDefault(r => r.Chave == saida[i].Chave && r.Metodo == "notificacao" && r.App != "");
                if (app != null && saida[i].Pendentes > 0)
                {
                    var l = saida[i];
                    l.MaisAntiga = Notificacoes.MaisAntigaNaoLida(app.App);
                    saida[i] = l;
                }
            }
            return saida;
        }

        static List<Leitura> LerFarois(List<Regra> regras, List<Farol> farois)
        {
            var diag = UltimoDiag;
            var janelas = farois.Any(f => f.Tipo != "app") ? Perfis.Janelas() : new List<Perfis.Janela>();
            foreach (var j in janelas) diag.Add("navegador " + j.Navegador + " perfil=" + (j.PerfilId == "" ? "?" : j.PerfilId) + " via=" + j.Via + " guias=" + j.Guias.Count);
            var guias = janelas.SelectMany(j => j.Guias.Select(t => new GuiaVista { Navegador = j.Navegador, PerfilId = j.PerfilId, PerfilNome = j.PerfilNome, Titulo = t })).ToList();
            var apps = farois.Any(f => f.Tipo == "app") ? LerApps(regras) : new List<LeituraApp>();
            var lidas = Farois.Ler(farois, regras, guias, apps);

            var saida = new List<Leitura>();
            for (int i = 0; i < lidas.Count; i++)
            {
                var f = farois[i]; var l = lidas[i];
                var leitura = new Leitura { Chave = l.Chave, Nome = l.Nome, Pendentes = l.Pendentes };
                string via;
                if (f.Tipo == "app")
                {
                    via = "app";
                    if (leitura.Pendentes > 0 && f.App != "") leitura.MaisAntiga = Notificacoes.MaisAntigaNaoLida(f.App);
                }
                else
                {
                    var vias = janelas.Where(j => j.Navegador == f.Navegador && string.Equals(j.PerfilId, f.PerfilId, StringComparison.OrdinalIgnoreCase)).Select(j => j.Via).Distinct().ToList();
                    via = vias.Count == 0 ? "-" : string.Join("+", vias);
                }
                diag.Add("farol " + f.Nome + " perfil=" + (f.Tipo == "app" ? "-" : f.PerfilId) + " via=" + via + " -> " + (l.Pendentes.HasValue ? l.Pendentes.Value.ToString() : "null"));
                saida.Add(leitura);
            }
            return saida;
        }

        /// <summary>Apps de computador: o número de cada regra janela/notificação, sem juntar. Só número.</summary>
        public static List<LeituraApp> LerApps(List<Regra> regras)
        {
            var titulos = TitulosDeProcesso();
            var r = new List<LeituraApp>();
            foreach (var rg in regras.Where(x => x.Ativo).OrderBy(x => x.Ordem))
            {
                if (rg.Metodo == "janela")
                    r.Add(new LeituraApp { RegraChave = rg.Chave, App = "", Pendentes = Contagem.PorRotulo(titulos.Where(kv => Casa(rg.Processo, kv.Key)).SelectMany(kv => kv.Value), rg.Rotulo) });
                else if (rg.Metodo == "notificacao")
                    r.Add(new LeituraApp { RegraChave = rg.Chave, App = rg.App, Pendentes = Notificacoes.Badge(rg.App) });
            }
            return r;
        }

        static bool Casa(string padrao, string valor) => Leituras.Casa(padrao, valor);

        static Dictionary<string, List<string>> TitulosDeProcesso()
        {
            var d = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                string t;
                try { t = p.MainWindowTitle; } catch { continue; }
                if (string.IsNullOrEmpty(t)) continue;
                if (!d.TryGetValue(p.ProcessName, out var l)) { l = new List<string>(); d[p.ProcessName] = l; }
                l.Add(t);
            }
            return d;
        }

        public struct Arvore { public List<string> Guias; public List<string> Rotulos; }

        /// <summary>
        /// Varre a árvore de acessibilidade dos navegadores UMA vez por processo e separa os títulos de guia
        /// dos rótulos curtos (botões, abas internas). Limita a profundidade e o total para não travar.
        /// </summary>
        static Dictionary<string, Arvore> TextosPorUia(List<Regra> regras)
        {
            var d = new Dictionary<string, Arvore>(StringComparer.OrdinalIgnoreCase);
            var procs = regras.Where(r => r.Metodo == "titulo-guia").Select(r => r.Processo).Distinct().ToList();
            foreach (var p in Process.GetProcesses())
            {
                if (!procs.Any(pr => Casa(pr, p.ProcessName))) continue;
                if (p.MainWindowHandle == IntPtr.Zero) continue;
                try
                {
                    var raiz = AutomationElement.FromHandle(p.MainWindowHandle);
                    if (raiz == null) continue;
                    if (!d.TryGetValue(p.ProcessName, out var a)) { a = new Arvore { Guias = new List<string>(), Rotulos = new List<string>() }; }
                    ColherGuias(raiz, a.Guias);
                    d[p.ProcessName] = a;
                }
                catch { /* janela fechou no meio */ }
            }
            return d;
        }

        /// <summary>
        /// Guias da faixa do navegador, com o nome já limpo (GuiasNav.Normalizar). Não entra na página (Document):
        /// abas de dentro dos sites ("Arquivos", "Código") não são guias. O título da janela entra junto, porque é o
        /// único lugar com o "(n)" da guia ativa.
        /// </summary>
        internal static void ColherGuias(AutomationElement raiz, List<string> saida)
        {
            var brutas = new List<string>();
            string titulo = "";
            try { titulo = raiz.Current.Name ?? ""; } catch { }
            try
            {
                var w = TreeWalker.ControlViewWalker;
                var fila = new Queue<KeyValuePair<AutomationElement, int>>();
                fila.Enqueue(new KeyValuePair<AutomationElement, int>(raiz, 0));
                int vistos = 0;
                while (fila.Count > 0 && vistos < 3000 && brutas.Count < 400)
                {
                    var item = fila.Dequeue(); var e = item.Key; var nivel = item.Value;
                    vistos++;
                    ControlType ct;
                    try { ct = e.Current.ControlType; } catch { continue; }
                    if (ct == ControlType.Document) continue;          // conteúdo do site: fora
                    if (ct == ControlType.TabItem)
                    {
                        var nome = Nome(e);
                        if (!string.IsNullOrEmpty(nome)) brutas.Add(nome);
                        continue;                                      // guia não tem guia dentro
                    }
                    if (nivel >= 20) continue;
                    try
                    {
                        for (var f = w.GetFirstChild(e); f != null; f = w.GetNextSibling(f))
                            fila.Enqueue(new KeyValuePair<AutomationElement, int>(f, nivel + 1));
                    }
                    catch { }
                }
            }
            catch { }
            saida.AddRange(GuiasNav.Juntar(brutas, titulo));
        }

        static void ColherRotulos(AutomationElement raiz, List<string> saida)
        {
            try
            {
                var cond = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
                foreach (AutomationElement e in raiz.FindAll(TreeScope.Descendants, cond))
                {
                    var nome = Nome(e);
                    if (!string.IsNullOrEmpty(nome) && nome.Length <= 40) saida.Add(nome);
                    if (saida.Count > 1500) break;   // teto: a página do Gestta é grande
                }
            }
            catch { }
        }

        static string Nome(AutomationElement e)
        {
            try { return (e.Current.Name ?? "").Trim(); } catch { return ""; }
        }
    }
}
