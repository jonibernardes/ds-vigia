using System;
using System.Windows.Forms;

namespace DSVigia
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool novo;
            using (var mutex = new System.Threading.Mutex(true, "DSVigia_unico_" + Environment.UserName, out novo))
            {
                if (!novo) return; // já está rodando para este usuário
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var app = new AplicacaoBandeja())
                    Application.Run();
            }
        }
    }
}
