using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DSVigia.Nucleo;

namespace DSVigia
{
    /// <summary>Os faróis ficam em %AppData%\DSVigia\farois.json, só deste usuário.</summary>
    public static class ArquivoFarois
    {
        public static string Caminho => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DSVigia", "farois.json");

        public static List<Farol> Carregar()
        {
            try { return File.Exists(Caminho) ? Farois.Carregar(File.ReadAllText(Caminho, Encoding.UTF8)) : new List<Farol>(); }
            catch { return new List<Farol>(); }
        }

        public static void Salvar(List<Farol> farois)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Caminho));
            if (farois.Count == 0) { if (File.Exists(Caminho)) File.Delete(Caminho); return; }
            var tmp = Caminho + ".tmp";
            File.WriteAllText(tmp, Farois.Salvar(farois), new UTF8Encoding(false));
            if (File.Exists(Caminho)) File.Replace(tmp, Caminho, null); else File.Move(tmp, Caminho);
        }
    }

    /// <summary>
    /// Tela "Configurar faróis": a atendente diz o que cada sinal vigia. Mostra só o nome do sistema
    /// reconhecido em cada navegador + perfil; nunca título de conversa.
    /// </summary>
    public sealed class ConfigurarFarois : Form
    {
        static readonly Color Escuro = Color.FromArgb(15, 46, 46), Verde = Color.FromArgb(18, 161, 80),
            Vermelho = Color.FromArgb(217, 45, 32), Texto = Color.FromArgb(29, 43, 43), Apoio = Color.FromArgb(90, 105, 105),
            AvisoFundo = Color.FromArgb(255, 244, 214), AvisoBorda = Color.FromArgb(245, 183, 10);

        readonly Func<List<Regra>> regras;
        readonly Action<List<Farol>> aoSalvar;
        readonly List<Farol> farois;
        readonly ListView abertos, lista;
        readonly TextBox nome;
        readonly Button ver, adicionar, renomear, remover, subir, descer, salvar, automatico, fechar;
        readonly Label situacao, aviso;
        bool mexeu;

        public ConfigurarFarois(Func<List<Regra>> regras, List<Farol> atuais, Action<List<Farol>> aoSalvar)
        {
            this.regras = regras; this.aoSalvar = aoSalvar;
            farois = (atuais ?? new List<Farol>()).Select(f => f.Copia()).ToList();

            SuspendLayout();
            Font = new Font("Segoe UI", 10F);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Configurar faróis · DS Vigia";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 520);
            BackColor = Color.White; ForeColor = Texto;
            ShowInTaskbar = true;

            var raiz = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(0) };
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // faixa do topo na cor do sinaleiro
            var topo = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Escuro, Padding = new Padding(16, 12, 16, 12), Margin = new Padding(0) };
            topo.Controls.Add(new Label { Text = "Configurar faróis", ForeColor = Color.White, Font = new Font("Segoe UI", 13F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0) });
            topo.Controls.Add(new Label { Text = "Cada farol é um sinal no sinaleiro, com o nome que você escolher.", ForeColor = Color.FromArgb(138, 222, 220), AutoSize = true, Margin = new Padding(0, 4, 0, 0) });
            raiz.Controls.Add(topo, 0, 0);

            // aviso fixo
            aviso = new Label
            {
                Text = "Duas contas do mesmo site no mesmo perfil do Chrome ficam iguais para o vigia. Use um perfil do Chrome para cada WhatsApp.",
                AutoSize = true, BackColor = AvisoFundo, ForeColor = Texto, Padding = new Padding(14, 8, 10, 8), Margin = new Padding(16, 14, 16, 4),
            };
            aviso.Paint += (s, e) => { using (var b = new SolidBrush(AvisoBorda)) e.Graphics.FillRectangle(b, 0, 0, (int)Math.Round(4 * e.Graphics.DpiX / 96f), aviso.Height); };
            raiz.Controls.Add(aviso, 0, 1);

            // corpo: o que está aberto | seus faróis
            var corpo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(16, 8, 16, 0) };
            corpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            corpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
            corpo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var esq = Coluna(3);
            esq.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            esq.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            esq.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            esq.Controls.Add(Titulo("1. O que está aberto"), 0, 0);
            ver = Botao("Ver o que está aberto agora", true);
            ver.Click += (s, e) => VerAbertos();
            esq.Controls.Add(ver, 0, 1);
            abertos = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, HeaderStyle = ColumnHeaderStyle.Nonclickable, Margin = new Padding(0, 8, 0, 0) };
            abertos.Columns.Add("Sistema", 150); abertos.Columns.Add("Onde", 150);
            abertos.Items.Add(new ListViewItem(new[] { "Procurando...", "" }) { ForeColor = Apoio });
            abertos.SelectedIndexChanged += (s, e) => SugerirNome();
            abertos.DoubleClick += (s, e) => Adicionar();
            esq.Controls.Add(abertos, 0, 2);
            corpo.Controls.Add(esq, 0, 0);

            var dir = Coluna(5);
            dir.Margin = new Padding(16, 0, 0, 0);
            dir.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            dir.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            dir.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            dir.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            dir.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            dir.Controls.Add(Titulo("2. Seus faróis (até " + Farois.Max + ")"), 0, 0);
            lista = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, HeaderStyle = ColumnHeaderStyle.Nonclickable, Margin = new Padding(0, 4, 0, 0) };
            lista.Columns.Add("Nome", 140); lista.Columns.Add("Vigia", 160);
            lista.ShowItemToolTips = true;
            lista.SelectedIndexChanged += (s, e) => { if (lista.SelectedIndices.Count > 0) nome.Text = farois[lista.SelectedIndices[0]].Nome; Botoes(); };
            dir.Controls.Add(lista, 0, 1);
            dir.Controls.Add(new Label { Text = "Nome do farol", AutoSize = true, Margin = new Padding(0, 10, 0, 2) }, 0, 2);
            nome = new TextBox { Dock = DockStyle.Fill, MaxLength = 40, Margin = new Padding(0, 0, 0, 6) };
            nome.TextChanged += (s, e) => Botoes();
            dir.Controls.Add(nome, 0, 3);
            var acoes = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            adicionar = Botao("Adicionar", true); adicionar.Click += (s, e) => Adicionar();
            renomear = Botao("Renomear", false); renomear.Click += (s, e) => Renomear();
            remover = Botao("Remover", false); remover.Click += (s, e) => Remover();
            subir = Botao("Subir ▲", false); subir.Click += (s, e) => Mover(-1);
            descer = Botao("Descer ▼", false); descer.Click += (s, e) => Mover(+1);
            acoes.Controls.AddRange(new Control[] { adicionar, renomear, remover, subir, descer });
            dir.Controls.Add(acoes, 0, 4);
            corpo.Controls.Add(dir, 1, 0);
            raiz.Controls.Add(corpo, 0, 2);

            // rodapé
            var rodape = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, AutoSize = true, Margin = new Padding(16, 10, 16, 14) };
            rodape.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            rodape.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rodape.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            automatico = Botao("Voltar ao automático", false);
            automatico.ForeColor = Vermelho;
            automatico.Click += (s, e) => VoltarAoAutomatico();
            rodape.Controls.Add(automatico, 0, 0);
            situacao = new Label { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Apoio, Margin = new Padding(12, 0, 12, 0) };
            rodape.Controls.Add(situacao, 1, 0);
            var direita = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            fechar = Botao("Fechar", false); fechar.Click += (s, e) => Close();
            salvar = Botao("Salvar", true); salvar.Click += (s, e) => Salvar();
            salvar.BackColor = Verde; salvar.ForeColor = Color.White; salvar.FlatAppearance.BorderColor = Verde;
            direita.Controls.Add(fechar); direita.Controls.Add(salvar);
            rodape.Controls.Add(direita, 2, 0);
            raiz.Controls.Add(rodape, 0, 3);

            Controls.Add(raiz);
            AcceptButton = null; CancelButton = fechar;
            ResumeLayout(true);

            Load += (s, e) => MinimumSize = Size;   // 720x520 já na escala da tela; dá para aumentar
            Resize += (s, e) => Ajustar();
            Shown += (s, e) => { Ajustar(); VerAbertos(); };
            Encher();
        }

        static TableLayoutPanel Coluna(int linhas) =>
            new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = linhas, Margin = new Padding(0), Padding = new Padding(0) };

        static Label Titulo(string t) =>
            new Label { Text = t, AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = Escuro, Margin = new Padding(0, 4, 0, 8) };

        static Button Botao(string t, bool principal)
        {
            var b = new Button
            {
                Text = t, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlatStyle = FlatStyle.Flat,
                Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 0, 8, 6), MinimumSize = new Size(0, 34),
                BackColor = Color.White, ForeColor = principal ? Escuro : Texto, Cursor = Cursors.Hand, UseVisualStyleBackColor = false,
            };
            b.FlatAppearance.BorderColor = principal ? Escuro : Color.FromArgb(190, 200, 200);
            return b;
        }

        void Ajustar()
        {
            // o aviso quebra a linha na largura da janela; as colunas das listas acompanham
            var largura = Math.Max(200, ClientSize.Width - aviso.Margin.Horizontal);
            aviso.MaximumSize = new Size(largura, 0); aviso.MinimumSize = new Size(largura, 0);
            foreach (var lv in new[] { abertos, lista })
            {
                var w = lv.ClientSize.Width;
                if (w <= 0 || lv.Columns.Count < 2) continue;
                lv.Columns[0].Width = (int)(w * (lv == lista ? 0.42 : 0.48));
                lv.Columns[1].Width = w - lv.Columns[0].Width - 2;
            }
        }

        // ---------- o que está aberto ----------

        void VerAbertos()
        {
            ver.Enabled = false; ver.Text = "Procurando...";
            situacao.Text = "";
            var rs = regras();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                List<Perfis.Janela> janelas; List<LeituraApp> apps;
                try { janelas = Perfis.Janelas(); } catch { janelas = new List<Perfis.Janela>(); }
                try { apps = Leitor.LerApps(rs); } catch { apps = new List<LeituraApp>(); }
                var guias = janelas.SelectMany(j => j.Guias.Select(t => new GuiaVista { Navegador = j.Navegador, PerfilId = j.PerfilId, PerfilNome = j.PerfilNome, Titulo = t })).ToList();
                var nav = Farois.Reconhecer(rs, guias);
                var progs = Farois.ReconhecerApps(rs, apps);
                // perfis abertos, mesmo sem sistema reconhecido (os títulos não saem daqui)
                var perfis = janelas.Select(j => new { j.Navegador, j.PerfilId, j.PerfilNome }).Distinct().ToList();
                try
                {
                    if (IsDisposed) return;
                    BeginInvoke((Action)(() =>
                    {
                        abertos.BeginUpdate(); abertos.Items.Clear(); abertos.Groups.Clear();
                        foreach (var p in perfis.OrderBy(p => p.Navegador).ThenBy(p => p.PerfilNome))
                        {
                            var rotulo = Farois.NomeDoNavegador(p.Navegador) + " · " + (p.PerfilId == "" ? "perfil não identificado" : "perfil " + p.PerfilNome);
                            var g = new ListViewGroup(rotulo); abertos.Groups.Add(g);
                            var doPerfil = p.PerfilId == "" ? new List<Reconhecido>() : nav.Where(r => r.Navegador == p.Navegador && r.PerfilId == p.PerfilId).ToList();
                            foreach (var r in doPerfil)
                                abertos.Items.Add(new ListViewItem(new[] { r.Sistema, Onde(r) }, g) { Tag = r });
                            if (doPerfil.Count == 0)
                                abertos.Items.Add(new ListViewItem(new[] { p.PerfilId == "" ? "não dá para usar" : "nada reconhecido", Farois.NomeDoNavegador(p.Navegador) }, g) { ForeColor = Apoio });
                        }
                        if (progs.Count > 0)
                        {
                            var g = new ListViewGroup("Programas do computador"); abertos.Groups.Add(g);
                            foreach (var r in progs) abertos.Items.Add(new ListViewItem(new[] { r.Sistema, "programa" }, g) { Tag = r });
                        }
                        if (abertos.Items.Count == 0)
                            abertos.Items.Add(new ListViewItem(new[] { "Nada reconhecido.", "" }) { ForeColor = Apoio });
                        abertos.EndUpdate();
                        ver.Text = "Ver o que está aberto agora"; ver.Enabled = true;
                        Ajustar(); Botoes();
                    }));
                }
                catch (InvalidOperationException) { /* janela fechou */ }
            });
        }

        static string Onde(Reconhecido r) =>
            r.Tipo == "app" ? "programa" : Farois.NomeDoNavegador(r.Navegador) + " · " + (string.IsNullOrEmpty(r.PerfilNome) ? r.PerfilId : r.PerfilNome);

        static string Onde(Farol f) =>
            Farois.NomeDoSistema(f.RegraChave, f.RegraChave, false) +
            (f.Tipo == "app" ? " (programa)" : " · " + (string.IsNullOrEmpty(f.PerfilNome) ? f.PerfilId : f.PerfilNome) + " (" + Farois.NomeDoNavegador(f.Navegador) + ")");

        Reconhecido Escolhido => abertos.SelectedItems.Count > 0 ? abertos.SelectedItems[0].Tag as Reconhecido : null;

        void SugerirNome()
        {
            var r = Escolhido;
            if (r != null && lista.SelectedIndices.Count == 0)
                nome.Text = r.Tipo == "app" ? r.Sistema : r.Sistema.Replace(" Web", "") + " " + (string.IsNullOrEmpty(r.PerfilNome) ? r.PerfilId : r.PerfilNome);
            Botoes();
        }

        // ---------- faróis ----------

        void Encher(int selecionar = -1)
        {
            lista.BeginUpdate(); lista.Items.Clear();
            foreach (var f in farois) lista.Items.Add(new ListViewItem(new[] { f.Nome, Onde(f) }) { ToolTipText = f.Nome + ": " + Onde(f) });
            lista.EndUpdate();
            if (selecionar >= 0 && selecionar < lista.Items.Count) { lista.Items[selecionar].Selected = true; lista.Items[selecionar].Focused = true; }
            Botoes();
        }

        int Sel => lista.SelectedIndices.Count > 0 ? lista.SelectedIndices[0] : -1;

        void Botoes()
        {
            if (adicionar == null) return;
            var n = nome.Text.Trim();
            adicionar.Enabled = Escolhido != null && n != "" && farois.Count < Farois.Max;
            renomear.Enabled = Sel >= 0 && n != "" && n != farois[Sel].Nome;
            remover.Enabled = Sel >= 0;
            subir.Enabled = Sel > 0;
            descer.Enabled = Sel >= 0 && Sel < farois.Count - 1;
            automatico.Enabled = farois.Count > 0 || File.Exists(ArquivoFarois.Caminho);
        }

        bool NomeLivre(string n, int menos)
        {
            for (int i = 0; i < farois.Count; i++)
                if (i != menos && string.Equals(farois[i].Nome, n, StringComparison.CurrentCultureIgnoreCase))
                { Avisar("Já existe um farol com o nome \"" + n + "\"."); return false; }
            return true;
        }

        void Adicionar()
        {
            var r = Escolhido; var n = nome.Text.Trim();
            if (r == null) { Avisar("Escolha uma linha em \"O que está aberto\"."); return; }
            if (n == "") { Avisar("Digite o nome do farol."); nome.Focus(); return; }
            if (farois.Count >= Farois.Max) { Avisar("Já são " + Farois.Max + " faróis. Remova um para pôr outro."); return; }
            if (!NomeLivre(n, -1)) return;
            var igual = farois.FirstOrDefault(f => f.Tipo == r.Tipo && f.RegraChave == r.RegraChave && f.Navegador == r.Navegador && f.PerfilId == r.PerfilId && f.App == r.App);
            if (igual != null) { Avisar("Esse sistema já tem o farol \"" + igual.Nome + "\"."); return; }
            farois.Add(new Farol
            {
                Id = Guid.NewGuid().ToString(), Nome = n, Tipo = r.Tipo, Navegador = r.Navegador, PerfilId = r.PerfilId,
                PerfilNome = r.PerfilNome, RegraChave = r.RegraChave, App = r.App,
            });
            Mexeu(); Encher(farois.Count - 1);
        }

        void Renomear()
        {
            var i = Sel; var n = nome.Text.Trim();
            if (i < 0 || n == "" || !NomeLivre(n, i)) return;
            farois[i].Nome = n; Mexeu(); Encher(i);
        }

        void Remover()
        {
            var i = Sel;
            if (i < 0) return;
            farois.RemoveAt(i); nome.Text = ""; Mexeu(); Encher(Math.Min(i, farois.Count - 1));
        }

        void Mover(int passo)
        {
            var i = Sel; var j = i + passo;
            if (i < 0 || j < 0 || j >= farois.Count) return;
            var f = farois[i]; farois[i] = farois[j]; farois[j] = f;
            Mexeu(); Encher(j);
        }

        void Mexeu() { mexeu = true; situacao.ForeColor = Apoio; situacao.Text = "Mudanças ainda não salvas."; }

        void Avisar(string t) { situacao.ForeColor = Vermelho; situacao.Text = t; }

        bool Salvar()
        {
            try { ArquivoFarois.Salvar(farois); }
            catch (Exception ex) { MessageBox.Show(this, "Não deu para gravar os faróis: " + ex.Message, "DS Vigia", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            aoSalvar?.Invoke(farois.Select(f => f.Copia()).ToList());
            mexeu = false;
            situacao.ForeColor = Verde;
            situacao.Text = farois.Count == 0 ? "Salvo. O vigia está no automático." : "Salvo. O sinaleiro já usa estes faróis.";
            Botoes();
            return true;
        }

        void VoltarAoAutomatico()
        {
            if (MessageBox.Show(this, "Apagar os " + farois.Count + " faróis e voltar a vigiar tudo no automático?", "Voltar ao automático",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            farois.Clear(); nome.Text = ""; Encher();
            Salvar();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (mexeu && e.CloseReason == CloseReason.UserClosing)
            {
                var r = MessageBox.Show(this, "Salvar as mudanças nos faróis?", "Configurar faróis", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel || (r == DialogResult.Yes && !Salvar())) { e.Cancel = true; return; }
            }
            base.OnFormClosing(e);
        }
    }
}
