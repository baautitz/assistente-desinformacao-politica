using AssistenteDesinformacao.WhatsAppBot;

namespace AssistenteDesinformacao.Tests.WhatsApp;

public class LeitorDeWebhookTests
{
    private static string Webhook(string chat, string mensagem, bool minha = false, string evento = "messages.upsert") =>
        $$$"""
        {"event":"{{{evento}}}","instance":"bot","data":{"key":{"remoteJid":"{{{chat}}}","fromMe":{{{(minha ? "true" : "false")}}},"id":"ABC123"},"message":{{{mensagem}}}}}
        """;

    private const string Pessoa = "5511999999999@s.whatsapp.net";

    [Fact]
    public void Le_texto_simples()
    {
        var lida = LeitorDeWebhook.Ler(Webhook(Pessoa, """{"conversation":"Lula vai taxar o pix"}"""));

        Assert.NotNull(lida);
        Assert.Equal("Lula vai taxar o pix", lida.Texto);
        Assert.Equal(Pessoa, lida.Chat);
        Assert.Equal("ABC123", lida.Id);
        Assert.False(lida.TemImagem);
    }

    [Fact]
    public void Le_texto_de_resposta_ou_link()
    {
        var lida = LeitorDeWebhook.Ler(Webhook(Pessoa, """{"extendedTextMessage":{"text":"veja isso https://x.com"}}"""));

        Assert.Equal("veja isso https://x.com", lida?.Texto);
    }

    [Fact]
    public void Le_imagem_com_legenda_e_guarda_o_que_a_evolution_pede_para_baixar()
    {
        var lida = LeitorDeWebhook.Ler(Webhook(Pessoa, """{"imageMessage":{"caption":"olha isso","mimetype":"image/jpeg"}}"""));

        Assert.NotNull(lida);
        Assert.True(lida.TemImagem);
        Assert.Equal("olha isso", lida.Texto);
        Assert.Equal("ABC123", lida.ChaveEMensagem!.Value.GetProperty("key").GetProperty("id").GetString());
    }

    [Fact]
    public void Le_reacao_como_emoji()
    {
        var lida = LeitorDeWebhook.Ler(Webhook(Pessoa, """{"reactionMessage":{"text":"👍"}}"""));

        Assert.True(lida?.EhReacao);
        Assert.Equal("👍", lida?.Texto);
    }

    [Theory]
    [InlineData("120363000000000000@g.us")]
    [InlineData("status@broadcast")]
    public void Ignora_grupo_e_status(string chat) =>
        Assert.Null(LeitorDeWebhook.Ler(Webhook(chat, """{"conversation":"oi"}""")));

    [Fact]
    public void Ignora_mensagem_enviada_pelo_proprio_bot() =>
        Assert.Null(LeitorDeWebhook.Ler(Webhook(Pessoa, """{"conversation":"resposta do bot"}""", minha: true)));

    [Fact]
    public void Ignora_outros_eventos_e_tipos_de_mensagem()
    {
        Assert.Null(LeitorDeWebhook.Ler(Webhook(Pessoa, """{"conversation":"oi"}""", evento: "connection.update")));
        Assert.Null(LeitorDeWebhook.Ler(Webhook(Pessoa, """{"audioMessage":{"seconds":3}}""")));
    }

    [Fact]
    public void Aceita_o_nome_do_evento_em_maiusculas_com_underscore() =>
        Assert.NotNull(LeitorDeWebhook.Ler(Webhook(Pessoa, """{"conversation":"oi"}""", evento: "MESSAGES_UPSERT")));

    [Theory]
    [InlineData("")]
    [InlineData("não é json")]
    [InlineData("[]")]
    [InlineData("""{"event":"messages.upsert"}""")]
    public void Lixo_nao_derruba_o_leitor(string json) =>
        Assert.Null(LeitorDeWebhook.Ler(json));
}

public class FeedbackPorEmojiTests
{
    [Theory]
    [InlineData("👍", true)]
    [InlineData(" 👎 ", false)]
    public void Voto_corresponde_a_ultima_resposta_do_chat(string emoji, bool esperado)
    {
        var feedback = new FeedbackPorEmoji();
        var resposta = Guid.NewGuid();
        feedback.Lembrar("chat", resposta);

        Assert.True(feedback.TentarVotar("chat", emoji, out var id, out var util));
        Assert.Equal(resposta, id);
        Assert.Equal(esperado, util);
    }

    [Fact]
    public void Voto_vale_uma_vez_so()
    {
        var feedback = new FeedbackPorEmoji();
        feedback.Lembrar("chat", Guid.NewGuid());

        Assert.True(feedback.TentarVotar("chat", "👍", out _, out _));
        Assert.False(feedback.TentarVotar("chat", "👍", out _, out _));
    }

    [Fact]
    public void Sem_resposta_esperando_ou_com_texto_comum_nao_e_voto()
    {
        var feedback = new FeedbackPorEmoji();

        Assert.False(feedback.TentarVotar("chat", "👍", out _, out _));

        feedback.Lembrar("chat", Guid.NewGuid());
        Assert.False(feedback.TentarVotar("chat", "obrigado", out _, out _));
        Assert.True(feedback.TentarVotar("chat", "👍", out _, out _));
    }

    [Fact]
    public void Resposta_antiga_nao_recebe_voto()
    {
        var relogio = new RelogioManual();
        var feedback = new FeedbackPorEmoji(relogio);
        feedback.Lembrar("chat", Guid.NewGuid());

        relogio.Advance(TimeSpan.FromHours(25));

        Assert.False(feedback.TentarVotar("chat", "👍", out _, out _));
    }

    [Fact]
    public void Voto_de_um_chat_nao_atinge_outro()
    {
        var feedback = new FeedbackPorEmoji();
        feedback.Lembrar("a", Guid.NewGuid());

        Assert.False(feedback.TentarVotar("b", "👍", out _, out _));
    }

    [Fact]
    public void Id_do_usuario_e_estavel_e_nao_expoe_o_telefone()
    {
        var chat = "5511999999999@s.whatsapp.net";

        Assert.Equal(WhatsAppWorker.IdDoUsuario(chat), WhatsAppWorker.IdDoUsuario(chat));
        Assert.NotEqual(WhatsAppWorker.IdDoUsuario(chat), WhatsAppWorker.IdDoUsuario("5511888888888@s.whatsapp.net"));
    }
}

file sealed class RelogioManual : TimeProvider
{
    private DateTimeOffset _agora = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _agora;

    public void Advance(TimeSpan tempo) => _agora += tempo;
}
