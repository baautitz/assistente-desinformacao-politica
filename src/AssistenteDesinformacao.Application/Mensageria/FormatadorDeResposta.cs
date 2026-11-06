using System.Globalization;
using System.Text;
using AssistenteDesinformacao.Domain.Pesquisa;
using AssistenteDesinformacao.Domain.Sinteses;

namespace AssistenteDesinformacao.Application.Mensageria;

/// <summary>Monta o texto enviado ao usuário. Link e data de cada fonte vêm dos dados, nunca da IA.</summary>
public static class FormatadorDeResposta
{
    public static string Formatar(Sintese sintese)
    {
        if (!sintese.EncontrouFontes)
            return sintese.Resumo;

        var texto = new StringBuilder(sintese.Resumo)
            .AppendLine()
            .AppendLine()
            .AppendLine("Fontes:");

        foreach (var fonte in sintese.Fontes)
        {
            texto.Append("• ").Append(fonte.Veiculo).Append(" (").Append(Data(fonte.DataPublicacao)).Append("): ");
            texto.AppendLine(fonte is Checagem checagem
                ? $"classificou como \"{checagem.Classificacao}\""
                : fonte.Titulo);
            texto.AppendLine(fonte.Link.AbsoluteUri);
        }

        return texto.ToString().TrimEnd();
    }

    private static string Data(DateOnly? data) =>
        data?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "sem data";
}
