using System.Reflection;
using System.Text.Json;
using HunterBuddy.Models;

namespace HunterBuddy.Services;

public class DropResolver
{
    private readonly Dictionary<uint, List<DropSource>> _drops = [];

    public DropResolver() => LoadDropData();

    private void LoadDropData()
    {
        try
        {
            var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("HunterBuddy.Data.drops.json");

            if (stream == null)
            {
                Plugin.Log.Warning("[HunterBuddy] drops.json introuvable dans les ressources.");
                return;
            }

            var root = JsonSerializer.Deserialize<DropsRoot>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (root?.Drops == null) return;

            foreach (var (key, sources) in root.Drops)
            {
                if (uint.TryParse(key, out var itemId))
                    _drops[itemId] = sources;
            }

            Plugin.Log.Info($"[HunterBuddy] {_drops.Count} items chargés depuis drops.json.");
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "[HunterBuddy] Erreur lors du chargement de drops.json.");
        }
    }

    public List<DropSource> GetSources(uint itemId)
        => _drops.TryGetValue(itemId, out var s) ? s : [];

    public bool HasSources(uint itemId) => _drops.ContainsKey(itemId);

    public IEnumerable<uint> AllKnownItemIds => _drops.Keys;

    private class DropsRoot
    {
        public string Version { get; set; } = string.Empty;
        public Dictionary<string, List<DropSource>>? Drops { get; set; }
    }
}
