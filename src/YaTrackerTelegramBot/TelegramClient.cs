using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YaTrackerTelegramBot;

public sealed record TelegramUser([property: JsonPropertyName("id")] long Id);
public sealed record TelegramChat([property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string? Type);
public sealed record TelegramMessage([property: JsonPropertyName("message_id")] int MessageId,
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("from")] TelegramUser? From,
    [property: JsonPropertyName("text")] string? Text);
public sealed record TelegramCallback([property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("from")] TelegramUser From,
    [property: JsonPropertyName("message")] TelegramMessage? Message,
    [property: JsonPropertyName("data")] string? Data);
public sealed record TelegramUpdate([property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message,
    [property: JsonPropertyName("callback_query")] TelegramCallback? CallbackQuery);

public sealed class TelegramApiException(string method, HttpStatusCode statusCode, string? description)
    : Exception($"Telegram {method}: HTTP {(int)statusCode}, {description?[..Math.Min(description.Length, 160)] ?? "без описания"}");

public interface ITelegramClient
{
    Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken);
    Task SendAsync(long chatId, string text, object? replyMarkup, bool html, CancellationToken cancellationToken);
    Task EditAsync(long chatId, int messageId, string text, object? replyMarkup, CancellationToken cancellationToken);
    Task AnswerCallbackAsync(string callbackId, CancellationToken cancellationToken);
}

public sealed class TelegramClient(HttpClient http, string token) : ITelegramClient
{
    private readonly string _baseUrl = $"https://api.telegram.org/bot{token}/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken)
    {
        var result = await CallAsync<TelegramUpdate[]>("getUpdates", new
        {
            offset, timeout = 30, allowed_updates = new[] { "message", "callback_query" }
        }, cancellationToken);
        return result ?? [];
    }

    public Task SendAsync(long chatId, string text, object? replyMarkup, bool html, CancellationToken cancellationToken) =>
        CallAsync<JsonElement>("sendMessage", new
        {
            chat_id = chatId, text, parse_mode = html ? "HTML" : null,
            disable_web_page_preview = true, reply_markup = replyMarkup
        }, cancellationToken);

    public Task EditAsync(long chatId, int messageId, string text, object? replyMarkup, CancellationToken cancellationToken) =>
        CallAsync<JsonElement>("editMessageText", new
        {
            chat_id = chatId, message_id = messageId, text, parse_mode = "HTML",
            disable_web_page_preview = true, reply_markup = replyMarkup
        }, cancellationToken, ignoreNotModified: true);

    public Task AnswerCallbackAsync(string callbackId, CancellationToken cancellationToken) =>
        CallAsync<JsonElement>("answerCallbackQuery", new { callback_query_id = callbackId }, cancellationToken);

    private async Task<T?> CallAsync<T>(string method, object body, CancellationToken cancellationToken,
        bool ignoreNotModified = false)
    {
        using var response = await http.PostAsJsonAsync(_baseUrl + method, body, JsonOptions, cancellationToken);
        var envelope = await response.Content.ReadFromJsonAsync<TelegramEnvelope<T>>(JsonOptions, cancellationToken);
        if (ignoreNotModified && response.StatusCode == System.Net.HttpStatusCode.BadRequest &&
            envelope?.Description?.Contains("message is not modified", StringComparison.OrdinalIgnoreCase) == true)
            return default;
        if (!response.IsSuccessStatusCode)
            throw new TelegramApiException(method, response.StatusCode, envelope?.Description);
        if (envelope?.Ok != true) throw new InvalidOperationException("Telegram API отклонил запрос.");
        return envelope.Result;
    }

    private sealed record TelegramEnvelope<T>([property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("result")] T? Result,
        [property: JsonPropertyName("description")] string? Description);
}
