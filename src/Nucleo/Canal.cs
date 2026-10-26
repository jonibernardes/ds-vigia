using System;

namespace DSVigia.Nucleo
{
    public enum Cor { Verde, Amarelo, Vermelho, Cinza }

    /// <summary>O estado de um sistema neste computador. Mesmas regras da extensão e da tela /atendimento.</summary>
    public sealed class Canal
    {
        public const int LimiteMin = 10;

        public string Chave = "";
        public string Nome = "";
        public bool Aberto;
        public int? Pendentes;
        public DateTime? PendenteDesde;   // UTC
        public bool Notificado;

        /// <summary>
        /// Leitura nova. null = o programa não está aberto. PendenteDesde marca a 1ª vez que viu conversa
        /// esperando (ou a hora da notificação mais antiga, quando o Windows informa) e zera quando não sobra nada.
        /// Devolve true se mudou algo que vale mandar ao sistema.
        /// </summary>
        public bool Atualizar(int? pendentes, DateTime agora, DateTime? maisAntiga = null)
        {
            bool mudou;
            if (!pendentes.HasValue)
            {
                mudou = Aberto || Pendentes != null;
                Aberto = false; Pendentes = null; PendenteDesde = null; Notificado = false;
                return mudou;
            }
            mudou = !Aberto || Pendentes != pendentes;
            Aberto = true;
            Pendentes = pendentes;
            if (pendentes.Value > 0)
            {
                if (!PendenteDesde.HasValue)
                {
                    PendenteDesde = maisAntiga.HasValue && maisAntiga.Value < agora ? maisAntiga : agora;
                    Notificado = false;
                    mudou = true;
                }
                else if (maisAntiga.HasValue && maisAntiga.Value < PendenteDesde.Value)
                {
                    PendenteDesde = maisAntiga; mudou = true;
                }
            }
            else
            {
                if (PendenteDesde.HasValue) mudou = true;
                PendenteDesde = null; Notificado = false;
            }
            return mudou;
        }

        public int Minutos(DateTime agora)
        {
            if (!PendenteDesde.HasValue || !(Pendentes > 0)) return 0;
            return Math.Max(0, (int)Math.Floor((agora - PendenteDesde.Value).TotalMinutes));
        }

        public Cor CorEm(DateTime agora)
        {
            if (!Aberto || !Pendentes.HasValue) return Cor.Cinza;
            if (Pendentes.Value == 0) return Cor.Verde;
            return Minutos(agora) >= LimiteMin ? Cor.Vermelho : Cor.Amarelo;
        }

        public bool PrecisaAvisar(DateTime agora) => CorEm(agora) == Cor.Vermelho && !Notificado;

        public static string TextoEspera(int minutos)
        {
            if (minutos < 1) return "agora";
            if (minutos < 60) return minutos + " min";
            var h = minutos / 60;
            return h + " h" + (minutos % 60 > 0 ? " " + (minutos % 60) + " min" : "");
        }
    }

    public static class Versao
    {
        /// <summary>"1.2.10" &gt; "1.2.9". Texto ruim conta como 0.</summary>
        public static int Comparar(string a, string b)
        {
            var pa = (a ?? "").Split('.'); var pb = (b ?? "").Split('.');
            for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                int x = i < pa.Length && int.TryParse(pa[i], out var t1) ? t1 : 0;
                int y = i < pb.Length && int.TryParse(pb[i], out var t2) ? t2 : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }
    }
}
