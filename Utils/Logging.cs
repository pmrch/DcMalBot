namespace DcMalBot.Utils;

using Serilog;
using Serilog.Core;
using Serilog.Events;

public class Logging {

    public static Logger? CreateLogger(string name, LogEventLevel logLevel, bool toFile, bool toConsole, string? basePath) {
        LoggerConfiguration config = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("BotName", name)
            .Enrich.WithThreadId();

        string directoryPath = Path.GetFullPath(basePath ?? "logs");
        string fullPath = Path.Join(directoryPath, name);
        Directory.CreateDirectory(fullPath);

        if (toFile) {config = config.WriteTo.File(Path.Join(fullPath, $"{name}.log"), rollingInterval: RollingInterval.Day); }
        if (toConsole) { 
            config = config.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] (Thread:{ThreadId}) {Message:lj}{NewLine}{Exception}"
            ); 
        }

        return config.CreateLogger();
    }
}