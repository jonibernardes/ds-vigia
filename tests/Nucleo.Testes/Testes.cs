using System;
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
