using RolloutDemo.Models;

namespace RolloutDemo.Repositories;

/// <summary>
/// "Repositório" em memória para demo. Em produção, usaria o banco.
/// </summary>
public class FeatureFlagRepository
{
    private OutboxFeatureFlag? _current;

    public OutboxFeatureFlag? ObterFeatureFlagOutbox() => _current;

    public void Definir(OutboxFeatureFlag flag) => _current = flag;

    public void Definir(bool ativo, decimal percentual, DateTime? dataFim = null, string? obs = null)
    {
        _current = new OutboxFeatureFlag
        {
            Id = 1,
            Ativo = ativo,
            PercentualVolume = percentual,
            DataFim = dataFim,
            Observacao = obs ?? "Demo",
            DataUltimaAtualizacao = DateTime.UtcNow
        };
    }
}
