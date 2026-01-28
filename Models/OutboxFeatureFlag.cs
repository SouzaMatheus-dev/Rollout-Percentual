namespace RolloutDemo.Models;

/// <summary>
/// Espelha a tabela [dbo].[OutboxFeatureFlag] do banco.
/// </summary>
public class OutboxFeatureFlag
{
    public int Id { get; set; }
    public bool Ativo { get; set; }
    public decimal PercentualVolume { get; set; }  // 0.00 a 100.00
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataUltimaAtualizacao { get; set; }
    public string? UsuarioAtualizacao { get; set; }
}
