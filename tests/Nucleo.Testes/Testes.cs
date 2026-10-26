using System;
using System.Collections.Generic;
using System.Linq;
using DSVigia.Nucleo;
using Xunit;

public class ContagemTestes
{
    static string R(string id) => RegrasPadrao.Todas().First(r => r.Id == id).Rotulo;

    [Fact] public void Guia_do_whatsapp_com_numero() =>
        Assert.Equal(7, Contagem.PorRotulo(new[] { "Nova guia", "(7) WhatsApp", "Gmail" }, R("p-wa-web")));

    [Fact] public void Guia_do_whatsapp_aberta_sem_conversa_e_zero() =>
        Assert.Equal(0, Contagem.PorRotulo(new[] { "WhatsApp" }, R("p-wa-web")));

    [Fact] public void Sem_guia_do_whatsapp_e_null() =>
        Assert.Null(Contagem.PorRotulo(new[] { "Gmail", "Nova guia" }, R("p-wa-web")));

    // Gestta/NP agora são pela GUIA (título do site), não por texto solto da página
    [Fact] public void Gestta_pela_guia_com_numero() => Assert.Equal(3, Contagem.PorRotulo(new[] { "DS - Gestta (3)" }, R("p-gestta")));
    [Fact] public void Gestta_pela_guia_sem_numero() => Assert.Equal(0, Contagem.PorRotulo(new[] { "Gestta - Mensagens" }, R("p-gestta")));
    [Fact] public void Texto_solto_pendentes_nao_vira_gestta() => Assert.Null(Contagem.PorRotulo(new[] { "Pendentes 12" }, R("p-gestta")));
    [Fact] public void Np_connect() => Assert.Equal(4, Contagem.PorRotulo(new[] { "NP Connect - Atendimento (4)" }, R("p-np")));
    [Fact] public void Gmail() => Assert.Equal(5, Contagem.PorRotulo(new[] { "Caixa de entrada (5) - joni@x.com - Gmail" }, R("p-email-web")));
    [Fact] public void Telegram() { Assert.Equal(3, Contagem.PorRotulo(new[] { "Telegram (3)" }, R("p-tg"))); Assert.Equal(0, Contagem.PorRotulo(new[] { "Telegram" }, R("p-tg"))); }

    [Fact] public void Guia_do_gestta_vale_o_maior() =>
        Assert.Equal(12, Contagem.PorRotulo(new[] { "Gestta (3)", "Gestta - Mensagens (12)" }, R("p-gestta")));

    [Fact] public void Rotulo_sem_numero_conta_ocorrencias() =>
        Assert.Equal(2, Contagem.PorRotulo(new[] { "não lida", "lida", "Não lida" }, @"^não lida$"));

    [Fact] public void Texto_longo_e_ignorado() =>
        Assert.Null(Contagem.PorRotulo(new[] { new string('a', 200) + " Pendentes 3" }, @"Pendentes (\d+)"));

    [Fact] public void Regex_quebrada_nao_derruba() => Assert.Null(Contagem.PorRotulo(new[] { "x" }, "(((("));

    [Theory]
    [InlineData("<badge value=\"5\"/>", 5)]
    [InlineData("<badge value=\"none\"/>", 0)]
    [InlineData("<badge value=\"alert\"/>", 1)]
    public void Badge(string xml, int esperado) => Assert.Equal(esperado, Contagem.DoBadge(xml));

    [Fact] public void Badge_vazio_e_null() => Assert.Null(Contagem.DoBadge(""));

    [Fact] public void Titulo() { Assert.Equal(3, Contagem.PeloTitulo("Telegram (3)")); Assert.Equal(0, Contagem.PeloTitulo("Telegram")); }

    [Fact] public void Juntar_vale_o_maior_e_null_se_ninguem_achou()
    {
        Assert.Equal(4, Contagem.Juntar(new int?[] { null, 2, 4 }));
        Assert.Null(Contagem.Juntar(new int?[] { null, null }));
        Assert.Equal(0, Contagem.Juntar(new int?[] { null, 0 }));
    }
}

public class CanalTestes
{
    static readonly DateTime T0 = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact] public void Fica_amarelo_e_depois_vermelho_aos_10_min()
    {
        var c = new Canal();
        Assert.True(c.Atualizar(2, T0));
        Assert.Equal(Cor.Amarelo, c.CorEm(T0.AddMinutes(9)));
        Assert.Equal(Cor.Vermelho, c.CorEm(T0.AddMinutes(10)));
        Assert.True(c.PrecisaAvisar(T0.AddMinutes(10)));
    }

    [Fact] public void Zerou_volta_verde_e_limpa_o_tempo()
    {
        var c = new Canal(); c.Atualizar(2, T0); c.Notificado = true;
        Assert.True(c.Atualizar(0, T0.AddMinutes(12)));
        Assert.Equal(Cor.Verde, c.CorEm(T0.AddMinutes(12)));
        Assert.Null(c.PendenteDesde); Assert.False(c.Notificado);
    }

    [Fact] public void Fechou_o_programa_fica_cinza()
    {
        var c = new Canal(); c.Atualizar(1, T0);
        Assert.True(c.Atualizar(null, T0.AddMinutes(1)));
        Assert.Equal(Cor.Cinza, c.CorEm(T0));
    }

    [Fact] public void Mesma_leitura_nao_muda()
    {
        var c = new Canal(); c.Atualizar(3, T0);
        Assert.False(c.Atualizar(3, T0.AddMinutes(1)));
        Assert.Equal(T0, c.PendenteDesde);
    }

    [Fact] public void Usa_a_notificacao_mais_antiga_quando_o_windows_informa()
    {
        var c = new Canal();
        c.Atualizar(1, T0, T0.AddMinutes(-15));
        Assert.Equal(Cor.Vermelho, c.CorEm(T0));
        Assert.Equal(15, c.Minutos(T0));
    }

    [Fact] public void Hora_do_futuro_nao_vale() { var c = new Canal(); c.Atualizar(1, T0, T0.AddMinutes(5)); Assert.Equal(T0, c.PendenteDesde); }

    [Fact] public void Texto_espera()
    {
        Assert.Equal("agora", Canal.TextoEspera(0));
        Assert.Equal("12 min", Canal.TextoEspera(12));
        Assert.Equal("1 h 5 min", Canal.TextoEspera(65));
        Assert.Equal("2 h", Canal.TextoEspera(120));
    }
}

public class OutrosTestes
{
    [Fact] public void Versao() { Assert.True(DSVigia.Nucleo.Versao.Comparar("1.2.10", "1.2.9") > 0); Assert.Equal(0, DSVigia.Nucleo.Versao.Comparar("1.0", "1.0.0")); }

    [Fact] public void Imap_busca() => Assert.Equal(new[] { 4, 7, 9 }, ImapTexto.Busca(new[] { "* SEARCH 4 7 9", "A3 OK SEARCH completed" }).ToArray());

    [Fact] public void Imap_busca_vazia() => Assert.Empty(ImapTexto.Busca(new[] { "* SEARCH", "A3 OK" }));

    [Fact] public void Imap_data() =>
        Assert.Equal(new DateTime(2026, 10, 24, 12, 15, 2, DateTimeKind.Utc), ImapTexto.DataInterna(new[] { "* 4 FETCH (INTERNALDATE \"24-Oct-2026 09:15:02 -0300\")" }));

    [Fact] public void Imap_aspas() => Assert.Equal("\"a\\\"b\\\\c\"", ImapTexto.Aspas("a\"b\\c"));

    [Fact] public void Regras_padrao_compilam()
    {
        foreach (var r in RegrasPadrao.Todas().Where(r => r.Rotulo != ""))
            Assert.NotNull(Contagem.Re(r.Rotulo));
    }
}

public class FaroisTestes
{
    static Farol Wa(string id, string nome, string perfil) =>
        new Farol { Id = id, Nome = nome, Tipo = "navegador", Navegador = "chrome", PerfilId = perfil, PerfilNome = perfil, RegraChave = "whatsapp" };

    static GuiaVista G(string perfil, string titulo, string nav = "chrome") => new GuiaVista { Navegador = nav, PerfilId = perfil, Titulo = titulo };

    [Fact] public void Json_ida_e_volta()
    {
        var antes = new List<Farol> {
            Wa("a1", "WhatsApp Cliente 1", "Profile 1"),
            new Farol { Id = "b2", Nome = "Interno \"sala\" \\ ç", Tipo = "app", RegraChave = "whatsapp", App = "WhatsAppDesktop" },
        };
        var depois = Farois.Carregar(Farois.Salvar(antes));
        Assert.Equal(2, depois.Count);
        Assert.Equal("WhatsApp Cliente 1", depois[0].Nome); Assert.Equal("Profile 1", depois[0].PerfilId); Assert.Equal("chrome", depois[0].Navegador);
        Assert.Equal("Interno \"sala\" \\ ç", depois[1].Nome); Assert.Equal("app", depois[1].Tipo); Assert.Equal("WhatsAppDesktop", depois[1].App);
        Assert.Equal("b2", depois[1].Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("isto não é json")]
    [InlineData("{\"farois\": [ {\"Id\": \"x\", ")]
    [InlineData("[1, 2, 3]")]
    [InlineData("null")]
    public void Json_invalido_vira_lista_vazia(string json) => Assert.Empty(Farois.Carregar(json));

    [Fact] public void No_maximo_8()
    {
        var muitos = Enumerable.Range(1, 11).Select(i => Wa("id" + i, "F" + i, "Profile " + i)).ToList();
        Assert.Equal(8, Farois.Carregar(Farois.Salvar(muitos)).Count);
        var json = "[" + string.Join(",", Enumerable.Range(1, 11).Select(i => "{\"Id\":\"" + i + "\",\"Nome\":\"F" + i + "\",\"RegraChave\":\"whatsapp\"}")) + "]";
        Assert.Equal(8, Farois.Carregar(json).Count);
        Assert.Equal(8, Farois.Ler(muitos, RegrasPadrao.Todas(), new List<GuiaVista>(), new List<LeituraApp>()).Count);
    }

    [Fact] public void Dois_whatsapp_em_perfis_diferentes_contam_separado()
    {
        var farois = new List<Farol> { Wa("c1", "WhatsApp Cliente 1", "Profile 1"), Wa("in", "WhatsApp Interno", "Default") };
        var guias = new List<GuiaVista> { G("Profile 1", "(3) WhatsApp"), G("Default", "(1) WhatsApp"), G("Default", "Gmail") };
        var r = Farois.Ler(farois, RegrasPadrao.Todas(), guias, new List<LeituraApp>());
        Assert.Equal(new int?[] { 3, 1 }, r.Select(x => x.Pendentes).ToArray());
        Assert.Equal("farol:c1", r[0].Chave); Assert.Equal("WhatsApp Cliente 1", r[0].Nome);
        Assert.Equal("farol:in", r[1].Chave); Assert.Equal("WhatsApp Interno", r[1].Nome);
    }

    [Fact] public void Farol_de_perfil_sem_guia_aberta_e_fechado()
    {
        var r = Farois.Ler(new List<Farol> { Wa("c1", "WhatsApp Cliente 1", "Profile 2") }, RegrasPadrao.Todas(),
            new List<GuiaVista> { G("Profile 1", "(3) WhatsApp"), G("Profile 2", "Gmail") }, new List<LeituraApp>());
        Assert.Single(r); Assert.Null(r[0].Pendentes);
    }

    [Fact] public void Mesmo_perfil_em_outro_navegador_nao_conta()
    {
        var r = Farois.Ler(new List<Farol> { Wa("c1", "W", "Default") }, RegrasPadrao.Todas(), new List<GuiaVista> { G("Default", "(5) WhatsApp", "msedge") }, null);
        Assert.Null(r[0].Pendentes);
    }

    [Fact] public void Gestta_continua_presenca()
    {
        var f = new Farol { Id = "g", Nome = "Gestta", Navegador = "chrome", PerfilId = "Default", RegraChave = "gestta" };
        var r = Farois.Ler(new List<Farol> { f }, RegrasPadrao.Todas(), new List<GuiaVista> { G("Default", "Gestta - Mensagens"), G("Default", "Pendentes 12") }, null);
        Assert.Equal(0, r[0].Pendentes);
    }

    [Fact] public void Farol_de_app_usa_badge_e_janela()
    {
        var f = new Farol { Id = "w", Nome = "WhatsApp do PC", Tipo = "app", RegraChave = "whatsapp", App = "WhatsAppDesktop" };
        var apps = new List<LeituraApp> {
            new LeituraApp { RegraChave = "whatsapp", App = "WhatsAppDesktop", Pendentes = 2 },
            new LeituraApp { RegraChave = "whatsapp", App = "", Pendentes = 4 },
            new LeituraApp { RegraChave = "telegram", App = "TelegramDesktop", Pendentes = 9 },
        };
        Assert.Equal(4, Farois.Ler(new List<Farol> { f }, RegrasPadrao.Todas(), null, apps)[0].Pendentes);
        Assert.Null(Farois.Ler(new List<Farol> { f }, RegrasPadrao.Todas(), null, new List<LeituraApp>())[0].Pendentes);
    }

    [Fact] public void Sem_farois_e_o_automatico()
    {
        // sem faróis o núcleo devolve null e o leitor segue no automático, que continua igual
        Assert.Null(Farois.Ler(new List<Farol>(), RegrasPadrao.Todas(), new List<GuiaVista> { G("Default", "(3) WhatsApp") }, null));
        Assert.Null(Farois.Ler(null, RegrasPadrao.Todas(), null, null));
        var janelas = new Dictionary<string, List<string>> { { "Telegram", new List<string> { "Telegram (2)" } } };
        var guias = new Dictionary<string, List<string>> { { "chrome", new List<string> { "(3) WhatsApp", "(1) WhatsApp", "DS - Gestta (4)" } } };
        var auto = Leituras.Automatico(RegrasPadrao.Todas(), janelas, guias, app => app == "WhatsAppDesktop" ? 5 : (int?)null);
        Assert.Equal(5, auto.Single(l => l.Chave == "whatsapp").Pendentes);   // vale o maior entre guia e app
        Assert.Equal(2, auto.Single(l => l.Chave == "telegram").Pendentes);
        Assert.Equal(4, auto.Single(l => l.Chave == "gestta").Pendentes);
        Assert.DoesNotContain(auto, l => l.Chave == "email" || l.Chave == "npconnect" || l.Chave.StartsWith("farol:"));
    }

    [Fact] public void Reconhece_sistemas_por_perfil_sem_titulo()
    {
        var rec = Farois.Reconhecer(RegrasPadrao.Todas(), new List<GuiaVista> { G("Profile 1", "(3) WhatsApp"), G("Profile 1", "Gestta - Mensagens"), G("Default", "Conversa com Fulano"), G("", "(2) WhatsApp") });
        Assert.Equal(new[] { "Gestta", "WhatsApp Web" }, rec.Select(r => r.Sistema).OrderBy(x => x).ToArray());
        Assert.All(rec, r => Assert.Equal("Profile 1", r.PerfilId));
    }

    [Theory]
    [InlineData("Chrome", "Default")]
    [InlineData("Chrome.UserData.Profile1", "Profile 1")]
    [InlineData("MSEdge.UserData.Profile3", "Profile 3")]
    [InlineData("Chrome.ABCDEF.UserData.Trabalho", "Trabalho")]
    public void Perfil_pelo_app_id(string aumid, string esperado) =>
        Assert.Equal(esperado, Farois.PerfilDoAppId(aumid, new[] { "Default", "Profile 1", "Trabalho" }));

    [Fact] public void Perfil_pelo_app_id_vazio_ou_de_app_instalado_e_null()
    {
        Assert.Null(Farois.PerfilDoAppId("", new[] { "Default" }));
        Assert.Null(Farois.PerfilDoAppId("Chrome._crx_hnpfjngllnobngcgfapefoaidbinmjnm.UserData.Default", new[] { "Default" }));
    }

    [Fact] public void Perfil_pelo_botao_e_pela_linha()
    {
        var nomes = new Dictionary<string, List<string>> { { "Default", new List<string> { "Joni" } }, { "Profile 1", new List<string> { "Joni Trabalho" } } };
        Assert.Equal("Profile 1", Farois.PerfilPeloBotao(new[] { "Voltar", "Joni Trabalho" }, nomes));
        Assert.Equal("Default", Farois.PerfilPeloBotao(new[] { "Joni" }, nomes));
        Assert.Null(Farois.PerfilPeloBotao(new[] { "Recarregar" }, nomes));
        Assert.Equal("Profile 2", Farois.PerfilDaLinha("\"C:\\chrome.exe\" --profile-directory=\"Profile 2\" --flag"));
        Assert.Equal("Default", Farois.PerfilDaLinha("chrome.exe --profile-directory=Default"));
        Assert.Null(Farois.PerfilDaLinha("chrome.exe --type=renderer"));
    }
}

public class GuiasNavTestes
{
    static string Wa => RegrasPadrao.Todas().First(r => r.Id == "p-wa-web").Rotulo;

    [Theory]
    [InlineData("Uso da memória em WhatsApp Business (fixada): 292 MB", "WhatsApp Business")]
    [InlineData("Uso da memória em ZapFunil (fixada): 292", "ZapFunil")]
    [InlineData("Uso da memória em Nova sessão - Claude (", "Nova sessão - Claude")]
    [InlineData("Memory usage in (3) WhatsApp (pinned): 1,2 GB", "(3) WhatsApp")]
    [InlineData("(2) WhatsApp", "(2) WhatsApp")]
    [InlineData("Caixa de entrada (5) - joni@x.com - Gmail", "Caixa de entrada (5) - joni@x.com - Gmail")]
    public void Normaliza_nome_da_guia(string bruto, string esperado) => Assert.Equal(esperado, GuiasNav.Normalizar(bruto));

    [Theory]
    [InlineData("(1) WhatsApp Business - Google Chrome", "(1) WhatsApp Business")]
    [InlineData("Nova sessão - Claude - Google Chrome", "Nova sessão - Claude")]
    [InlineData("Notas e mais 3 páginas - Pessoal - Microsoft\u200B Edge", "Notas")]
    public void Titulo_sem_navegador(string bruto, string esperado) => Assert.Equal(esperado, GuiasNav.TituloSemNavegador(bruto));

    [Fact] public void Titulo_da_janela_traz_o_numero_sem_contar_em_dobro()
    {
        var g = GuiasNav.Juntar(new[] { "Uso da memória em WhatsApp Business (fixada): 292 MB", "Uso da memória em ZapFunil (fixada): 292" }, "(1) WhatsApp Business - Google Chrome");
        Assert.Equal(new[] { "(1) WhatsApp Business", "ZapFunil" }, g);
        Assert.Equal(1, Contagem.PorRotulo(g, Wa));
    }

    [Fact] public void Titulo_de_outra_pagina_entra_como_guia() =>
        Assert.Equal(new[] { "WhatsApp", "Gmail" }, GuiasNav.Juntar(new[] { "WhatsApp" }, "Gmail - Google Chrome"));

    [Fact] public void WhatsApp_Business_casa_com_a_regra_do_whatsapp()
    {
        Assert.Equal(0, Contagem.PorRotulo(new[] { "WhatsApp Business" }, Wa));
        Assert.Equal(4, Contagem.PorRotulo(new[] { "(4) WhatsApp Business" }, Wa));
    }

    [Fact] public void Pasta_de_dados_separa_chrome_de_automacao()
    {
        Assert.Equal("C:\\relay\\nav", GuiasNav.PastaDaLinha("\"chrome.exe\" --user-data-dir=\"C:\\relay\\nav\\\" --remote-debugging-port=9292"));
        Assert.Null(GuiasNav.PastaDaLinha("chrome.exe --type=renderer"));
        var padrao = "C:\\Users\\x\\AppData\\Local\\Google\\Chrome\\User Data";
        Assert.Equal("Profile 1", GuiasNav.ChavePerfil(padrao, padrao, "Profile 1"));
        Assert.Equal("Default", GuiasNav.ChavePerfil(null, padrao, "Default"));
        Assert.Equal("C:\\relay\\nav|Default", GuiasNav.ChavePerfil("C:\\relay\\nav", padrao, "Default"));
        Assert.Equal("", GuiasNav.ChavePerfil(padrao, padrao, null));
    }
}
