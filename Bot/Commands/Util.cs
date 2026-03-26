namespace DcMalBot.Bot.Commands;

using Discord.Interactions;

public class UtilCommands : InteractionModuleBase<SocketInteractionContext> {
    [SlashCommand("ping", "Receive bot latency in ms")]
    public async Task Ping() {
        await RespondAsync($"Bot latency: {Context.Client.Latency} ms");
    }
}