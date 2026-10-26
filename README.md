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
- `tests/Nucleo.Testes` — 30 testes do núcleo.
