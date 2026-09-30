# Яндекс Трекер → Telegram

Личный Telegram-бот на .NET 10. По кнопке «📋 Задачи на мне» он показывает открытые задачи, назначенные владельцу OAuth-токена Яндекс Трекера. Список отсортирован по времени обновления, свежие сверху. На странице 10 задач; кнопки «Назад» и «Далее» редактируют то же сообщение. У каждой задачи есть ссылка, статус и время обновления по Москве.

Бот отвечает на `/start`, `/tasks` и непонятный ввод. Доступ к задачам разрешён только одному Telegram user ID и только в личном чате. Для чтения используется [официальный API поиска задач](https://yandex.ru/support/tracker/ru/api/issues/search-issues); завершённые задачи исключены условием `Resolution: empty()`.

## Получить данные для настройки

### Telegram BotFather

1. Откройте [@BotFather](https://t.me/BotFather), отправьте `/newbot`, задайте имя и уникальный username, заканчивающийся на `bot`.
2. Сохраните выданный токен как `TELEGRAM_BOT_TOKEN` в секретах GitHub. Не помещайте токен в issue, PR, код или чат.
3. Через `/setcommands` задайте команды:

   ```text
   start - Открыть меню
   tasks - Показать мои задачи
   ```

4. По желанию через `/setdescription` задайте «Показывает мои активные задачи из Яндекс Трекера» и через `/setabouttext` — «Мои задачи из Трекера».
5. Через `/setjoingroups` отключите добавление бота в группы. Бот работает только в личном чате; дополнительных настроек webhook не требуется.
6. Узнайте числовой Telegram user ID своего аккаунта (например, через `@userinfobot`) и сохраните его как `ALLOWED_TELEGRAM_USER_ID`.

После создания бота сообщите мне только его **username/ссылку** и подтверждение, что `TELEGRAM_BOT_TOKEN` и `ALLOWED_TELEGRAM_USER_ID` добавлены в GitHub Secrets. Сам токен присылать не нужно.

### Яндекс Трекер

По [инструкции Яндекса](https://yandex.ru/support/tracker/ru/api/access) создайте приложение на [oauth.yandex.ru](https://oauth.yandex.ru) с правом **«Чтение данных Трекера» (`tracker:read`)**. Получите OAuth-токен для своего аккаунта и сохраните его как `TRACKER_OAUTH_TOKEN` в GitHub Secrets. Бот будет видеть те же задачи, что видит этот аккаунт.

Идентификатор организации возьмите в Трекере в «Администрирование → Организации». Сохраните его как `TRACKER_ORG_ID`. Для организации Yandex Cloud/Identity Hub установите `TRACKER_ORG_TYPE=cloud`, для Яндекс 360 — `TRACKER_ORG_TYPE=360`. Используется соответствующий заголовок `X-Cloud-Org-ID` или `X-Org-ID`.

## Локальный запуск

Скопируйте `.env.example` в `.env`, подставьте свои значения. Файл `.env` исключён из Git и Docker-образа. Для запуска без Docker загрузите переменные из `.env` в окружение и выполните:

```sh
dotnet run --project src/YaTrackerTelegramBot
```

Для запуска через Docker Compose:

```sh
docker compose up -d --build
docker compose logs -f bot
```

Остановка: `docker compose down`. Боту нужен исходящий HTTPS к `api.telegram.org` и `api.tracker.yandex.net`, входящий порт не нужен. Запускайте только одну копию с одним токеном: Telegram long polling не поддерживает два конкурирующих опросчика.

## CI/CD

Есть две ветки: `development` и `main`. PR в любую из них запускает `dotnet restore`, `build`, `test`. Push в `main` после успешных проверок собирает Docker-образ на GitHub runner, проверяет ED25519-отпечаток учебного сервера, копирует образ, исходный код и переменные по SSH в `/home/<SSH_USER>/telegram-bot`, загружает образ через `docker load` и запускает `docker compose up -d --no-build`. Затем CI проверяет через API токен бота и поиск задач Трекера. Проверка не выводит токены и задачи в журнал. Для деплоя на сервере у пользователя должны работать Docker и Compose.

В репозитории GitHub откройте **Settings → Secrets and variables → Actions → New repository secret** и добавьте:

| Секрет | Значение |
| --- | --- |
| `SSH_HOST` | Адрес учебного сервера |
| `SSH_USER` | Свой логин на сервере |
| `SSH_PASSWORD` | Текущий пароль после обязательной смены временного |
| `TELEGRAM_BOT_TOKEN` | Токен BotFather |
| `TRACKER_OAUTH_TOKEN` | OAuth-токен Трекера с `tracker:read` |
| `TRACKER_ORG_ID` | ID организации Трекера |
| `TRACKER_ORG_TYPE` | `cloud` или `360` |
| `ALLOWED_TELEGRAM_USER_ID` | Свой числовой Telegram user ID |

Настройте правила веток: для `development` и `main` требовать PR и успешный job `verify`; прямые push в `main` запретить. Текущую версию смотрите в GitHub Actions и `git rev-parse HEAD`. Журнал сервера: `cd ~/telegram-bot && docker compose logs --tail=100 bot`. Откат: верните предыдущий проверенный commit в `main` отдельным PR; merge запустит повторный деплой.

## Проверки

```sh
dotnet build YaTrackerTelegramBot.slnx -c Release
dotnet test YaTrackerTelegramBot.slnx -c Release
```

Тесты проверяют `/start`, список и пагинацию, неверный ввод, ограничение доступа, сообщение при ошибке Трекера, экранирование HTML и формат поиска API. Проверка с реальными Telegram и Трекером требует токенов и выполняется после настройки секретов.

Учебные артефакты и состояние выполнения находятся в `artifacts/20260930-yatracker-telegram-bot/`, итог — в `report.md`.
