using System;
using System.Drawing;
using System.Windows.Forms;

namespace DSVigia
{
    /// <summary>Login do usuário do sistema. A senha vai direto ao sistema; o vigia não a guarda.</summary>
    public sealed class JanelaLogin : Form
    {
        readonly TextBox email, senha;
        public string Email => email.Text.Trim();
        public string Senha => senha.Text;

        public JanelaLogin(string emailAtual)
        {
            Text = "Entrar — DS Vigia"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(340, 190); Font = new Font("Segoe UI", 9.5f);
            Controls.Add(new Label { Text = "Use o mesmo login do sistema DS Soluções.", Top = 14, Left = 16, Width = 310, ForeColor = Color.DimGray });
            Controls.Add(new Label { Text = "E-mail", Top = 46, Left = 16, AutoSize = true });
            email = new TextBox { Top = 64, Left = 16, Width = 308, Text = emailAtual }; Controls.Add(email);
            Controls.Add(new Label { Text = "Senha", Top = 96, Left = 16, AutoSize = true });
            senha = new TextBox { Top = 114, Left = 16, Width = 308, UseSystemPasswordChar = true }; Controls.Add(senha);
            var ok = new Button { Text = "Entrar", DialogResult = DialogResult.OK, Top = 150, Left = 168, Width = 76 };
            var ca = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Top = 150, Left = 248, Width = 76 };
            Controls.Add(ok); Controls.Add(ca); AcceptButton = ok; CancelButton = ca;
        }
    }

    public sealed class JanelaTexto : Form
    {
        readonly TextBox cx;
        public string Valor => cx.Text;
        public JanelaTexto(string titulo, string rotulo, string valor)
        {
            Text = titulo; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(340, 130); Font = new Font("Segoe UI", 9.5f);
            Controls.Add(new Label { Text = rotulo, Top = 16, Left = 16, Width = 308 });
            cx = new TextBox { Top = 48, Left = 16, Width = 308, Text = valor }; Controls.Add(cx);
            var ok = new Button { Text = "Salvar", DialogResult = DialogResult.OK, Top = 88, Left = 168, Width = 76 };
            var ca = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Top = 88, Left = 248, Width = 76 };
            Controls.Add(ok); Controls.Add(ca); AcceptButton = ok; CancelButton = ca;
        }
    }
}
