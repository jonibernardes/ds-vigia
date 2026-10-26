using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace DSVigia
{
    /// <summary>Guarda a sessão e o apelido do computador em %AppData%\DSVigia. Só deste usuário.</summary>
    public sealed class Estado
    {
        public string AccessToken, RefreshToken, UserId, Email, Nome, TenantId, Dispositivo, NomePC;
        public long ExpiraEm;

        static string Pasta => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DSVigia");
        static string Arq => Path.Combine(Pasta, "estado.json");

        public bool Logado => !string.IsNullOrEmpty(RefreshToken) && !string.IsNullOrEmpty(TenantId);

        public static Estado Carregar()
        {
            try
            {
                if (File.Exists(Arq))
                {
                    var e = new JavaScriptSerializer().Deserialize<Estado>(File.ReadAllText(Arq, Encoding.UTF8));
                    if (e != null) { if (string.IsNullOrEmpty(e.Dispositivo)) e.Dispositivo = Guid.NewGuid().ToString(); return e; }
                }
            }
            catch { }
            return new Estado { Dispositivo = Guid.NewGuid().ToString(), NomePC = Environment.MachineName };
        }

        public void Salvar()
        {
            try
            {
                Directory.CreateDirectory(Pasta);
                File.WriteAllText(Arq, new JavaScriptSerializer().Serialize(this), Encoding.UTF8);
            }
            catch { }
        }

        public void Limpar() { AccessToken = RefreshToken = UserId = Email = Nome = TenantId = null; ExpiraEm = 0; Salvar(); }
    }
}
