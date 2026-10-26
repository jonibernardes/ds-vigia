using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using DSVigia.Nucleo;

namespace DSVigia
{
    /// <summary>
    /// Conversa com o sistema (Supabase REST), com o login do próprio usuário. Manda só os números e o
    /// sinal de vida do computador; busca as regras de leitura. Igual à extensão aprovada, em programa nativo.
    /// </summary>
    public sealed class Enviador
    {
        const string URL = "https://lfjlnpwkqmqwdiydzixu.supabase.co";
        const string ANON = "sb_publishable_cSRZuvKzCHmqsTJZgYsAHA_s8_CMxhI";
        const string VERSAO = "1.0.0";

        readonly Estado e;
        static readonly JavaScriptSerializer J = new JavaScriptSerializer();

        public Enviador(Estado est) { e = est; ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; }

        public string Entrar(string email, string senha)
        {
            var s = (Dictionary<string, object>)J.DeserializeObject(Http("/auth/v1/token?grant_type=password", "POST",
                J.Serialize(new { email, password = senha }), null));
            var user = (Dictionary<string, object>)s["user"];
            var uid = (string)user["id"];
            var token = (string)s["access_token"];
            var perfil = (object[])J.DeserializeObject(Http("/rest/v1/profiles?select=tenant_id,full_name,status&user_id=eq." + uid, "GET", null, token));
            if (perfil.Length == 0) throw new Exception("Usuário sem escritório no sistema.");
            var p = (Dictionary<string, object>)perfil[0];
            if ((string)p["status"] != "aprovado") throw new Exception("Usuário ainda não aprovado no sistema.");
            e.AccessToken = token; e.RefreshToken = (string)s["refresh_token"]; e.ExpiraEm = Convert.ToInt64(s["expires_at"]);
            e.UserId = uid; e.Email = email; e.Nome = p["full_name"] as string; e.TenantId = (string)p["tenant_id"];
            e.Salvar();
            return e.Nome;
        }

        string TokenValido()
        {
            if (string.IsNullOrEmpty(e.RefreshToken)) return null;
            if (e.ExpiraEm - DateTimeOffset.UtcNow.ToUnixTimeSeconds() > 120) return e.AccessToken;
            try
            {
                var s = (Dictionary<string, object>)J.DeserializeObject(Http("/auth/v1/token?grant_type=refresh_token", "POST",
                    J.Serialize(new { refresh_token = e.RefreshToken }), null));
                e.AccessToken = (string)s["access_token"]; e.RefreshToken = (string)s["refresh_token"]; e.ExpiraEm = Convert.ToInt64(s["expires_at"]);
                e.Salvar();
                return e.AccessToken;
            }
            catch { e.Limpar(); return null; }
        }

        /// <summary>Grava os números no sistema. Cada linha é um canal; só número, não conteúdo.</summary>
        public void Enviar(IEnumerable<Canal> canais, DateTime agora)
        {
            var token = TokenValido();
            if (token == null) return;
            var linhas = canais.Select(c => new Dictionary<string, object> {
                { "tenant_id", e.TenantId }, { "user_id", e.UserId }, { "dispositivo", e.Dispositivo },
                { "chave", c.Chave }, { "nome", c.Nome }, { "sistema", c.Chave },
                { "pendentes", c.Aberto ? (object)c.Pendentes : null },
                { "pendente_desde", c.Aberto && c.PendenteDesde.HasValue ? (object)c.PendenteDesde.Value.ToString("o") : null },
                { "aberto", c.Aberto }, { "atualizado_em", agora.ToString("o") },
            }).ToList();
            if (linhas.Count == 0) return;
            try
            {
                Http("/rest/v1/at_canais?on_conflict=user_id,dispositivo,chave", "POST", J.Serialize(linhas), token,
                     prefer: "resolution=merge-duplicates,return=minimal");
                Diag("envio ok " + linhas.Count + " linhas " + DateTime.UtcNow.ToString("o"));
            }
            catch (Exception ex) { Diag("ENVIO ERRO: " + ex.Message); throw; }
        }

        /// <summary>Sinal de vida do computador: versão e última leitura, para o diagnóstico remoto (ideia 10).</summary>
        public void BaterPonto(DateTime agora, string obs)
        {
            var token = TokenValido();
            if (token == null) return;
            var linha = new[] { new Dictionary<string, object> {
                { "tenant_id", e.TenantId }, { "user_id", e.UserId }, { "dispositivo", e.Dispositivo },
                { "nome_pc", e.NomePC }, { "versao", VERSAO }, { "visto_em", agora.ToString("o") }, { "obs", obs },
            }};
            try { Http("/rest/v1/at_dispositivos?on_conflict=dispositivo", "POST", J.Serialize(linha), token, prefer: "resolution=merge-duplicates,return=minimal"); }
            catch { }
        }

        /// <summary>Busca as regras do tenant; se não houver nenhuma, usa as embutidas.</summary>
        public List<Regra> Regras()
        {
            try
            {
                var token = TokenValido();
                if (token == null) return RegrasPadrao.Todas();
                var arr = (object[])J.DeserializeObject(Http(
                    "/rest/v1/at_regras?select=id,chave,nome,metodo,processo,janela,controle,rotulo,app,tipo_notificacao,regiao,ativo,ordem&tenant_id=eq." + e.TenantId + "&ativo=eq.true&order=ordem", "GET", null, token));
                if (arr.Length == 0) return RegrasPadrao.Todas();
                return arr.Select(o => { var d = (Dictionary<string, object>)o; return new Regra {
                    Id = S(d, "id"), Chave = S(d, "chave"), Nome = S(d, "nome"), Metodo = S(d, "metodo"),
                    Processo = S(d, "processo"), Janela = S(d, "janela"), Controle = S(d, "controle"), Rotulo = S(d, "rotulo"),
                    App = S(d, "app"), TipoNotificacao = S(d, "tipo_notificacao"), Regiao = S(d, "regiao"),
                    Ativo = d.ContainsKey("ativo") && Convert.ToBoolean(d["ativo"]),
                    Ordem = d.ContainsKey("ordem") && d["ordem"] != null ? Convert.ToInt32(d["ordem"]) : 0,
                }; }).ToList();
            }
            catch { return RegrasPadrao.Todas(); }
        }

        static void Diag(string txt)
        {
            try
            {
                var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DSVigia");
                if (File.Exists(Path.Combine(pasta, "debug"))) File.AppendAllText(Path.Combine(pasta, "ultimo-envio.txt"), txt + "\r\n");
            }
            catch { }
        }

        static string S(Dictionary<string, object> d, string k) => d.ContainsKey(k) && d[k] != null ? d[k].ToString() : "";

        string Http(string caminho, string metodo, string corpo, string token, string prefer = null)
        {
            var req = (HttpWebRequest)WebRequest.Create(URL + caminho);
            req.Method = metodo; req.Timeout = 20000; req.Headers["apikey"] = ANON;
            if (token != null) req.Headers["Authorization"] = "Bearer " + token;
            if (prefer != null) req.Headers["Prefer"] = prefer;
            if (corpo != null) { req.ContentType = "application/json"; var b = Encoding.UTF8.GetBytes(corpo); req.ContentLength = b.Length; using (var st = req.GetRequestStream()) st.Write(b, 0, b.Length); }
            try { using (var resp = (HttpWebResponse)req.GetResponse()) using (var r = new StreamReader(resp.GetResponseStream())) return r.ReadToEnd(); }
            catch (WebException ex)
            {
                string msg = ex.Message;
                if (ex.Response is HttpWebResponse hr) using (var r = new StreamReader(hr.GetResponseStream())) msg = r.ReadToEnd();
                throw new Exception(Resumo(msg));
            }
        }

        static string Resumo(string json)
        {
            try { var d = J.DeserializeObject(json) as Dictionary<string, object>; if (d != null) foreach (var k in new[] { "msg", "message", "error_description", "error" }) if (d.ContainsKey(k) && d[k] != null) return d[k].ToString(); }
            catch { }
            return json.Length > 160 ? json.Substring(0, 160) : json;
        }
    }
}
