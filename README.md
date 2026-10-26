# DS Vigia

Programa de bandeja (Windows) que conta quantas conversas esperam resposta em cada sistema de
atendimento aberto no computador (WhatsApp Web e app, Messenger do Gestta, NP Connect, e-mail,
Telegram) e mostra no sinaleiro da DS Soluções. Substitui a extensão do Chrome "DS Atendimento".

Princípio, igual ao da extensão: **só o número e o estado saem do computador.** Nenhuma
mensagem, nome ou telefone de cliente é lido ou enviado. O programa é visível na bandeja; não
roda escondido.

- `src/Nucleo` — regras de contagem, cor e espera (sem Windows; coberto por testes).
- `src/DSVigia` — o leitor (acessibilidade + número do ícone de notificação), a bandeja, o
  sinaleiro sempre por cima, o envio ao sistema e a atualização automática.
- `tests/Nucleo.Testes` — 52 testes do núcleo.

## Faróis

Sem configuração, o vigia lê tudo no automático (um sinal por sistema). Em **Configurar faróis**
(menu da bandeja) a atendente cria até 8 sinais com nome, por exemplo "WhatsApp Cliente 1" e
"WhatsApp Interno", cada um preso a um sistema num **perfil do Chrome/Edge** ou a um programa
(WhatsApp, Telegram, Outlook). Com faróis salvos, o vigia lê só os faróis, na ordem escolhida, e
manda ao sistema com a chave `farol:<id>` e o nome dado. "Voltar ao automático" apaga os faróis.

- Arquivo: `%AppData%\DSVigia\farois.json`. Vale na próxima leitura, sem reiniciar.
- Perfil de cada janela: (a) AppUserModelID da janela, (b) botão de perfil da barra, (c)
  `--profile-directory` do processo. O nome amigável vem do `Local State` do navegador.
- Duas contas do mesmo site no mesmo perfil ficam iguais: use um perfil do Chrome para cada WhatsApp.
- Diagnóstico (`ultima-leitura.txt`): `farol <nome> perfil=<id> via=<a|b|c> -> n`.
