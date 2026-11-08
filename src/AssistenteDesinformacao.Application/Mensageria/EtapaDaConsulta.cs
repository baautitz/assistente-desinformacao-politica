namespace AssistenteDesinformacao.Application.Mensageria;

/// <summary>Em que ponto a consulta está. Serve só para o bot mostrar o andamento ao usuário.</summary>
public enum EtapaDaConsulta
{
    Lendo,
    Buscando,
    BuscandoDeNovo,
    Conferindo,
    Escrevendo,
}
