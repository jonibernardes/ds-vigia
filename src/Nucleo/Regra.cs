using System;
using System.Collections.Generic;

namespace DSVigia.Nucleo
{
    /// <summary>
    /// Como achar o número de conversas esperando num programa. Vem da tabela at_regras do sistema
    /// (o gestor ajusta lá e todos os computadores pegam em 1 minuto) e tem cópia embutida para
    /// funcionar mesmo sem internet.
    /// </summary>
    public sealed class Regra
    {
        public string Id = "";
        public string Chave = "";          // canal: whatsapp, gestta, npconnect, email, telegram...
        public string Nome = "";           // como aparece no sinaleiro
        public string Metodo = "";         // titulo-guia | janela | notificacao | pagina | ocr
        public string Processo = "";       // regex no nome do processo (chrome|msedge)
        public string Janela = "";         // regex no título da janela (opcional)
        public string Controle = "";       // reservado
        public string Rotulo = "";         // regex; o grupo 1 é o número. Sem grupo: conta as ocorrências
        public string App = "";            // notificacao: pedaço do id do app (WhatsAppDesktop)
        public string TipoNotificacao = "";// notificacao: badge | toast
        public string Regiao = "";         // ocr: "x,y,l,a" em % da janela
        public bool Ativo = true;
        public int Ordem;

        public override string ToString() => $"{Chave}/{Metodo}";
    }

    public static class RegrasPadrao
    {
        /// <summary>
        /// Regras embutidas. As leituras de navegador usam só o TÍTULO DA GUIA, que identifica o site e
        /// não se confunde com texto de outra página aberta. Contagem que mora dentro da página (o
        /// "Pendentes N" do Gestta, o "Aguardando N" do NP Connect) não vem pelo título: essas o gestor
        /// cadastra em at_regras com o método "pagina" (lido pela extensão por aba) ou fica como presença
        /// (aberto, em dia) até chegar o número. Isso evita contar "Pendentes" de um site qualquer.
        /// </summary>
        public static List<Regra> Todas() => new List<Regra>
        {
            // WhatsApp Web: o título da guia traz "(3) WhatsApp", por aba, sem se misturar com outra página
            new Regra { Id = "p-wa-web", Chave = "whatsapp", Nome = "WhatsApp", Metodo = "titulo-guia", Processo = "^(chrome|msedge)$", Rotulo = @"^(?:\((\d{1,4})\)\s*)?WhatsApp\b", Ordem = 10 },
            // WhatsApp de computador: o número do ícone na barra de tarefas, e o título da janela
            new Regra { Id = "p-wa-app", Chave = "whatsapp", Nome = "WhatsApp", Metodo = "notificacao", App = "WhatsAppDesktop", TipoNotificacao = "badge", Ordem = 11 },
            new Regra { Id = "p-wa-titulo", Chave = "whatsapp", Nome = "WhatsApp", Metodo = "janela", Processo = "^WhatsApp", Rotulo = @"^WhatsApp(?:.*\((\d{1,4})\))?", Ordem = 12 },
            // E-mail no navegador: "Caixa de entrada (5)" no título da guia
            new Regra { Id = "p-email-web", Chave = "email", Nome = "E-mail", Metodo = "titulo-guia", Processo = "^(chrome|msedge)$", Rotulo = @"^(?:Caixa de entrada|Inbox|Entrada)(?:\s*\((\d{1,4})\))?", Ordem = 40 },
            new Regra { Id = "p-outlook", Chave = "email", Nome = "E-mail", Metodo = "notificacao", App = "Outlook", TipoNotificacao = "badge", Ordem = 41 },
            // Telegram de computador
            new Regra { Id = "p-tg", Chave = "telegram", Nome = "Telegram", Metodo = "janela", Processo = "^Telegram$", Rotulo = @"^Telegram(?:\s*\((\d{1,4})\))?", Ordem = 50 },
            new Regra { Id = "p-tg-badge", Chave = "telegram", Nome = "Telegram", Metodo = "notificacao", App = "TelegramDesktop", TipoNotificacao = "badge", Ordem = 51 },
            // Gestta e NP Connect: presença pela guia (fica "em dia" com 0 até o número chegar pela extensão
            // ou por uma regra "pagina" do escritório). Não varre a página inteira, para não contar errado.
            new Regra { Id = "p-gestta", Chave = "gestta", Nome = "Messenger", Metodo = "titulo-guia", Processo = "^(chrome|msedge)$", Rotulo = @"(?:^|\W)Gestta\b(?:.*\((\d{1,4})\))?", Ordem = 20 },
            new Regra { Id = "p-np", Chave = "npconnect", Nome = "NP Connect", Metodo = "titulo-guia", Processo = "^(chrome|msedge)$", Rotulo = @"(?:^|\W)NP\s?Connect\b(?:.*\((\d{1,4})\))?", Ordem = 30 },
        };
    }
}
