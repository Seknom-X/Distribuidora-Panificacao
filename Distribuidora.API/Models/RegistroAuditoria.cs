namespace Distribuidora.API.Models;

public sealed class RegistroAuditoria
{
    public long Id { get; set; }
    public DateTime DataUtc { get; set; }
    public int UsuarioId { get; set; }
    public string Perfil { get; set; } = string.Empty;
    public string Metodo { get; set; } = string.Empty;
    public string Caminho { get; set; } = string.Empty;
    public int StatusHttp { get; set; }
    public string CorrelacaoId { get; set; } = string.Empty;
}
