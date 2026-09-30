namespace YaTrackerTelegramBot;

public sealed class BotService(ITelegramClient telegram, ITrackerClient tracker, long allowedUserId)
{
    private static readonly object MainKeyboard = new
    {
        keyboard = new[] { new[] { new { text = "📋 Задачи на мне" } } },
        resize_keyboard = true,
        is_persistent = true
    };

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        long offset = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var updates = await telegram.GetUpdatesAsync(offset, cancellationToken);
                foreach (var update in updates)
                {
                    await HandleUpdateAsync(update, cancellationToken);
                    offset = update.UpdateId + 1;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Ошибка обработки обновления: " + FormatError(exception));
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    public async Task HandleUpdateAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (update.Message is { } message)
        {
            if (message.Chat.Type != "private" || message.From is null) return;
            if (message.From.Id != allowedUserId)
            {
                await telegram.SendAsync(message.Chat.Id,
                    $"⛔ Доступ закрыт. Ваш Telegram ID: {message.From.Id}. Передайте его владельцу бота.",
                    null, false, cancellationToken);
                return;
            }
            if (message.Text is "/start" or "/start@YaTrackerBot")
            {
                await telegram.SendAsync(message.Chat.Id,
                    "Привет! Покажу ваши активные задачи из Яндекс Трекера. Нажмите «📋 Задачи на мне».",
                    MainKeyboard, false, cancellationToken);
                return;
            }
            if (message.Text is "/tasks" or "📋 Задачи на мне")
            {
                await ShowPageAsync(message.Chat.Id, null, 1, cancellationToken);
                return;
            }
            await telegram.SendAsync(message.Chat.Id,
                "Не понял сообщение. Используйте кнопку «📋 Задачи на мне» или команду /tasks.",
                MainKeyboard, false, cancellationToken);
            return;
        }

        if (update.CallbackQuery is not { } callback || callback.Message?.Chat.Type != "private") return;
        await telegram.AnswerCallbackAsync(callback.Id, cancellationToken);
        if (callback.From.Id != allowedUserId) return;
        if (callback.Data is null || !callback.Data.StartsWith("tasks:", StringComparison.Ordinal)
            || !int.TryParse(callback.Data.AsSpan(6), out var page) || page is < 1 or > 1000)
        {
            await telegram.SendAsync(callback.Message.Chat.Id, "Некорректная страница. Нажмите «Задачи на мне» ещё раз.",
                MainKeyboard, false, cancellationToken);
            return;
        }
        await ShowPageAsync(callback.Message.Chat.Id, callback.Message.MessageId, page, cancellationToken);
    }

    private async Task ShowPageAsync(long chatId, int? messageId, int page, CancellationToken cancellationToken)
    {
        try
        {
            var rendered = IssueRenderer.Render(await tracker.GetMyIssuesAsync(page, cancellationToken));
            if (messageId is int id)
                await telegram.EditAsync(chatId, id, rendered.Text, rendered.InlineKeyboard, cancellationToken);
            else
                await telegram.SendAsync(chatId, rendered.Text, rendered.InlineKeyboard, true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Ошибка получения задач: " + FormatError(exception));
            await telegram.SendAsync(chatId,
                "Не удалось получить задачи из Трекера. Проверьте подключение и настройки, затем попробуйте ещё раз.",
                MainKeyboard, false, cancellationToken);
        }
    }

    private static string FormatError(Exception exception) => exception is TelegramApiException
        ? exception.Message
        : $"{exception.GetType().Name}, HTTP {(exception as HttpRequestException)?.StatusCode?.ToString() ?? "—"}";
}
