using System.Net;
using System.Text;
using System.Text.Json;
using YaTrackerTelegramBot;

namespace YaTrackerTelegramBot.Tests;

public sealed class BotTests
{
    [Fact]
    public async Task StartAndWrongInputShowUsefulInstructions()
    {
        var telegram = new FakeTelegram();
        var bot = new BotService(telegram, new FakeTracker(), 42);
        await bot.HandleUpdateAsync(Message("/start"), default);
        await bot.HandleUpdateAsync(Message("что-то другое"), default);
        Assert.Contains("Задачи на мне", telegram.Sent[0].Text);
        Assert.NotNull(telegram.Sent[0].Markup);
        Assert.Contains("Не понял", telegram.Sent[1].Text);
    }

    [Fact]
    public async Task TasksAndPaginationRequestExactlyOnePage()
    {
        var telegram = new FakeTelegram();
        var tracker = new FakeTracker();
        var bot = new BotService(telegram, tracker, 42);
        await bot.HandleUpdateAsync(Message("📋 Задачи на мне"), default);
        await bot.HandleUpdateAsync(new TelegramUpdate(2, null,
            new TelegramCallback("callback", new TelegramUser(42),
                new TelegramMessage(100, new TelegramChat(42, "private"), null, null), "tasks:2")), default);
        Assert.Equal([1, 2], tracker.RequestedPages);
        Assert.Single(telegram.Edited);
        Assert.Contains("2/3", telegram.Edited[0].Text);
        Assert.Equal(["callback"], telegram.AnsweredCallbacks);
    }

    [Fact]
    public async Task OtherUsersCannotReadTracker()
    {
        var telegram = new FakeTelegram();
        var tracker = new FakeTracker();
        var bot = new BotService(telegram, tracker, 42);
        await bot.HandleUpdateAsync(Message("/tasks", 99), default);
        Assert.Empty(tracker.RequestedPages);
        Assert.Contains("Доступ закрыт", telegram.Sent.Single().Text);
    }

    [Fact]
    public async Task TrackerFailureProducesFriendlyAnswer()
    {
        var telegram = new FakeTelegram();
        var bot = new BotService(telegram, new FakeTracker { Fail = true }, 42);
        await bot.HandleUpdateAsync(Message("/tasks"), default);
        Assert.Contains("Не удалось получить задачи", telegram.Sent.Single().Text);
    }

    [Fact]
    public void RendererEscapesIssueFieldsAndShowsMoscowTime()
    {
        var page = new TrackerPage([new TrackerIssue("ABC-1", "<script>&", "Открыт",
            new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero))], 1, 2);
        var rendered = IssueRenderer.Render(page);
        Assert.Contains("&lt;script&gt;&amp;", rendered.Text);
        Assert.DoesNotContain("<script>", rendered.Text);
        Assert.Contains("12:00 МСК", rendered.Text);
        Assert.Contains("https://tracker.yandex.ru/ABC-1", rendered.Text);
    }

    [Fact]
    public async Task TrackerSearchUsesMyOpenIssuesAndServerPagination()
    {
        var handler = new FakeHttpHandler();
        using var http = new HttpClient(handler);
        var client = new TrackerClient(http, new BotOptions("telegram", "tracker", "org", true, 42));
        var page = await client.GetMyIssuesAsync(2, default);
        Assert.Equal(2, page.Page);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal("ABC-2", page.Issues.Single().Key);
        Assert.Contains("perPage=10&page=2", handler.Uri);
        Assert.Contains("Assignee: me()", handler.Body);
        Assert.Contains("Resolution: empty()", handler.Body);
        Assert.Contains("Updated DESC", handler.Body);
        Assert.Equal("OAuth tracker", handler.Authorization);
        Assert.Equal("org", handler.Organization);
    }

    [Fact]
    public async Task TelegramRepeatedEditDoesNotReportTrackerFailure()
    {
        using var http = new HttpClient(new TelegramNotModifiedHandler());
        var telegram = new TelegramClient(http, "test-token");
        await telegram.EditAsync(42, 100, "same page", null, default);
    }

    [Fact]
    public async Task PlainTelegramMessageOmitsNullParseMode()
    {
        var handler = new TelegramSendHandler();
        using var http = new HttpClient(handler);
        var telegram = new TelegramClient(http, "test-token");
        await telegram.SendAsync(42, "Привет", null, false, default);
        Assert.DoesNotContain("parse_mode", handler.Body);
        Assert.DoesNotContain("reply_markup", handler.Body);
        using var payload = JsonDocument.Parse(handler.Body);
        Assert.Equal("Привет", payload.RootElement.GetProperty("text").GetString());
    }

    private static TelegramUpdate Message(string text, long from = 42) => new(1,
        new TelegramMessage(1, new TelegramChat(from, "private"), new TelegramUser(from), text), null);

    private sealed class FakeTracker : ITrackerClient
    {
        public List<int> RequestedPages { get; } = [];
        public bool Fail { get; init; }
        public Task<TrackerPage> GetMyIssuesAsync(int page, CancellationToken cancellationToken)
        {
            RequestedPages.Add(page);
            if (Fail) throw new HttpRequestException("test failure");
            return Task.FromResult(new TrackerPage([new TrackerIssue($"ABC-{page}", "Задача", "Открыт", null)], page, 3));
        }
    }

    private sealed class FakeTelegram : ITelegramClient
    {
        public List<(string Text, object? Markup)> Sent { get; } = [];
        public List<(string Text, object? Markup)> Edited { get; } = [];
        public List<string> AnsweredCallbacks { get; } = [];
        public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TelegramUpdate>>([]);
        public Task SendAsync(long chatId, string text, object? replyMarkup, bool html, CancellationToken cancellationToken)
        {
            Sent.Add((text, replyMarkup));
            return Task.CompletedTask;
        }
        public Task EditAsync(long chatId, int messageId, string text, object? replyMarkup, CancellationToken cancellationToken)
        {
            Edited.Add((text, replyMarkup));
            return Task.CompletedTask;
        }
        public Task AnswerCallbackAsync(string callbackId, CancellationToken cancellationToken)
        {
            AnsweredCallbacks.Add(callbackId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        public string Uri { get; private set; } = "";
        public string Body { get; private set; } = "";
        public string Authorization { get; private set; } = "";
        public string Organization { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri!.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.GetValues("Authorization").Single();
            Organization = request.Headers.GetValues("X-Cloud-Org-ID").Single();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"key\":\"ABC-2\",\"summary\":\"Тест\",\"status\":{\"display\":\"Открыт\"},\"updatedAt\":\"2026-09-30T09:00:00.000+0000\"}]", Encoding.UTF8, "application/json")
            };
            response.Headers.Add("X-Total-Pages", "3");
            return response;
        }
    }

    private sealed class TelegramNotModifiedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: message is not modified\"}",
                    Encoding.UTF8, "application/json")
            });
    }

    private sealed class TelegramSendHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true,\"result\":{}}", Encoding.UTF8, "application/json")
            };
        }
    }
}
