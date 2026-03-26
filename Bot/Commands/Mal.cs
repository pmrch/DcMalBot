using Discord;
using Discord.Interactions;
using JikanDotNet;
using Serilog;

using DcMalBot.Utils;
using Discord.WebSocket;

namespace DcMalBot.Bot.Commands;

public class MalCommands(ILogger logger, AnimeSearchCache cache) : InteractionModuleBase<SocketInteractionContext> {
    private readonly ILogger _logger = logger;
    private readonly Jikan _jikan = new();
    private readonly AnimeSearchCache _searchCache = cache;

    private static MessageComponent BuildPageButtons(int currentPage, int maxPages) {
        return new ComponentBuilder()
            .WithButton("◀", "prev_page", disabled: currentPage == 0)
            .WithButton($"{currentPage + 1}/{maxPages}", "page_indicator", ButtonStyle.Secondary, disabled: true)
            .WithButton("▶", "next_page", disabled: currentPage == maxPages - 1)
            .Build();
    }

    [ComponentInteraction("prev_page")]
    private async Task PrevPage() {
        var state = _searchCache.Get(Context.User.Id);
        if (state is null) { await RespondAsync("Session expired!", ephemeral: true); return; }

        _searchCache.SetPage(Context.User.Id, state.Value.Page - 1);
        await UpdatePage();
    }

    [ComponentInteraction("next_page")]
    private async Task NextPage() {
        var state = _searchCache.Get(Context.User.Id);
        if (state is null) { await RespondAsync("Session expired!", ephemeral: true); return; }

        _searchCache.SetPage(Context.User.Id, state.Value.Page + 1);
        await UpdatePage();
    }

    private async Task UpdatePage() {
        var state = _searchCache.Get(Context.User.Id)!.Value;
        var anime = state.Results[state.Page];
        var maxPages = state.Results.Count;

        var title = anime.Titles?.FirstOrDefault()?.Title ?? "N/A";
        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithUrl(anime.Url)
            .WithThumbnailUrl(anime.Images?.JPG?.ImageUrl)
            .WithColor(Color.Blue)
            .AddField("Score", anime.Score?.ToString() ?? "N/A", inline: true)
            .AddField("Year", anime.Year?.ToString() ?? "N/A", inline: true)
            .AddField("Episodes", anime.Episodes?.ToString() ?? "N/A", inline: true)
            .AddField("Status", anime.Status ?? "N/A", inline: true)
            .AddField("Synopsis", anime.Synopsis?.Length > 1024
                ? anime.Synopsis[..1021] + "..."
                : anime.Synopsis ?? "N/A")
            .WithFooter($"Result {state.Page + 1}/{maxPages} • MyAnimeList")
            .Build();

        var components = BuildPageButtons(state.Page, maxPages);
        await (Context.Interaction as SocketMessageComponent)!.UpdateAsync(x => {
            x.Embed = embed;
            x.Components = components;
        });
    }

    [SlashCommand("anime_search", "Search MAL for an anime", runMode: RunMode.Async)]
    public async Task AnimeSearch(string query, bool strict = false) {
        await DeferAsync(ephemeral: true);

        var results = await _jikan.SearchAnimeAsync(query);
        if (results is null || results.Data is null) {
            await FollowupAsync("Anime search query failed!", ephemeral: true);
            return;
        }
        
        var animes = results.Data.Where(x => x is not null).ToList();
        if (strict == true) {
            animes = results.Data.Where(x => 
                x.Titles.FirstOrDefault()?.Title is not null
                && x.Titles.FirstOrDefault()!.Title.Contains(query)
            ).ToList();
        }

        _searchCache.Store(Context.User.Id, animes);

        var anime = animes[0];
        var firstTitle = anime.Titles.FirstOrDefault()?.Title ?? query;
        var embed = new EmbedBuilder()
            .WithTitle(firstTitle)
            .WithUrl(anime.Url)
            .WithThumbnailUrl(anime.Images?.JPG?.ImageUrl)
            .WithColor(Color.Blue)
            .AddField("Score", anime.Score?.ToString() ?? "N/A", inline: true)
            .AddField("Episodes", anime.Episodes?.ToString() ?? "N/A", inline: true)
            .AddField("Status", anime.Status ?? "N/A", inline: true)
            .AddField("Synopsis", anime.Synopsis?.Length > 1024 
                ? anime.Synopsis[..1021] + "..."
                : anime.Synopsis ?? "N/A")
            .WithFooter($"Result 1/{animes.Count} • MyAnimeList")
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true, components: BuildPageButtons(0, animes.Count));
    }
}