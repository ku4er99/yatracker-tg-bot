using YaTrackerTelegramBot;

try
{
    var options = BotOptions.FromEnvironment();
    using var telegramHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
    using var trackerHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    var telegram = new TelegramClient(telegramHttp, options.TelegramToken);
    var tracker = new TrackerClient(trackerHttp, options);
    var bot = new BotService(telegram, tracker, options.AllowedTelegramUserId);
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
    await bot.RunAsync(cancellation.Token);
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
}
