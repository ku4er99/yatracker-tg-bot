using System.Net;

namespace YaTrackerTelegramBot;

public sealed record RenderedPage(string Text, object? InlineKeyboard);

public static class IssueRenderer
{
    public static RenderedPage Render(TrackerPage page)
    {
        if (page.Issues.Count == 0)
            return new RenderedPage(page.Page == 1 ? "📭 Активных задач на вас нет." : "На этой странице задач нет. Вернитесь назад.",
                page.Page > 1 ? Keyboard(page.Page, page.TotalPages) : null);

        var lines = new List<string> { $"📋 <b>Задачи на мне</b> · {page.Page}/{page.TotalPages}", "Сначала недавно обновлённые", "" };
        for (var i = 0; i < page.Issues.Count; i++)
        {
            var issue = page.Issues[i];
            var key = WebUtility.HtmlEncode(issue.Key);
            var title = WebUtility.HtmlEncode(Truncate(issue.Summary, 110));
            var status = WebUtility.HtmlEncode(Truncate(issue.Status, 35));
            var date = issue.UpdatedAt?.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM.yyyy HH:mm") ?? "дата неизвестна";
            var urlKey = Uri.EscapeDataString(issue.Key);
            lines.Add($"<b>{(page.Page - 1) * 10 + i + 1}.</b> <a href=\"https://tracker.yandex.ru/{urlKey}\">{key}</a> · {title}");
            lines.Add($"    {status} · 🕒 {date} МСК");
        }
        return new RenderedPage(string.Join('\n', lines), Keyboard(page.Page, page.TotalPages));
    }

    private static object? Keyboard(int page, int totalPages)
    {
        var buttons = new List<object>();
        if (page > 1) buttons.Add(new { text = "◀️ Назад", callback_data = $"tasks:{page - 1}" });
        if (page < totalPages) buttons.Add(new { text = "Далее ▶️", callback_data = $"tasks:{page + 1}" });
        return buttons.Count == 0 ? null : new { inline_keyboard = new[] { buttons } };
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}
