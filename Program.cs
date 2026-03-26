namespace DcMalBot;

using Discord;
using DotNetEnv;
using Serilog.Core;

using DcMalBot.Utils;
using DcMalBot.Bot;
using Serilog;

internal class Program {
    static async Task Main(string[] _) {
        var level = Serilog.Events.LogEventLevel.Information;
        Log.Logger = Logging.CreateLogger("Global", level, true, true, null)!;

        Env.Load();
        Log.Information("Successfully loaded .env");

        string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");    
        GatewayIntents intents = GatewayIntents.Guilds 
            | GatewayIntents.DirectMessages
            | GatewayIntents.GuildMembers
            | GatewayIntents.Guilds
            | GatewayIntents.GuildVoiceStates 
            | GatewayIntents.All;

        DiscordBot discordBot = new(intents, token, guildId: 1370851604596654171);
        Log.Information("Successfully initialized Discord bot!");
        await discordBot.Start();
    }
}