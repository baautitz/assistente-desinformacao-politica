using AssistenteDesinformacao.Domain.Pesquisa;
using AssistenteDesinformacao.Infrastructure.Pesquisa;

namespace AssistenteDesinformacao.Tests.Pesquisa;

public class LeitorDeMateriaTests
{
    private const string Pagina = """
        <html><head>
          <meta property="og:description" content="O Senado aprovou o fim da escala 6x1 e garantiu dois dias de descanso semanal.">
        </head><body>
          <nav><p>Menu de navegação com texto longo o suficiente para passar do mínimo de sessenta caracteres.</p></nav>
          <script>var x = "<p>script com paragrafo longo o suficiente para ser confundido com texto da matéria</p>";</script>
          <article>
            <p>Curto.</p>
            <p>O Senado aprovou nesta quinta-feira o fim da escala 6x1 e garantiu dois dias de descanso semanal aos trabalhadores.</p>
            <p>A proposta segue agora para promulgação, e o texto prevê uma transição gradual para as empresas de todos os portes.</p>
          </article>
          <footer><p>Rodapé com texto bem longo, de mais de sessenta caracteres, que não faz parte da matéria em si.</p></footer>
        </body></html>
        """;

    [Fact]
    public void Extrai_a_descricao_e_os_primeiros_paragrafos_da_materia()
    {
        var texto = LeitorDeMateria.ExtrairTexto(Pagina);

        Assert.StartsWith("O Senado aprovou o fim da escala 6x1 e garantiu dois dias de descanso semanal.", texto);
        Assert.Contains("segue agora para promulgação", texto);
    }

    [Fact]
    public void Ignora_menu_script_rodape_e_paragrafo_curto()
    {
        var texto = LeitorDeMateria.ExtrairTexto(Pagina);

        Assert.DoesNotContain("Menu de navegação", texto);
        Assert.DoesNotContain("script com paragrafo", texto);
        Assert.DoesNotContain("Rodapé", texto);
        Assert.DoesNotContain("Curto.", texto);
    }

    [Fact]
    public void Nao_repete_a_descricao_quando_ela_e_o_primeiro_paragrafo()
    {
        var texto = LeitorDeMateria.ExtrairTexto(
            """<meta property="og:description" content="Plano de governo do candidato cita o equipamento dezenove vezes em todo o texto."><p>Plano de governo do candidato cita o equipamento dezenove vezes em todo o texto, diz o levantamento.</p>""");

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(texto, "Plano de governo"));
    }

    [Fact]
    public void Limita_o_tamanho_do_texto()
    {
        var paragrafo = "<p>" + string.Concat(Enumerable.Repeat("palavra ", 200)) + "</p>";

        var texto = LeitorDeMateria.ExtrairTexto(string.Concat(Enumerable.Repeat(paragrafo, 5)));

        Assert.True(texto.Length <= LeitorDeMateria.MaximoDeCaracteres + 1);
    }

    [Fact]
    public void Pagina_sem_texto_devolve_vazio() =>
        Assert.Equal("", LeitorDeMateria.ExtrairTexto("<html><body><div>nada</div></body></html>"));

    [Fact]
    public void Checagem_com_mais_de_um_ano_para_a_noticia_recente_sai()
    {
        var antiga = new Checagem("PEC não foi votada", new Uri("https://uol.com.br/confere/2024/11/14/pec.htm"), null, "UOL", "Falso");
        var recente = new Checagem("Vídeo é falso", new Uri("https://aosfatos.org/x/"), new DateOnly(2026, 5, 29), "Aos Fatos", "Falso");
        var noticia = new Fonte("Congresso promulga", new Uri("https://camara.leg.br/n"), new DateOnly(2026, 10, 16), "Câmara");

        var restantes = Fonte.SemChecagensDefasadas([antiga, recente, noticia]);

        Assert.DoesNotContain(antiga, restantes);
        Assert.Contains(recente, restantes);
        Assert.Contains(noticia, restantes);
    }

    [Fact]
    public void Sem_noticia_para_comparar_mantem_a_checagem_antiga()
    {
        var antiga = new Checagem("PEC não foi votada", new Uri("https://uol.com.br/confere/2024/11/14/pec.htm"), null, "UOL", "Falso");

        Assert.Single(Fonte.SemChecagensDefasadas([antiga]));
    }
}
