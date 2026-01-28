using RolloutDemo.Models;
using RolloutDemo.Repositories;
using RolloutDemo.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo: Rollout por % (Outbox Feature Flag)                       ║");
Console.WriteLine("║  VerificarFeatureFlagOutbox — rollout por percentual                ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// --- 1. Entendendo o hash ---
Console.WriteLine("── 1. Como funciona o percentualHash (0.00 a 99.99) ──");
Console.WriteLine("   Usamos: hash = |Id.GetHashCode()|  →  percentualHash = (hash % 10000) / 100");
Console.WriteLine("   Assim, cada Id mapeia sempre no MESMO valor (distribuição consistente).");
Console.WriteLine();

var exemplo = Guid.NewGuid();
var pHash = RolloutService.ObterPercentualHash(exemplo);
Console.WriteLine($"   Exemplo: Id = {exemplo}");
Console.WriteLine($"            percentualHash = {pHash:F2}");
Console.WriteLine($"   Se PercentualVolume = 25.00 → Outbox? {pHash < 25.00m}");
Console.WriteLine();

// --- 2. Conjunto fixo de Ids para simulação ---
var ids = GerarIds(1000);

// --- 3. Simulação com vários percentuais ---
Console.WriteLine("── 2. Simulação: 1000 Ids, diferentes percentuais ──");
Console.WriteLine();

var percentuais = new[] { 0m, 10m, 25m, 50m, 75m, 100m };
foreach (var pct in percentuais)
{
    var flag = new OutboxFeatureFlag { Ativo = true, PercentualVolume = pct };
    var outbox = ids.Count(id => RolloutService.VerificarFeatureFlagOutbox(id, flag));
    var pctReal = ids.Count > 0 ? (100.0 * outbox / ids.Count) : 0;
    Console.WriteLine($"   PercentualVolume = {pct,5:F2}%  →  Outbox: {outbox,4} / {ids.Count}  ({pctReal:F1}%)");
}

Console.WriteLine();
Console.WriteLine("── 3. Consistência: mesmo Id → mesmo resultado ──");
Console.WriteLine("   (vale também entre pods: K8s, múltiplas réplicas — decisão só depende do Id.)");

var um = ids[0];
var flag50 = new OutboxFeatureFlag { Ativo = true, PercentualVolume = 50m };
var v1 = RolloutService.VerificarFeatureFlagOutbox(um, flag50);
var v2 = RolloutService.VerificarFeatureFlagOutbox(um, flag50);
Console.WriteLine($"   Id: {um}");
Console.WriteLine($"   Chamada 1: Outbox = {v1}, Chamada 2: Outbox = {v2}  (sempre iguais)");
Console.WriteLine();

// --- 4. Feature flag desativada / DataFim ---
Console.WriteLine("── 4. Regras extras: Ativo=false e DataFim ──");

var repo = new FeatureFlagRepository();
repo.Definir(ativo: false, percentual: 50m);
var ffOff = repo.ObterFeatureFlagOutbox();
Console.WriteLine($"   Ativo = false, Percentual = 50% → Outbox? {RolloutService.VerificarFeatureFlagOutbox(um, ffOff)} (sempre false)");

repo.Definir(ativo: true, percentual: 50m, dataFim: DateTime.UtcNow.AddDays(-1));
var ffExpirado = repo.ObterFeatureFlagOutbox();
Console.WriteLine($"   DataFim no passado → Outbox? {RolloutService.VerificarFeatureFlagOutbox(um, ffExpirado)} (sempre false)");
Console.WriteLine();

// --- 5. Detalhe de alguns Ids (amostra) ---
Console.WriteLine("── 5. Amostra: percentualHash e decisão (PercentualVolume = 30%) ──");
var flag30 = new OutboxFeatureFlag { Ativo = true, PercentualVolume = 30m };
foreach (var id in ids.Take(10))
{
    var h = RolloutService.ObterPercentualHash(id);
    var outbox = RolloutService.VerificarFeatureFlagOutbox(id, flag30);
    Console.WriteLine($"   {id}  →  hash% = {h,5:F2}  Outbox = {outbox}");
}
Console.WriteLine();

// --- 6. Interativo (opcional) ---
var naoInterativo = args.Contains("--no-input", StringComparer.OrdinalIgnoreCase);
if (!naoInterativo)
{
    Console.WriteLine("── 6. Testar outro percentual (0–100, ou Enter para sair) ──");
    string? input;
    while (true)
    {
        Console.Write("   PercentualVolume (%): ");
        input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) break;
        if (!decimal.TryParse(input.Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var pct) || pct < 0 || pct > 100)
        {
            Console.WriteLine("   Inválido. Use 0–100.");
            continue;
        }
        var f = new OutboxFeatureFlag { Ativo = true, PercentualVolume = pct };
        var n = ids.Count(id => RolloutService.VerificarFeatureFlagOutbox(id, f));
        var pctR = 100.0 * n / ids.Count;
        Console.WriteLine($"   → Outbox: {n} / {ids.Count} ({pctR:F1}%)");
    }
}
else
{
    Console.WriteLine("── 6. Modo --no-input: interativo omitido. Rode sem o argumento para testar. ──");
}

Console.WriteLine();
Console.WriteLine("Até logo.");

// --- Helper ---
static List<Guid> GerarIds(int n)
{
    var list = new List<Guid>(n);
    var rng = new Random(42); // fixo para reprodutibilidade
    var b = new byte[16];
    for (var i = 0; i < n; i++)
    {
        rng.NextBytes(b);
        list.Add(new Guid(b));
    }
    return list;
}
