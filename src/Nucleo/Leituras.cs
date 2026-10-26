using System;
using System.Collections.Generic;
using System.Linq;

namespace DSVigia.Nucleo
{
    /// <summary>Uma leitura pronta para o Canal: só número e, quando dá, desde quando espera.</summary>
    public sealed class LeituraCanal
    {
        public string Chave = "";
        public string Nome = "";
        public int? Pendentes;
        public DateTime? MaisAntiga;
    }

    /// <summary>
    /// O modo automático: aplica as regras a tudo que está aberto e junta por canal (vale o maior).
    /// O leitor colhe os textos no Windows e passa aqui; nada de Windows neste arquivo.
    /// </summary>
    public static class Leituras
    {
        /// <param name="janelas">processo → títulos de janela</param>
        /// <param name="guias">processo de navegador → títulos de guia</param>
        /// <param name="badge">número do ícone do app (pedaço do id) ou null</param>
        public static List<LeituraCanal> Automatico(IList<Regra> regras, IDictionary<string, List<string>> janelas,
            IDictionary<string, List<string>> guias, Func<string, int?> badge, Action<string> diag = null)
        {
            var saida = new Dictionary<string, LeituraCanal>();
            foreach (var r in regras.Where(r => r.Ativo).OrderBy(r => r.Ordem))
            {
                int? n = null;
                if (r.Metodo == "janela")
                    n = Contagem.PorRotulo(janelas.Where(kv => Casa(r.Processo, kv.Key)).SelectMany(kv => kv.Value), r.Rotulo);
                else if (r.Metodo == "titulo-guia")
                    n = Contagem.PorRotulo(guias.Where(kv => Casa(r.Processo, kv.Key)).SelectMany(kv => kv.Value), r.Rotulo);
                else if (r.Metodo == "notificacao")
                    n = badge?.Invoke(r.App);
                // ocr: só quando a Denise marcar uma região; fica para a v2 (a extensão não fazia)

                diag?.Invoke("regra " + r.Chave + "/" + r.Metodo + " proc=" + r.Processo + " -> " + (n.HasValue ? n.Value.ToString() : "null"));
                if (!n.HasValue) continue;
                if (!saida.TryGetValue(r.Chave, out var ja)) { ja = new LeituraCanal { Chave = r.Chave, Nome = r.Nome }; saida[r.Chave] = ja; }
                ja.Pendentes = Contagem.Juntar(new[] { ja.Pendentes, n });
                if (string.IsNullOrEmpty(ja.Nome)) ja.Nome = r.Nome;
            }
            return saida.Values.ToList();
        }

        public static bool Casa(string padrao, string valor)
        {
            if (string.IsNullOrEmpty(padrao)) return true;
            var re = Contagem.Re(padrao);
            return re != null && re.IsMatch(valor ?? "");
        }
    }
}
