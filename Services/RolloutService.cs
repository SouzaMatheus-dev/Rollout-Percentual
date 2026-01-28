using RolloutDemo.Models;

namespace RolloutDemo.Services;

/// <summary>
/// Lógica de rollout por percentual.
/// Decide se uma mensagem deve ir para Outbox com base na feature flag.
/// Funciona em múltiplos pods (Kubernetes etc.): a decisão é determinística (só Id + flag), sem aleatório nem estado local.
/// </summary>
public class RolloutService
{
    /// <summary>
    /// Verifica se a mensagem do processo deve usar Outbox, conforme feature flag e percentual.
    /// </summary>
    /// <param name="id">Id (usado no hash para distribuição consistente)</param>
    /// <param name="featureFlag">Feature flag obtida do banco (OutboxFeatureFlag)</param>
    /// <returns>true = usar Outbox; false = comportamento atual (não Outbox)</returns>
    public static bool VerificarFeatureFlagOutbox(Guid id, OutboxFeatureFlag? featureFlag)
    {
        if (featureFlag == null || !featureFlag.Ativo)
            return false; // Feature flag desativada - usa comportamento atual

        if (featureFlag.DataFim.HasValue && featureFlag.DataFim.Value < DateTime.UtcNow)
            return false; // Período de rollout já encerrado

        if (featureFlag.PercentualVolume >= 100.00m)
            return true; // 100% = todas as mensagens vão para outbox

        // Distribuição consistente por Id: mesmo id sempre cai no mesmo "lado"
        var hash = Math.Abs(id.GetHashCode());
        var percentualHash = (hash % 10000) / 100.0m; // 0.00 a 99.99

        return percentualHash < featureFlag.PercentualVolume;
    }

    /// <summary>
    /// Calcula o "percentualHash" (0.00 a 99.99) para um Id.
    /// Útil para debug e para entender a distribuição.
    /// </summary>
    public static decimal ObterPercentualHash(Guid id)
    {
        var hash = Math.Abs(id.GetHashCode());
        return (hash % 10000) / 100.0m;
    }
}
