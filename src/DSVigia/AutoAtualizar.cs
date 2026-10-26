using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using DSVigia.Nucleo;

namespace DSVigia
{
    /// <summary>
    /// Atualização sozinha (ideia 9/10): olha a última versão publicada nas Releases do repositório e, se for
    /// maior, baixa o novo .exe ao lado do atual e reinicia. Sem instalador, sem pedir nada ao usuário.
    /// </summary>
    public static class AutoAtualizar
    {
        const string RELEASE = "https://api.github.com/repos/jonibernardes/ds-vigia/releases/latest";
        public const string Versao = "1.0.0";

        public static void Verificar()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(RELEASE);
                req.UserAgent = "DSVigia"; req.Timeout = 15000; req.Accept = "application/vnd.github+json";
                string json;
                using (var r = new StreamReader(((HttpWebResponse)req.GetResponse()).GetResponseStream())) json = r.ReadToEnd();
                var rel = (System.Collections.Generic.Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
                var tag = Regex.Replace((rel["tag_name"] ?? "").ToString(), "[^0-9.]", "");
                if (DSVigia.Nucleo.Versao.Comparar(tag, Versao) <= 0) return;

                string url = null;
                foreach (var a in (object[])rel["assets"])
                {
                    var d = (System.Collections.Generic.Dictionary<string, object>)a;
                    if ((d["name"] ?? "").ToString().EndsWith("DSVigia.exe", StringComparison.OrdinalIgnoreCase)) { url = (string)d["browser_download_url"]; break; }
                }
                if (url == null) return;

                var atual = Process.GetCurrentProcess().MainModule.FileName;
                var novo = atual + ".novo";
                using (var w = new WebClient()) { w.Headers["User-Agent"] = "DSVigia"; w.DownloadFile(url, novo); }
                // troca na próxima abertura: um .bat espera este processo sair, renomeia e reabre
                var bat = Path.Combine(Path.GetTempPath(), "dsvigia-troca.bat");
                File.WriteAllText(bat,
                    "@echo off\r\n:esp\r\nping -n 2 127.0.0.1 >nul\r\n" +
                    "tasklist /fi \"imagename eq DSVigia.exe\" | find /i \"DSVigia.exe\" >nul && goto esp\r\n" +
                    "move /y \"" + novo + "\" \"" + atual + "\" >nul\r\n" +
                    "start \"\" \"" + atual + "\"\r\n");
                Process.Start(new ProcessStartInfo { FileName = bat, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true });
                Application_Exit();
            }
            catch { /* sem internet ou sem release: segue na versão atual */ }
        }

        static void Application_Exit() { try { System.Windows.Forms.Application.Exit(); } catch { } }
    }
}
