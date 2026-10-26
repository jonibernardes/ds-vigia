using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DSVigia.Nucleo;

namespace DSVigia
{
    /// <summary>
    /// O vigia: um ícone na bandeja que mostra o total de conversas esperando e abre a barrinha do sinaleiro.
    /// A cada ~20s lê os números, pinta o ícone, avisa aos 10 min e manda ao sistema. Visível, nunca oculto.
    /// </summary>
    public sealed class AplicacaoBandeja : IDisposable
    {
        readonly NotifyIcon icone;
        readonly System.Windows.Forms.Timer relogio;
        readonly SynchronizationContext ui;
        readonly Estado estado;
        readonly Enviador enviador;
        readonly Dictionary<string, Canal> canais = new Dictionary<string, Canal>();
        List<Regra> regras;
        volatile List<Farol> farois;   // vazio = automático
        ConfigurarFarois telaFarois;
        DateTime regrasEm = DateTime.MinValue, ultimoEnvio = DateTime.MinValue, ultimoPonto = DateTime.MinValue;
        Sinaleiro barra;
        bool ocupado;

        public AplicacaoBandeja()
        {
            ui = SynchronizationContext.Current;
            if (ui == null) { ui = new WindowsFormsSynchronizationContext(); SynchronizationContext.SetSynchronizationContext(ui); }
            estado = Estado.Carregar();
            enviador = new Enviador(estado);
            regras = RegrasPadrao.Todas();
            farois = ArquivoFarois.Carregar();

            icone = new NotifyIcon { Visible = true, Text = "DS Vigia", Icon = Pintar(Color.Gray, "") };
            icone.DoubleClick += (s, e) => AbrirSinaleiro();
            icone.ContextMenuStrip = Menu();

            relogio = new System.Windows.Forms.Timer { Interval = 20000 };
            relogio.Tick += (s, e) => Ciclo(false);
            relogio.Start();

            try {
                var gat = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DSVigia", "abrir-sinaleiro");
                if (System.IO.File.Exists(gat)) { System.IO.File.Delete(gat); AbrirSinaleiro(); }
            } catch { }
            ThreadPool.QueueUserWorkItem(_ => { AutoAtualizar.Verificar(); try { regras = enviador.Regras(); regrasEm = DateTime.UtcNow; } catch { } Ciclo(true); });
            if (!estado.Logado) PedirLogin();
        }

        ContextMenuStrip Menu()
        {
            var m = new ContextMenuStrip();
            m.Items.Add("Abrir sinaleiro", null, (s, e) => AbrirSinaleiro());
            m.Items.Add("Configurar faróis", null, (s, e) => AbrirFarois());
            m.Items.Add(new ToolStripSeparator());
            var entrar = m.Items.Add("Entrar / trocar usuário", null, (s, e) => PedirLogin());
            m.Items.Add("Nome deste computador", null, (s, e) => RenomearPC());
            m.Items.Add("Verificar agora", null, (s, e) => Ciclo(true));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Sair", null, (s, e) => { icone.Visible = false; Application.Exit(); });
            m.Opening += (s, e) => entrar.Text = estado.Logado ? "Usuário: " + (estado.Nome ?? estado.Email) : "Entrar no sistema";
            return m;
        }

        void Ciclo(bool forcar)
        {
            if (ocupado) return;
            ocupado = true;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var agora = DateTime.UtcNow;
                    if ((agora - regrasEm).TotalMinutes > 1 && estado.Logado) { try { regras = enviador.Regras(); regrasEm = agora; } catch { } }

                    bool mudou = false;
                    var vistos = new HashSet<string>();
                    var fs = farois;
                    bool comFarois = fs != null && fs.Count > 0;
                    foreach (var l in Leitor.Ler(regras, fs))
                    {
                        vistos.Add(l.Chave);
                        if (!canais.TryGetValue(l.Chave, out var c)) { c = new Canal { Chave = l.Chave, Nome = l.Nome }; canais[l.Chave] = c; }
                        if (string.IsNullOrEmpty(c.Nome)) c.Nome = l.Nome;
                        else if (comFarois && c.Nome != l.Nome) { c.Nome = l.Nome; mudou = true; }   // farol renomeado
                        mudou |= c.Atualizar(l.Pendentes, agora, l.MaisAntiga);
                    }
                    // trocou de modo (ou tirou um farol): o sinal velho vai fechado ao sistema uma vez e sai da lista
                    var descartar = canais.Values.Where(c => !vistos.Contains(c.Chave) && (comFarois || c.Chave.StartsWith(Farois.Prefixo))).Select(c => c.Chave).ToList();
                    foreach (var c in canais.Values)
                        if (!vistos.Contains(c.Chave)) mudou |= c.Atualizar(null, agora);

                    foreach (var c in canais.Values.Where(c => c.PrecisaAvisar(agora)))
                    {
                        c.Notificado = true; mudou = true;
                        if (ui != null) ui.Post(_2 => icone.ShowBalloonTip(8000, c.Nome + ": " + c.Pendentes + (c.Pendentes == 1 ? " conversa esperando" : " conversas esperando"),
                            "Sem resposta há " + Canal.TextoEspera(c.Minutos(DateTime.UtcNow)) + ".", ToolTipIcon.Warning), null);
                    }

                    try {
                        var dbg = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DSVigia", "debug");
                        if (System.IO.File.Exists(dbg)) {
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine("== canais ==");
                            foreach (var c in canais.Values) sb.AppendLine(c.Chave + " nome=" + c.Nome + " pend=" + (c.Pendentes?.ToString() ?? "null") + " aberto=" + c.Aberto + " cor=" + c.CorEm(agora));
                            sb.AppendLine("== diagnostico leitor ==");
                            foreach (var d in Leitor.UltimoDiag) sb.AppendLine(d);
                            System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DSVigia", "ultima-leitura.txt"), sb.ToString());
                        }
                    } catch { }
                    if (estado.Logado && (mudou || forcar || (agora - ultimoEnvio).TotalSeconds > 55))
                    {
                        enviador.Enviar(canais.Values.ToList(), agora); ultimoEnvio = agora;
                    }
                    foreach (var k in descartar) canais.Remove(k);
                    if (estado.Logado && (agora - ultimoPonto).TotalMinutes > 2)
                    {
                        enviador.BaterPonto(agora, canais.Count + " canais"); ultimoPonto = agora;
                    }

                    if (ui != null) ui.Post(_2 => Pintar(agora), null); else Pintar(agora);
                }
                catch { }
                finally { ocupado = false; }
            });
        }

        void Pintar(DateTime agora)
        {
            var vivos = canais.Values.Where(c => c.Aberto).ToList();
            int total = vivos.Sum(c => c.Pendentes ?? 0);
            var cores = vivos.Select(c => c.CorEm(agora)).ToList();
            var cor = cores.Contains(Cor.Vermelho) ? Color.FromArgb(217, 45, 32)
                    : cores.Contains(Cor.Amarelo) ? Color.FromArgb(245, 183, 10)
                    : cores.Contains(Cor.Verde) ? Color.FromArgb(18, 161, 80) : Color.Gray;
            var velho = icone.Icon;
            icone.Icon = Pintar(cor, total > 0 ? (total > 99 ? "99" : total.ToString()) : "");
            icone.Text = estado.Logado ? ("DS Vigia · " + (total == 0 ? "em dia" : total + " esperando")) : "DS Vigia · entre no sistema";
            velho?.Dispose();
            barra?.Mostrar(canais.Values.ToList(), agora, Ordem());
        }

        static Icon Pintar(Color cor, string texto)
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(cor)) g.FillEllipse(b, 1, 1, 30, 30);
                if (!string.IsNullOrEmpty(texto))
                    using (var f = new Font("Segoe UI", texto.Length > 1 ? 11 : 15, FontStyle.Bold))
                    {
                        var sz = g.MeasureString(texto, f);
                        g.DrawString(texto, f, Brushes.White, (32 - sz.Width) / 2, (32 - sz.Height) / 2);
                    }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        void AbrirSinaleiro()
        {
            if (barra == null || barra.IsDisposed) barra = new Sinaleiro();
            barra.Show(); barra.Mostrar(canais.Values.ToList(), DateTime.UtcNow, Ordem());
        }

        /// <summary>Com faróis, o sinaleiro segue a ordem que a atendente escolheu; sem, a de sempre (por cor).</summary>
        List<string> Ordem()
        {
            var fs = farois;
            return fs != null && fs.Count > 0 ? fs.Select(f => Farois.Prefixo + f.Id).ToList() : null;
        }

        void AbrirFarois()
        {
            if (telaFarois != null && !telaFarois.IsDisposed) { telaFarois.WindowState = FormWindowState.Normal; telaFarois.Activate(); return; }
            telaFarois = new ConfigurarFarois(() => regras, farois, novos => { farois = novos; Ciclo(true); });
            telaFarois.Show();
        }

        void PedirLogin()
        {
            using (var f = new JanelaLogin(estado.Logado ? estado.Email : ""))
            {
                if (f.ShowDialog() != DialogResult.OK) return;
                try { var nome = enviador.Entrar(f.Email, f.Senha); icone.ShowBalloonTip(4000, "DS Vigia", "Entrou como " + nome + ". O sinaleiro vai aparecer no canto da tela.", ToolTipIcon.Info); AbrirSinaleiro(); Ciclo(true); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "DS Vigia", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        void RenomearPC()
        {
            using (var f = new JanelaTexto("Nome deste computador", "Como este computador aparece no painel:", estado.NomePC ?? Environment.MachineName))
                if (f.ShowDialog() == DialogResult.OK && f.Valor.Trim().Length > 0) { estado.NomePC = f.Valor.Trim(); estado.Salvar(); Ciclo(true); }
        }

        public void Dispose() { telaFarois?.Dispose(); relogio?.Dispose(); icone?.Dispose(); barra?.Dispose(); }
    }
}
