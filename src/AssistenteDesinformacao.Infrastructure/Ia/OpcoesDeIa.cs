namespace AssistenteDesinformacao.Infrastructure.Ia;

/// <summary>Provedor de IA compatível com a API da OpenAI. Trocar de provedor é só mudar estes valores.</summary>
public sealed record OpcoesDeIa(Uri UrlBase, string Chave, string ModeloDeTexto);
