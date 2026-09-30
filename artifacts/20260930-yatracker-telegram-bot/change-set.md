# Изменения и проверки

- .NET 10 long polling Telegram-бот; один сценарий «Задачи на мне» с 10 задачами на странице и сортировкой по обновлению.
- Поиск через API Яндекс Трекера от имени OAuth-пользователя; только активные задачи.
- Ограничение доступа по Telegram ID, понятные ответы при ошибках, экранирование HTML.
- Dockerfile, Compose, PR-проверки и деплой по SSH после push в `main`.
- `dotnet build YaTrackerTelegramBot.slnx -c Release`: успешно, 0 предупреждений.
- `dotnet test YaTrackerTelegramBot.slnx -c Release`: 7 пройдено, 0 ошибок.
- `docker build -t yatracker-tg-bot:local .`: успешно.
- `docker run` без конфигурации завершается с понятным сообщением об отсутствующей переменной.
- PR #1 в `main`: GitHub Actions job `verify` успешно прошёл; `deploy` пропущен до merge.
- Защита `development` и `main` настроена и проверена через GitHub API: PR и `verify` обязательны, действует для администратора.
- Живые Telegram/Трекер и SSH-деплой ожидают добавления секретов и создания бота.
