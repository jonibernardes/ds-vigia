using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Automation;
using DSVigia.Nucleo;

namespace DSVigia
{
    /// <summary>
    /// De que perfil do Chrome/Edge é cada janela. O Chrome roda todos os perfis no mesmo processo, então a
    /// linha de comando não separa. Tenta, nesta ordem: (a) o AppUserModelID da janela, (b) o nome do botão
    /// de perfil da barra, (c) o --profile-directory do processo. Os títulos de guia ficam só na memória.
    /// </summary>
    public static class Perfis
    {
        public sealed class Janela
        {
            public IntPtr Hwnd;
            public string Navegador = "";   // chrome | msedge
            public string PerfilId = "";    // "" = não deu para saber
            public string PerfilNome = "";
            public string Via = "-";        // a | b | c | -
            public List<string> Guias = new List<string>();
        }

        public static readonly string[] Navegadores = { "chrome", "msedge" };

        /// <summary>Todas as janelas de navegador abertas (não só a principal de cada processo), com o perfil e as guias.</summary>
        public static List<Janela> Janelas()
        {
            var r = new List<Janela>();
            var nomes = new Dictionary<uint, string>();
            foreach (var h in JanelasDeTopo())
            {
                try
                {
                    GetWindowThreadProcessId(h, out var pid);
                    if (!nomes.TryGetValue(pid, out var proc))
                    {
                        try { proc = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant(); } catch { proc = ""; }
                        nomes[pid] = proc;
                    }
                    if (!Navegadores.Contains(proc)) continue;

                    var raiz = AutomationElement.FromHandle(h);
                    if (raiz == null) continue;
                    var j = new Janela { Hwnd = h, Navegador = proc };
                    Leitor.ColherGuias(raiz, j.Guias);
                    if (j.Guias.Count == 0) continue;   // app instalado (PWA) ou janela auxiliar: não é janela de guias

                    // outra pasta de dados (--user-data-dir): outro Chrome, com perfis próprios
                    var linha = LinhaDeComando(pid);
                    var padrao = PastaDados(proc);
                    var pasta = GuiasNav.PastaDaLinha(linha) ?? padrao;
                    bool outra = !string.Equals(pasta.TrimEnd('\\'), padrao.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                    var perfis = ListaDaPasta(pasta);
                    var pastas = perfis.Keys.ToList();
                    string id = Farois.PerfilDoAppId(AppId(h), pastas);
                    if (id != null) j.Via = "a";
                    if (id == null) { id = Farois.PerfilPeloBotao(BotoesDaBarra(raiz), perfis); if (id != null) j.Via = "b"; }
                    if (id == null) { id = Farois.PerfilDaLinha(linha); if (id != null) j.Via = "c"; }
                    if (id == null && outra) { id = "Default"; j.Via = "c"; }
                    j.PerfilId = GuiasNav.ChavePerfil(pasta, padrao, id);
                    var nome = id != null && perfis.TryGetValue(id, out var ns) && ns.Count > 0 && ns[0] != "" ? ns[0] : (id ?? "");
                    j.PerfilNome = outra && nome != "" ? nome + " (outra pasta)" : nome;
                    r.Add(j);
                }
                catch { /* janela fechou no meio */ }
            }
            return r;
        }

        // ---------- nomes amigáveis: Local State ----------

        static readonly Dictionary<string, Tuple<DateTime, Dictionary<string, List<string>>>> cache = new Dictionary<string, Tuple<DateTime, Dictionary<string, List<string>>>>();

        public static string PastaDados(string navegador)
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return navegador == "msedge" ? Path.Combine(local, "Microsoft", "Edge", "User Data") : Path.Combine(local, "Google", "Chrome", "User Data");
        }

        /// <summary>pasta do perfil → [nome no navegador, outros nomes (conta Google, atalho)]. Lê profile.info_cache do Local State.</summary>
        public static Dictionary<string, List<string>> Lista(string navegador) => ListaDaPasta(PastaDados(navegador));

        /// <summary>O mesmo, a partir da pasta de dados (a padrão ou a do --user-data-dir).</summary>
        public static Dictionary<string, List<string>> ListaDaPasta(string pastaDados)
        {
            var arq = Path.Combine(pastaDados ?? "", "Local State");
            var vazio = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(arq)) return vazio;
                var quando = File.GetLastWriteTimeUtc(arq);
                lock (cache) if (cache.TryGetValue(arq, out var c) && c.Item1 == quando) return c.Item2;

                string texto;
                using (var fs = new FileStream(arq, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs, Encoding.UTF8)) texto = sr.ReadToEnd();
                var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };
                var raiz = js.DeserializeObject(texto) as Dictionary<string, object>;
                var d = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                if (raiz != null && raiz.TryGetValue("profile", out var p) && p is Dictionary<string, object> perfil
                    && perfil.TryGetValue("info_cache", out var ic) && ic is Dictionary<string, object> info)
                {
                    foreach (var kv in info)
                    {
                        var x = kv.Value as Dictionary<string, object>;
                        var nomes = new List<string>();
                        foreach (var campo in new[] { "name", "shortcut_name", "gaia_name", "gaia_given_name" })
                            if (x != null && x.TryGetValue(campo, out var v) && v is string s && s.Trim() != "" && !nomes.Contains(s.Trim())) nomes.Add(s.Trim());
                        if (nomes.Count == 0) nomes.Add(kv.Key);
                        d[kv.Key] = nomes;
                    }
                }
                lock (cache) cache[arq] = Tuple.Create(quando, d);
                return d;
            }
            catch { return vazio; }
        }

        // ---------- (a) AppUserModelID ----------

        static string AppId(IntPtr h)
        {
            IPropertyStore ps = null;
            try
            {
                var iid = typeof(IPropertyStore).GUID;
                if (SHGetPropertyStoreForWindow(h, ref iid, out ps) != 0 || ps == null) return null;
                var chave = new PROPERTYKEY { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
                if (ps.GetValue(ref chave, out var pv) != 0) return null;
                try { return pv.vt == 31 /* VT_LPWSTR */ && pv.p != IntPtr.Zero ? Marshal.PtrToStringUni(pv.p) : null; }
                finally { PropVariantClear(ref pv); }
            }
            catch { return null; }
            finally { if (ps != null) Marshal.ReleaseComObject(ps); }
        }

        // ---------- (b) botão de perfil na barra ----------

        /// <summary>Nomes dos botões da moldura do navegador. Não entra na página (Document): só a barra.</summary>
        static List<string> BotoesDaBarra(AutomationElement raiz)
        {
            var r = new List<string>();
            try
            {
                var w = TreeWalker.ControlViewWalker;
                var fila = new Queue<KeyValuePair<AutomationElement, int>>();
                fila.Enqueue(new KeyValuePair<AutomationElement, int>(raiz, 0));
                int vistos = 0;
                while (fila.Count > 0 && vistos < 800)
                {
                    var item = fila.Dequeue(); var e = item.Key; var nivel = item.Value;
                    vistos++;
                    ControlType ct;
                    try { ct = e.Current.ControlType; } catch { continue; }
                    if (ct == ControlType.Document) continue;
                    if (ct == ControlType.Button || ct == ControlType.MenuItem)
                    {
                        string nome; try { nome = (e.Current.Name ?? "").Trim(); } catch { nome = ""; }
                        if (nome != "" && nome.Length <= 120) r.Add(nome);
                    }
                    if (nivel >= 14) continue;
                    try
                    {
                        for (var f = w.GetFirstChild(e); f != null; f = w.GetNextSibling(f))
                            fila.Enqueue(new KeyValuePair<AutomationElement, int>(f, nivel + 1));
                    }
                    catch { }
                }
            }
            catch { }
            return r;
        }

        // ---------- (c) linha de comando ----------

        static readonly Dictionary<uint, string> linhas = new Dictionary<uint, string>();

        static string LinhaDeComando(uint pid)
        {
            lock (linhas) if (linhas.TryGetValue(pid, out var l)) return l;
            string r = null;
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + pid))
                    foreach (ManagementObject o in s.Get()) { r = o["CommandLine"] as string; break; }
            }
            catch { }
            lock (linhas) { if (linhas.Count > 200) linhas.Clear(); linhas[pid] = r; }
            return r;
        }

        // ---------- Win32 ----------

        static List<IntPtr> JanelasDeTopo()
        {
            var r = new List<IntPtr>();
            EnumWindowsProc cb = (h, l) =>
            {
                if (!IsWindowVisible(h) || GetWindow(h, 4 /* GW_OWNER */) != IntPtr.Zero || GetWindowTextLength(h) == 0) return true;
                var sb = new StringBuilder(64);
                GetClassName(h, sb, sb.Capacity);
                if (sb.ToString() == "Chrome_WidgetWin_1") r.Add(h);
                return true;
            };
            EnumWindows(cb, IntPtr.Zero);
            GC.KeepAlive(cb);
            return r;
        }

        delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr h, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ps);
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT pv);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct PROPERTYKEY { public Guid fmtid; public uint pid; }

        [StructLayout(LayoutKind.Explicit)]
        struct PROPVARIANT
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr p;
            [FieldOffset(16)] public IntPtr p2;   // reserva: no x64 o PROPVARIANT tem 24 bytes
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint n);
            [PreserveSig] int GetAt(uint i, out PROPERTYKEY k);
            [PreserveSig] int GetValue(ref PROPERTYKEY k, out PROPVARIANT v);
            [PreserveSig] int SetValue(ref PROPERTYKEY k, ref PROPVARIANT v);
            [PreserveSig] int Commit();
        }
    }
}
