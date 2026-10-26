using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DSVigia.Nucleo;

namespace DSVigia
{
    /// <summary>A barrinha sempre por cima: um sinal por sistema, com o número e o tempo de espera (ideia 4).</summary>
    public sealed class Sinaleiro : Form
    {
        readonly FlowLayoutPanel faixa;
        Point arraste; bool arrastando;

        public Sinaleiro()
        {
            Text = "Sinaleiro DS"; FormBorderStyle = FormBorderStyle.None; TopMost = true;
            ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; BackColor = Color.FromArgb(15, 46, 46);
            Height = 92; Width = 360; MinimumSize = new Size(140, 92);
            var ta = Screen.PrimaryScreen.WorkingArea; Location = new Point(ta.Right - Width - 16, ta.Bottom - Height - 16);

            var topo = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Color.FromArgb(10, 33, 33), Cursor = Cursors.SizeAll };
            var x = new Label { Text = "✕", ForeColor = Color.White, AutoSize = true, Location = new Point(Width - 18, 1), Cursor = Cursors.Hand };
            x.Click += (s, e) => Hide();
            topo.Controls.Add(x);
            topo.MouseDown += (s, e) => { arrastando = true; arraste = e.Location; };
            topo.MouseMove += (s, e) => { if (arrastando) Location = new Point(Location.X + e.X - arraste.X, Location.Y + e.Y - arraste.Y); };
            topo.MouseUp += (s, e) => arrastando = false;

            faixa = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(6, 4, 6, 4), AutoScroll = false };
            Controls.Add(faixa); Controls.Add(topo);
        }

        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000; return p; } } // WS_EX_NOACTIVATE

        static readonly Dictionary<Cor, Color> C = new Dictionary<Cor, Color> {
            { Cor.Verde, Color.FromArgb(18,161,80) }, { Cor.Amarelo, Color.FromArgb(245,183,10) },
            { Cor.Vermelho, Color.FromArgb(217,45,32) }, { Cor.Cinza, Color.FromArgb(120,135,135) } };

        public void Mostrar(List<Canal> canais, DateTime agora)
        {
            if (IsDisposed || !Visible) return;
            var ordem = new Dictionary<Cor, int> { { Cor.Vermelho, 0 }, { Cor.Amarelo, 1 }, { Cor.Verde, 2 }, { Cor.Cinza, 3 } };
            var lista = canais.OrderBy(c => ordem[c.CorEm(agora)]).ThenByDescending(c => c.Minutos(agora)).ThenBy(c => c.Nome).ToList();
            faixa.SuspendLayout(); faixa.Controls.Clear();
            if (lista.Count == 0)
                faixa.Controls.Add(new Label { Text = "Nenhum sistema aberto", ForeColor = Color.Gainsboro, AutoSize = true, Margin = new Padding(6, 24, 6, 0) });
            foreach (var c in lista)
            {
                var cor = c.CorEm(agora);
                var cel = new Panel { Width = 76, Height = 64, Margin = new Padding(2) };
                var bola = new Label {
                    Text = cor == Cor.Cinza ? "–" : (c.Pendentes ?? 0).ToString(), Width = 32, Height = 32,
                    TextAlign = ContentAlignment.MiddleCenter, ForeColor = cor == Cor.Amarelo ? Color.FromArgb(29,43,43) : Color.White,
                    BackColor = C[cor], Font = new Font("Segoe UI", 12, FontStyle.Bold), Left = 22, Top = 0 };
                bola.Paint += Arredondar(bola);
                var nome = new Label { Text = c.Nome, ForeColor = Color.White, Font = new Font("Segoe UI", 7.5f), Width = 76, Height = 14, Top = 34, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true };
                var apoio = new Label { Text = cor == Cor.Cinza ? "sem sinal" : (c.Pendentes > 0 ? Canal.TextoEspera(c.Minutos(agora)) : "em dia"),
                    ForeColor = cor == Cor.Vermelho ? Color.FromArgb(255,180,171) : Color.FromArgb(138,222,220), Font = new Font("Segoe UI", 7), Width = 76, Height = 12, Top = 48, TextAlign = ContentAlignment.MiddleCenter };
                cel.Controls.Add(bola); cel.Controls.Add(nome); cel.Controls.Add(apoio);
                faixa.Controls.Add(cel);
            }
            faixa.ResumeLayout();
            Width = Math.Min(Math.Max(140, lista.Count * 82 + 16), Screen.PrimaryScreen.WorkingArea.Width - 40);
        }

        static PaintEventHandler Arredondar(Control c) => (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(c.BackColor)) e.Graphics.FillEllipse(b, 0, 0, c.Width - 1, c.Height - 1);
            TextRenderer.DrawText(e.Graphics, c.Text, c.Font, c.ClientRectangle, c.ForeColor, Color.Transparent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
    }
}
