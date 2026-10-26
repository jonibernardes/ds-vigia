using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DSVigia.Nucleo;

namespace DSVigia
{
    /// <summary>
    /// Lê o banco de notificações do Windows (wpndatabase.db) só para pegar o NÚMERO do ícone de cada
    /// programa (o "badge", ex.: WhatsApp com 5) e a hora da notificação mais antiga ainda visível.
    /// Abre o arquivo em modo somente leitura. O texto das notificações nunca é lido nem enviado.
    /// </summary>
    public static class Notificacoes
    {
        static string Caminho => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Notifications", "wpndatabase.db");

        public static int? Badge(string appContem)
        {
            if (string.IsNullOrEmpty(appContem) || !File.Exists(Caminho)) return null;
            try
            {
                var sql = "select n.Payload from Notification n " +
                          "join NotificationHandler h on h.RecordId=n.HandlerId " +
                          "where n.Type='badge' and h.PrimaryId like '%" + Limpar(appContem) + "%' " +
                          "order by n.ArrivalTime desc limit 1";
                int? r = null;
                Sqlite.Consultar(Caminho, sql, cols => { r = Contagem.DoBadge(cols[0]); });
                return r;
            }
            catch { return null; }
        }

        public static DateTime? MaisAntigaNaoLida(string appContem)
        {
            if (string.IsNullOrEmpty(appContem) || !File.Exists(Caminho)) return null;
            try
            {
                var sql = "select min(n.ArrivalTime) from Notification n " +
                          "join NotificationHandler h on h.RecordId=n.HandlerId " +
                          "where n.Type='toast' and h.PrimaryId like '%" + Limpar(appContem) + "%'";
                DateTime? r = null;
                Sqlite.Consultar(Caminho, sql, cols => {
                    if (long.TryParse(cols[0], out var ft)) r = Contagem.DeFileTime(ft);
                });
                return r;
            }
            catch { return null; }
        }

        static string Limpar(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s) if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-') sb.Append(c);
            return sb.ToString();
        }
    }

    /// <summary>Acesso mínimo ao winsqlite3.dll, que já vem no Windows 10/11. Só leitura.</summary>
    internal static class Sqlite
    {
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)] static extern int Abrir(byte[] f, out IntPtr db, int flags, IntPtr vfs);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)] static extern int Preparar(IntPtr db, byte[] sql, int n, out IntPtr st, IntPtr tail);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)] static extern int Passo(IntPtr st);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr Texto(IntPtr st, int i);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_count", CallingConvention = CallingConvention.Cdecl)] static extern int Colunas(IntPtr st);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)] static extern int Encerrar(IntPtr st);
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)] static extern int Fechar(IntPtr db);

        const int ABRIR_RO = 0x01, ABRIR_URI = 0x40;

        public static void Consultar(string arquivo, string sql, Action<string[]> porLinha)
        {
            var uri = "file:" + arquivo.Replace("\\", "/") + "?mode=ro&immutable=1";
            if (Abrir(Encoding.UTF8.GetBytes(uri + "\0"), out var db, ABRIR_RO | ABRIR_URI, IntPtr.Zero) != 0) { Fechar(db); return; }
            try
            {
                if (Preparar(db, Encoding.UTF8.GetBytes(sql + "\0"), -1, out var st, IntPtr.Zero) != 0) return;
                int n = Colunas(st);
                while (Passo(st) == 100)
                {
                    var linha = new string[n];
                    for (int i = 0; i < n; i++) linha[i] = Ler(Texto(st, i));
                    porLinha(linha);
                }
                Encerrar(st);
            }
            finally { Fechar(db); }
        }

        static string Ler(IntPtr p)
        {
            if (p == IntPtr.Zero) return "";
            var bytes = new List<byte>();
            int i = 0; byte b;
            while ((b = Marshal.ReadByte(p, i++)) != 0) bytes.Add(b);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
    }
}
