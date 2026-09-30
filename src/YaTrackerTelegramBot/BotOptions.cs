namespace YaTrackerTelegramBot;

public sealed record BotOptions(string TelegramToken, string TrackerToken, string OrganizationId,
    bool IsCloudOrganization, long AllowedTelegramUserId)
{
    public static BotOptions FromEnvironment()
    {
        static string Required(string key) => Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
            ? value : throw new InvalidOperationException($"Не задана переменная {key}.");

        var telegramId = Required("ALLOWED_TELEGRAM_USER_ID");
        if (!long.TryParse(telegramId, out var allowedUserId) || allowedUserId <= 0)
            throw new InvalidOperationException("ALLOWED_TELEGRAM_USER_ID должен быть положительным числом.");

        var organizationType = Required("TRACKER_ORG_TYPE");
        if (organizationType is not ("cloud" or "360"))
            throw new InvalidOperationException("TRACKER_ORG_TYPE должен быть cloud или 360.");

        return new BotOptions(Required("TELEGRAM_BOT_TOKEN"), Required("TRACKER_OAUTH_TOKEN"),
            Required("TRACKER_ORG_ID"), organizationType == "cloud", allowedUserId);
    }
}
