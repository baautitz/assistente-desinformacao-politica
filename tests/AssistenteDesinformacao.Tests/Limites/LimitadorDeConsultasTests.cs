using AssistenteDesinformacao.Application.Limites;

namespace AssistenteDesinformacao.Tests.Limites;

public class LimitadorDeConsultasTests
{
    private sealed class RelogioFalso : TimeProvider
    {
        private DateTimeOffset _agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _agora;
        public void Avancar(TimeSpan tempo) => _agora += tempo;
    }

    [Fact]
    public void Aceita_ate_o_maximo_e_recusa_a_seguinte()
    {
        var limitador = LimitadorDeConsultas.PorMinuto(5, new RelogioFalso());

        for (var i = 0; i < 5; i++)
            Assert.True(limitador.TentarRegistrar(1));

        Assert.False(limitador.TentarRegistrar(1));
    }

    [Fact]
    public void Contagem_e_separada_por_usuario()
    {
        var limitador = LimitadorDeConsultas.PorMinuto(1, new RelogioFalso());

        Assert.True(limitador.TentarRegistrar(1));
        Assert.False(limitador.TentarRegistrar(1));
        Assert.True(limitador.TentarRegistrar(2));
    }

    [Fact]
    public void Libera_de_novo_depois_que_a_janela_passa()
    {
        var relogio = new RelogioFalso();
        var limitador = LimitadorDeConsultas.PorMinuto(2, relogio);

        Assert.True(limitador.TentarRegistrar(1));
        relogio.Avancar(TimeSpan.FromSeconds(30));
        Assert.True(limitador.TentarRegistrar(1));
        Assert.False(limitador.TentarRegistrar(1));

        relogio.Avancar(TimeSpan.FromSeconds(30));
        Assert.True(limitador.TentarRegistrar(1));
        Assert.False(limitador.TentarRegistrar(1));

        relogio.Avancar(TimeSpan.FromSeconds(30));
        Assert.True(limitador.TentarRegistrar(1));
    }

    [Fact]
    public void Consulta_recusada_nao_conta_para_a_janela()
    {
        var relogio = new RelogioFalso();
        var limitador = LimitadorDeConsultas.PorMinuto(1, relogio);

        Assert.True(limitador.TentarRegistrar(1));
        relogio.Avancar(TimeSpan.FromSeconds(59));
        Assert.False(limitador.TentarRegistrar(1));
        relogio.Avancar(TimeSpan.FromSeconds(1));
        Assert.True(limitador.TentarRegistrar(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Maximo_invalido_e_rejeitado(int maximo) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LimitadorDeConsultas.PorMinuto(maximo));
}
