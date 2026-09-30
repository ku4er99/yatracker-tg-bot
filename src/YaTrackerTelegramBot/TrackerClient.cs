using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YaTrackerTelegramBot;

public sealed record TrackerIssue(string Key, string Summary, string Status, DateTimeOffset? UpdatedAt);
public sealed record TrackerPage(IReadOnlyList<TrackerIssue> Issues, int Page, int TotalPages);

public interface ITrackerClient
{
    Task<TrackerPage> GetMyIssuesAsync(int page, CancellationToken cancellationToken);
}

public sealed class TrackerClient(HttpClient http, BotOptions options) : ITrackerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TrackerPage> GetMyIssuesAsync(int page, CancellationToken cancellationToken)
    {
        if (page < 1 || page > 1000) throw new ArgumentOutOfRangeException(nameof(page));
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.tracker.yandex.net/v3/issues/_search?perPage=10&page={page}");
        request.Headers.TryAddWithoutValidation("Authorization", $"OAuth {options.TrackerToken}");
        request.Headers.Add(options.IsCloudOrganization ? "X-Cloud-Org-ID" : "X-Org-ID", options.OrganizationId);
        request.Content = JsonContent.Create(new
        {
            query = "Assignee: me() Resolution: empty() \"Sort by\": Updated DESC"
        });
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<TrackerIssueDto>>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Трекер вернул пустой ответ.");

        var totalPages = response.Headers.TryGetValues("X-Total-Pages", out var values)
            && int.TryParse(values.FirstOrDefault(), out var count) ? count : page + (items.Count == 10 ? 1 : 0);
        var issues = items.Select(item => new TrackerIssue(item.Key ?? "?", item.Summary ?? "Без названия",
            item.Status?.Display ?? "Без статуса", ParseDate(item.UpdatedAt))).ToArray();
        return new TrackerPage(issues, page, Math.Max(1, totalPages));
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Length >= 5 && value[^5] is '+' or '-' &&
            value[^4..].All(char.IsDigit)) value = value.Insert(value.Length - 2, ":");
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
    }

    private sealed record TrackerIssueDto(
        [property: JsonPropertyName("key")] string? Key,
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("status")] TrackerStatusDto? Status,
        [property: JsonPropertyName("updatedAt")] string? UpdatedAt);

    private sealed record TrackerStatusDto([property: JsonPropertyName("display")] string? Display);
}
