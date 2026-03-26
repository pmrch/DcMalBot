namespace DcMalBot.Utils;
using JikanDotNet;

public struct AnimeItem {
    public List<Anime> Results;
    public int Page;
}

public class AnimeSearchCache {
    private readonly Dictionary<ulong, AnimeItem> _cache = [];

    public void Store(ulong userId, List<Anime> results) 
        => _cache[userId] = new AnimeItem { Results=results, Page=0 };

    public AnimeItem? Get(ulong userId)
        => _cache.TryGetValue(userId, out var val) ? val : null;

    public void SetPage(ulong userId, int page) {
        if (_cache.TryGetValue(userId, out var val)) {
            _cache[userId] = new AnimeItem { Results=val.Results, Page=page };
        }
    }

    public void Clear(ulong userId) => _cache.Remove(userId);
}