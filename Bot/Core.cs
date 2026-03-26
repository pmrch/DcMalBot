namespace DcMalBot.Bot;

using Serilog.Core;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Discord.LibDave.Binding;
using Serilog.Events;
using Discord;
using Discord.WebSocket;

using DcMalBot.Utils;

public class DiscordBot {
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _services;
    private readonly Logger _logger;
    private readonly Logger _daveLogger;
    private readonly string _token;
    private readonly ulong _guildId;

    public DiscordBot(GatewayIntents intents, string? token, ulong guildId) {
        // 1. Enforce Token presence
        _token = token  ?? throw new ArgumentNullException(nameof(token), "Bot token cannot be null.");
        _guildId = guildId;

        // 2. Setup Serilog logger
        var level = LogEventLevel.Verbose;
        _logger = Logging.CreateLogger("Discord", level, true, true, null)
            ?? throw new InvalidOperationException("Failed to create Discord logger instance.");

        var daveLogLevel = LogEventLevel.Warning;
        _daveLogger = Logging.CreateLogger("DAVE", daveLogLevel, true, true, null)
            ?? throw new InvalidOperationException("Failed to create DAVE logger instance.");

        // Setup Client
        DiscordSocketConfig socketConfig = new() { 
            GatewayIntents = intents,
            AlwaysDownloadUsers = true,
            EnableVoiceDaveEncryption = true
        };
        _discordClient = new(socketConfig);

        // Setup Interaction Service
        _interactionService = new InteractionService(_discordClient.Rest);

        // Setup Dependency Injection (self-registration)
        _services = new ServiceCollection()
            .AddSingleton(_discordClient)
            .AddSingleton(_interactionService)
            .AddSingleton<Serilog.ILogger>(_logger)
            .AddSingleton<AnimeSearchCache>()
            .BuildServiceProvider();
    }

    public async Task Start() {
        try {
            Discord.LibDave.Dave.SetLogSink(DaveLogSink);

            _discordClient.Log += LogToSerilog;
            _interactionService.Log += LogToSerilog;

            _discordClient.InteractionCreated += OnInteractionCreated;
            _discordClient.Ready += OnReady;

            // Discover modules
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            if (assembly != null) {
                await _interactionService.AddModulesAsync(assembly, _services);
            }

            // Login and start the bot
            await _discordClient.LoginAsync(TokenType.Bot, _token);
            await _discordClient.StartAsync();
            await Task.Delay(-1);
        } catch (Exception exc) {
            _logger.Fatal(exc, "The bot failed to start!");
            throw;
        }
    }

    private async Task OnReady() {
        _logger.Information("Registering commands to server...");
        await _interactionService.RegisterCommandsToGuildAsync(_guildId);
        _logger.Information($"Successfully registered {_interactionService.SlashCommands.Count} slash commands");
    }

    private async Task OnInteractionCreated(SocketInteraction interaction) {
        try {
            // Create a context/interaction
            var context = new SocketInteractionContext(_discordClient, interaction);

            // Execute the command found in modules
            await _interactionService.ExecuteCommandAsync(context, _services);
        } catch (Exception ex) {
            _logger.Error(ex, "Error handling interaction!");

            // If a slash command fails, try to let the user know
            if (interaction.Type == InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
        }
    }

    private Task LogToSerilog(LogMessage message) {
        var severity = message.Severity switch {
            LogSeverity.Critical => LogEventLevel.Fatal,
            LogSeverity.Error    => LogEventLevel.Error,
            LogSeverity.Warning  => LogEventLevel.Warning,
            LogSeverity.Info     => LogEventLevel.Information,
            LogSeverity.Verbose  => LogEventLevel.Verbose,
            LogSeverity.Debug    => LogEventLevel.Debug,
            _ => LogEventLevel.Information            
        };

        _logger.Write(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }

    private void DaveLogSink(LoggingSeverity severity, string filePath, int lineNumber, string message) {
        if (message.Contains("unrecognized user ID")) {
            return;
        }

        var sev = severity switch {
            LoggingSeverity.Verbose => LogEventLevel.Verbose,
            LoggingSeverity.Info => LogEventLevel.Information,
            LoggingSeverity.Warning => LogEventLevel.Warning,
            LoggingSeverity.Error => LogEventLevel.Error,
            _ => LogEventLevel.Information
        };

        _daveLogger.Write(sev, "[LIBDAVE @ {File}#{Line}]: {Message}", filePath, lineNumber, message);
    }
}