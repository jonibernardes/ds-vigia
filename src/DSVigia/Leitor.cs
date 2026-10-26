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

        public static List<Leitura> Ler(List<Regra> regras)
        {
            UltimoDiag = new List<string>();
            var titulos = TitulosDeProcesso();          // processo -> títulos de janela
            foreach (var kv in titulos) foreach (var t in kv.Value) UltimoDiag.Add("janela " + kv.Key + " | " + t);
            var daArvore = TextosPorUia(regras);        // coletado uma vez por processo de navegador
            foreach (var kv in daArvore) UltimoDiag.Add("uia " + kv.Key + " guias=" + kv.Value.Guias.Count + " rotulos=" + kv.Value.Rotulos.Count);
            var saida = new Dictionary<string, Leitura>();

            foreach (var r in regras.Where(r => r.Ativo).OrderBy(r => r.Ordem))
            {
                int? n = null;
                if (r.Metodo == "janela")
                {
                    var alvo = titulos.Where(kv => Casa(r.Processo, kv.Key)).SelectMany(kv => kv.Value);
                    n = Contagem.PorRotulo(alvo, r.Rotulo);
                }
                else if (r.Metodo == "titulo-guia")
                {
                    var textos = daArvore.Where(kv => Casa(r.Processo, kv.Key)).SelectMany(kv => kv.Value.Guias);
                    n = Contagem.PorRotulo(textos, r.Rotulo);
                }
                else if (r.Metodo == "notificacao")
                {
                    n = Notificacoes.Badge(r.App);
                }
                // ocr: só quando a Denise marcar uma região; fica para a v2 (a extensão não fazia)

                UltimoDiag.Add("regra " + r.Chave + "/" + r.Metodo + " proc=" + r.Processo + " -> " + (n.HasValue ? n.Value.ToString() : "null"));
                if (!n.HasValue) continue;
                var ja = saida.TryGetValue(r.Chave, out var p) ? p : new Leitura { Chave = r.Chave, Nome = r.Nome };
                ja.Pendentes = Contagem.Juntar(new[] { ja.Pendentes, n });
                if (string.IsNullOrEmpty(ja.Nome)) ja.Nome = r.Nome;
                saida[r.Chave] = ja;
            }

            // desde quando espera: só pelas notificações, que o Windows carimba. Título de janela não tem hora.
            foreach (var chave in saida.Keys.ToList())
            {
                var app = regras.FirstOrDefault(r => r.Chave == chave && r.Metodo == "notificacao" && r.App != "");
                if (app != null && saida[chave].Pendentes > 0)
                {
                    var l = saida[chave];
                    l.MaisAntiga = Notificacoes.MaisAntigaNaoLida(app.App);
                    saida[chave] = l;
                }
            }
            return saida.Values.ToList();
        }

        static bool Casa(string padrao, string valor)
        {
            if (string.IsNullOrEmpty(padrao)) return true;
            var re = Contagem.Re(padrao);
            return re != null && re.IsMatch(valor ?? "");
        }

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

        static void ColherGuias(AutomationElement raiz, List<string> saida)
        {
            try
            {
                var cond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);
                foreach (AutomationElement e in raiz.FindAll(TreeScope.Descendants, cond))
                {
                    var nome = Nome(e);
                    if (!string.IsNullOrEmpty(nome)) saida.Add(nome);
                    if (saida.Count > 400) break;
                }
            }
            catch { }
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
