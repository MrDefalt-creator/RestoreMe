# RestoreMe

[🇬🇧 English](README.md) · 🇷🇺 Русский

RestoreMe — это self-hosted система управления резервными копиями. Основные части:
- `Backup.Server.Api` — бэкенд на ASP.NET Core
- `Backup.Agent.Worker` — агент: регистрируется, синхронизирует политики, шлёт heartbeat и выполняет бэкапы
- `Frontend-2.0` — админ-панель на React для операторов и администраторов

Используется:
- PostgreSQL для реляционных данных
- MinIO для объектного хранилища
- Docker Compose для локального запуска всего стека

> [!WARNING]
> Прочитайте этот README и [docker-compose/README.ru.md](docker-compose/README.ru.md) перед запуском стека. Репозиторий специально содержит `.env` и стартовые секреты в Docker Compose для быстрого первого запуска, но все дефолтные пароли и токены **должны** быть заменены до публичного или production-подобного развёртывания.

## Структура репозитория

```text
RestorMe/
  Backup/
    Backup.Server.Api/
    Backup.Server.Application/
    Backup.Server.Domain/
    Backup.Server.Infrastructure/
    Backup.Server.Tests/         ← xUnit-интеграционные тесты
    Backup.Agent.Worker/
    Backup.Shared.Contracts/
  Frontend-2.0/                  ← админ-панель
  docker-compose/
    docker-compose.yml
    docker-compose.override.yml  ← авто-подхватывается локально
    docker-compose.prod.yml      ← опциональный prod-overlay
    .env
    secrets/
  installers/                    ← скрипты установки агента (Linux/Windows)
  .github/workflows/             ← CI
  README.md / README.ru.md
```

## Основные возможности

### Backend
- слоистая архитектура: API / Application / Domain / Infrastructure / Shared.Contracts
- поток одобрения агентов через pending-таблицу
- обработка heartbeat
- CRUD политик для файловых и логических БД-бэкапов
- жизненный цикл задач: старт / ошибка / успех
- хранение артефактов в MinIO и скачивание через бэкенд
- автоматическое применение EF Core миграций при старте
- поддержка file-based секретов через суффикс `*_FILE`
- JWT-аутентификация для пользователей панели в HttpOnly-куке
- роли: `admin`, `operator`, `viewer`
- защита bootstrap-регистрации агента через enrollment-токен + отдельные access-токены агента
- guardrails в Production: бэкенд отказывается стартовать с дефолтными dev-секретами
- мультиканальные уведомления (Webhook / Telegram / Slack / Discord) с подпиской по типам событий, секреты зашифрованы at-rest
- автоматическое отключение политики после серии подряд идущих провалов
- определение offline / back-online агента фоновым health-sweep'ом
- политики ретенции (по возрасту / количеству / суммарному размеру) с фоновой очисткой
- проверка целостности артефактов — пересчёт SHA256 при загрузке, проверка по запросу и плановый фоновый scrub
- audit log критических действий (создание/удаление пользователей, одобрение/отзыв агентов)

### Agent
- получает `AgentId` после pending-регистрации или переиспользует сохранённый
- хранит локальное состояние в `state/agent-state.json` (зашифровано ASP.NET Core DataProtection)
- хранит адрес бэкенда и access-токен агента локально
- шлёт heartbeat и периодически синхронизирует политики
- выполняет файловые бэкапы
- выполняет логические дампы PostgreSQL и MySQL
- загружает payload напрямую в объектное хранилище по presigned URL, выдаваемому бэкендом

### Frontend
- продвинутая премиум-UI на Radix UI
- тёмная и светлая темы
- дашборд с трендами, картой защиты и attention-items
- страница агентов с фильтрами, покрытием политиками, диалогом деталей
- мастер установки агента (one-liner — скрипт+бинарник берутся с самого бэкенда)
- approve/reject для pending-агентов
- страницы политик, задач, бэкапов/артефактов синхронизированы с DTOs бэкенда
- на странице политик виден статус «Auto-disabled» с повторным включением в один клик
- настройки ретенции в форме политики (хранение по возрасту / количеству / суммарному размеру)
- на странице бэкапов/артефактов — бейдж целостности артефакта с действием «Verify now»
- страница каналов уведомлений (`/notifications`, только для admin) — добавление/редактирование/тест каналов Webhook, Telegram, Slack, Discord; там же настройки расписания scrub целостности
- автоматический polling и инвалидация кэша
- audit-log (только для admin)

## Требования

### Локальная разработка без Docker
- .NET SDK 10
- Node.js 22+
- Yarn 1.x
- PostgreSQL
- MinIO

### Локальный полный стек с Docker
- Docker Desktop
- Docker Compose

## Рекомендуемые способы запуска

### Вариант 1. Полный стек через Docker Compose

Самый простой и рекомендуемый локальный путь.

> [!WARNING]
> Перед запуском Compose вне приватной локальной среды замените все значения в `docker-compose/secrets`, поменяйте пароль bootstrap-администратора после первого входа и замените JWT/enrollment-токены в конфигах бэкенда и агента.

```powershell
cd docker-compose
docker compose up --build
```

`docker-compose.override.yml` подхватывается автоматически для локальной разработки (выставляет `ASPNETCORE_ENVIRONMENT=Development`, открывает консоль MinIO). Для production-подобного запуска используйте prod-overlay:

```powershell
cd docker-compose
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

Необходимые переменные окружения (или файл `.env.prod` рядом с compose-файлами):
- `CORS_ORIGIN` — публичный origin фронтенда (например, `https://restoreme.example.com`)
- `API_PUBLIC_URL` — публичный URL бэкенда, который вшивается в Vite-бандл и в CSP `connect-src` фронтенда

Дефолтные порты:
- frontend: `http://localhost:5173`
- backend: `http://localhost:8080`
- MinIO API: `http://localhost:9000`
- MinIO Console: `http://localhost:9001`
- PostgreSQL: `localhost:5432`

### Вариант 2. Ручной запуск

Backend:
```powershell
cd Backup
dotnet run --project .\Backup.Server.Api\Backup.Server.Api.csproj
```

Frontend:
```powershell
cd Frontend-2.0
yarn
yarn dev
```

Agent:
```powershell
cd Backup
dotnet run --project .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
```

## Чек-лист первого развёртывания

1. Прочитайте [docker-compose/README.ru.md](docker-compose/README.ru.md).
2. Замените стартовые значения в [docker-compose/secrets](docker-compose/secrets).
3. Проверьте [docker-compose/.env](docker-compose/.env), если дефолтные порты заняты.
4. Запустите стек: `docker compose up --build`.
5. Дождитесь применения миграций бэкендом.
6. Откройте `http://localhost:5173`.
7. Войдите под bootstrap-администратором.
8. Смените пароль bootstrap-администратора.
9. При необходимости создайте дополнительных пользователей.
10. Запустите одного или нескольких агентов отдельно.
11. Одобрите pending-агентов в панели.
12. Создайте политики и проверьте задачи/артефакты.

## Секреты и конфиденциальная конфигурация

### Каталог секретов Compose

Локальный Docker-старт ожидает следующие файлы в [docker-compose/secrets](docker-compose/secrets):
- `postgres-password.txt`
- `postgres-connection.txt`
- `minio-access-key.txt`
- `minio-secret-key.txt`

> [!WARNING]
> Эти файлы закоммичены только как локальные стартовые значения. Воспринимайте их как шаблоны с рабочими дефолтами: замените перед публикацией стека в любой shared/demo/production среде.

### Как бэкенд читает секреты

Бэкенд поддерживает и обычные значения, и file-based секреты. Сначала проверяется `*_FILE`-сосед: если он указывает на существующий файл — читается оттуда; иначе используется обычное значение.

Примеры:
- `ConnectionStrings:DefaultConnection` / `ConnectionStrings:DefaultConnection_FILE`
- `Storage:AccessKey` / `Storage:AccessKey_FILE`
- `Storage:SecretKey` / `Storage:SecretKey_FILE`

Смысл:
- обычные значения удобны для быстрой локальной разработки
- `*_FILE` — предпочтительный путь, когда Docker монтирует секреты как файлы

### Важные секции конфига бэкенда

Главный конфиг:
- [Backup/Backup.Server.Api/appsettings.json](Backup/Backup.Server.Api/appsettings.json)

Полностью аннотированный референс с каждым ключом и placeholder'ом:
- [Backup/Backup.Server.Api/appsettings.example.json](Backup/Backup.Server.Api/appsettings.example.json)

Важные секции:
- `ConnectionStrings:DefaultConnection` / `ConnectionStrings:DefaultConnection_FILE`
- `Storage:Endpoint` (внутренний адрес MinIO для бэкенда)
- `Storage:PublicEndpoint` (внешний адрес MinIO, вшитый в URL-ы для агентов)
- `Storage:AccessKey` / `Storage:AccessKey_FILE`
- `Storage:SecretKey` / `Storage:SecretKey_FILE`
- `Storage:BucketName`, `Storage:UseSsl`
- `Storage:UseAdaptiveExpiry`, `Storage:AdaptiveBaseSeconds`, `Storage:AdaptivePerGbSeconds` — адаптивный TTL presigned URL (см. ниже)
- `Storage:UploadUrlExpirySeconds`, `Storage:DownloadUrlExpirySeconds` — статические fallback-ы
- `Storage:VerifyChecksumBeforeComplete`, `Storage:ChecksumVerifyMaxBytes` — гейт целостности артефакта при загрузке (см. [Проверка целостности артефактов](#проверка-целостности-артефактов))
- `Retention:CleanupIntervalHours` — частота фоновой очистки ретенции
- `Integrity:CheckIntervalSeconds` — как часто воркер проверяет, не пора ли плановый scrub целостности (само расписание управляется admin'ом в рантайме, не в конфиге)
- `Jwt:Issuer`, `Jwt:Audience`
- `Jwt:SigningKey` — ключ подписи user-токенов
- `Jwt:AgentSigningKey` — опциональный отдельный ключ для agent-токенов; ротация не инвалидирует user-сессии
- `AgentEnrollment:EnrollmentToken`
- `Cors:AllowedOrigins` — обязательно в Production; бэкенд отказывается стартовать с пустым или loopback-only списком

> [!NOTE]
> Уведомления **больше не настраиваются через `appsettings.json`**. Старый одиночный `Notifications:FailureWebhookUrl` заменён каналами уведомлений, которыми управляет admin, и которые хранятся в БД (см. [Уведомления](#уведомления) ниже).

### Production-замечание

Для локального развёртывания file-based Docker-секреты — заметное улучшение по сравнению с plain YAML. Для реального production предпочтительнее выделенный secret-manager или платформенное secret-хранилище.

### Production guardrails при старте

При `ASPNETCORE_ENVIRONMENT=Production` бэкенд отказывается стартовать, если выполняется любое из:
- `Jwt:SigningKey` — известный dev-дефолт или короче 32 байт
- `Jwt:AgentSigningKey` задан и совпадает с `Jwt:SigningKey` или короче 32 байт
- `AgentEnrollment:EnrollmentToken` пуст или совпадает с известным dev-дефолтом
- `Cors:AllowedOrigins` пуст
- `Cors:AllowedOrigins` содержит только loopback-хосты (localhost / 127.0.0.1 / ::1)

Это намеренно — guardrails не дают dev-дефолтам незаметно уехать в реальный environment.

Production также включает `UseHsts()` (30 дней, включая subdomains) и `UseHttpsRedirection()`. RestoreMe рассчитан на запуск за TLS-терминирующим reverse-proxy (Caddy, Traefik, nginx).

### Адаптивный TTL presigned URL

Агенты общаются с MinIO через presigned URLs. По умолчанию срок жизни ссылки масштабируется по размеру payload — маленькие задачи получают короткие окна (безопаснее), большие — часы или дни (всё равно успевают):

```
expiry = AdaptiveBaseSeconds + sizeGB * AdaptivePerGbSeconds   (cap 7 дней)
```

Дефолты — `AdaptiveBaseSeconds=600`, `AdaptivePerGbSeconds=300`:

| Payload | TTL ссылки |
|---|---|
| 1 GB | ~15 минут |
| 10 GB | ~1 час |
| 100 GB | ~8.5 часов |
| 1 TB | ~3.6 дня |

`Storage:UseAdaptiveExpiry: false` — переключение на статический `Storage:UploadUrlExpirySeconds`. `Storage:DownloadUrlExpirySeconds` задаёт TTL для restore-download отдельно.

### Уведомления

RestoreMe поставляется с мультиканальной системой уведомлений. Каналами управляет admin на странице `/notifications` (только для admin, эндпоинты `/api/notification-channels`) — в `appsettings.json` настроек уведомлений нет.

**Типы каналов:** `Webhook` (generic с HMAC-подписью), `Telegram`, `Slack`, `Discord`.

**Типы событий**, на которые можно подписать канал (пустая подписка = получать все):
- `BackupFailed`, `RestoreFailed`, `BackupCompleted`
- `AgentOffline`, `AgentBackOnline`
- `PolicyAutoDisabled`
- `RetentionCleaned`, `IntegrityCheckFailed`

Как это работает:
- У каждого канала свой JSON-блоб `Settings` под тип (bot token / webhook URL / общий секрет). Весь блоб **зашифрован at-rest** через ASP.NET Core DataProtection — секреты API наружу не отдаёт.
- На событие диспетчер веером рассылает его всем включённым каналам, подписанным на этот тип, и гонит каждый через соответствующий адаптер. Доставка **best-effort и изолирована по каналам**: один сломанный Slack-URL не глушит Telegram, а сбой уведомления никогда не блокирует задачу, которая его вызвала.
- Каждая попытка доставки пишется в audit log как `notification.sent` / `notification.failed` (тело сообщения и секреты намеренно исключены).
- Кнопка admin «Test channel» шлёт пробное событие через реальный адаптер — конфигурацию можно проверить.

**Подпись generic-вебхука** — если у канала `Webhook` задан секрет, запрос подписывается:

```
X-RestoreMe-Signature: sha256=<hex от HMAC-SHA256(body, secret)>
```

Получатель должен сравнивать constant-time с тем же digest от сырых байт тела. У HTTP-клиента каждого адаптера ограниченный timeout — медленный приёмник не заблокирует цепочку рассылки.

### Авто-отключение политики

Политика, упавшая **3 раза подряд**, автоматически выключается (`IsEnabled=false`), помечается `AutoDisabledAt` и причиной последней ошибки, пишется в audit log как `policy.auto_disabled` и анонсируется событием `PolicyAutoDisabled` — чтобы сломанный источник или плохие креды не спамили audit log и уведомления каждый интервал. Успешный бэкап сбрасывает серию. Фронт помечает такие политики бейджем «Auto-disabled»; повторное включение (toggle или сохранение включённой) сбрасывает счётчик, и следующий провал начинает серию заново.

### Ретенция

У каждой политики есть три опциональных параметра ретенции: `RetentionDays`, `RetentionMaxCount` (хранить новейшие N) и `RetentionMaxTotalBytes` (бюджет по размеру). Фоновый `RetentionCleanupService` запускается каждые `Retention:CleanupIntervalHours` (по умолчанию 24ч) и удаляет артефакты, выпавшие за рамки правил политики:

- newest-first, по каждой политике; новейший артефакт **никогда** не удаляется — политика всегда хранит хотя бы одну копию («floor»)
- **keep-union** — если заданы дни и/или количество, артефакт выживает, если он в пределах `RetentionDays` **или** среди новейших `RetentionMaxCount`
- **жёсткий size-cap** — среди выживших, newest-first, всё, чей накопленный размер превышает `RetentionMaxTotalBytes`, удаляется (кроме floor)
- политика без настроенных правил ретенции не удаляет ничего

Каждое удаление убирает объект из MinIO, затем строку из БД, пишется в audit log как `retention.deleted` (системное действие, без actor'а) и порождает событие уведомления `RetentionCleaned`. Поля ретенции редактируются в форме политики на странице Policies.

### Проверка целостности артефактов

RestoreMe защищает от незаметно повреждённых или обрезанных артефактов в нескольких точках:

- **При загрузке** — когда агент сообщает о завершённой загрузке, бэкенд перечитывает объект из MinIO и пересчитывает SHA256 (потоково, через incremental hash — артефакт целиком не буферизуется) и сверяет с checksum, заявленным агентом. При `Storage:VerifyChecksumBeforeComplete=true` (по умолчанию) несовпадение проваливает задачу — она никогда не станет `Completed` с битым артефактом; успех пишется в audit log как `artifact.verified`. `Storage:ChecksumVerifyMaxBytes` (null = без лимита) пропускает пересчёт для объектов больше лимита — наличие + размер всё равно проверяются, а пропуск логируется как `artifact.verify_skipped`. Проверка также пропускается, если агент не сообщил checksum.
- **По запросу** — оператор/админ может запустить `POST /api/backupartifacts/{id}/verify` («Verify now») со страницы бэкапов/артефактов; у каждого артефакта виден бейдж целостности (verified / failed / unverified).
- **По расписанию** — фоновый scrub периодически перепроверяет хранимые артефакты. Расписание (включено, интервал, время запуска, размер батча) **управляется admin'ом в рантайме** через `GET/PUT /api/integrity-settings` и карточку расписания scrub на странице `/notifications` — оно *не* зашито в конфиг. `Integrity:CheckIntervalSeconds` лишь задаёт, как часто воркер просыпается проверить, не пора ли запуск. Провалившийся scrub порождает событие `IntegrityCheckFailed`.
- **Перед restore** — агент перепроверяет checksum артефакта перед применением restore, чтобы повреждённая копия никогда не записалась поверх живого target'а.

### Health-эндпоинт

`GET /health` возвращает `200` только когда:
- бэкенд достучался до PostgreSQL (`AddDbContextCheck`)
- бэкенд достучался до MinIO (custom-проба через `BucketExistsAsync`)

Docker Compose использует этот же эндпоинт для healthcheck контейнера; бэкенд стартует только после того, как `db` и `minio` стали `service_healthy`.

## Аутентификация и роли

### Bootstrap-администратор

В `Development` система создаёт ровно одного начального администратора — **только если таблица пользователей пуста**.

Текущие dev-креды:
- `admin / Admin123!`

Seed-админ создаётся с флагом `MustChangePassword=true`. Это **рекомендательный nudge, а не серверный гейт** — пока флаг стоит, бэкенд не блокирует ни один эндпоинт. Фронт показывает его как toast при логине, баннер на странице Account и шаг онбординга «Set a personal password», чтобы подтолкнуть оператора сменить дефолтный пароль, но ничего не форсит. Дефолтные/временные креды — это ответственность за hardening деплоя, а не гарантия API.

Тот же флаг ставится, когда админ сбрасывает чужой пароль, так что целевому пользователю при следующем входе предлагают выбрать свой пароль. Флаг снимается автоматически при следующей успешной смене пароля.

Источник:
- [Backup/Backup.Server.Api/appsettings.Development.json](Backup/Backup.Server.Api/appsettings.Development.json)

### Роли панели

- `viewer` — только чтение workspace
- `operator` — работа с агентами, политиками, задачами и артефактами
- `admin` — полный доступ, включая управление пользователями

### Правила управления пользователями

Защитные инварианты:
- хотя бы один активный администратор должен оставаться в системе
- текущий вошедший аккаунт нельзя удалить из админ-таблицы
- текущий вошедший аккаунт нельзя выключить из админ-таблицы
- текущий вошедший аккаунт нельзя сменить себе роль из админ-таблицы
- любой вошедший пользователь может сменить свой пароль на странице Account
- только администраторы могут создавать пользователей, менять чужие пароли, выключать и удалять пользователей

### Хранение сессионного токена

Access-JWT живёт в HttpOnly куке `access_token`, которую выставляет бэкенд. JavaScript на фронтенде токен не видит — XSS-payload не сможет его выкрасть. Кука с `SameSite=Strict` и `Secure` автоматически вне Development. Фронт настроен на `withCredentials: true`.

Маленький профиль текущего пользователя (id, username, role, флаг `mustChangePassword`) хранится на фронте, чтобы UI знал, какие страницы рендерить.

### Поведение Remember me

Логин-страница позволяет выбрать персистентность сессии:
- `Remember me` включён — кука получает явный `Expires`, равный времени жизни JWT
- `Remember me` выключен — сессионная кука, исчезает при закрытии браузера
- кэш профиля на фронте следует тому же выбору (localStorage vs sessionStorage)

### Инвалидация пароля и сессии

Каждый user-JWT несёт claim `stamp`, привязанный к `AppUser.SecurityStamp`. При любой смене пароля (самим пользователем или сбросе админом) stamp перегенерируется на сервере; любой токен, выданный до смены, моментально не проходит валидацию при следующем вызове. Проверка кэшируется в памяти 30 секунд.

### Отзыв агента

Админы могут отозвать конкретного агента со страницы Agents (видно только `admin`). Бэкенд увеличивает `Agent.TokenVersion`; JWT агента несёт предыдущий version в `tokver` и проваливается при следующем вызове. Агент обязан переэнролиться по enrollment-токену. Действие пишется в audit log как `agent.revoke`.

### Audit log

Бэкенд пишет audit-записи на каждое критическое действие: создание / удаление / смена статуса / смена роли / сброс пароля пользователя, approve / reject / revoke агента, авто-отключение политики (`policy.auto_disabled`), результаты проверки целостности артефактов (`artifact.verified` / `artifact.verify_skipped`), удаления по ретенции (`retention.deleted`) и результаты доставки уведомлений (`notification.sent` / `notification.failed`). Admin-only `GET /api/audit-logs` возвращает paginated-список с username actor'а, join'нутым на сервере. Фронт показывает read-only страницу `/audit-log` (только для admin) с фильтром по action.

## Установка агента

Агент поставляется как self-contained one-file бинарь для `linux-x64`, `linux-arm64`, `win-x64`. .NET runtime на целевой машине не нужен. Мастер установки в Frontend-2.0 печатает готовый one-liner, ссылающийся на ваш собственный бэкенд; ниже — ручные эквиваленты.

### Установка на Linux

```bash
sudo curl -fsSL https://<your-backend>/installers/install-agent.sh -o /tmp/install-agent.sh
sudo bash /tmp/install-agent.sh \
  --server https://<your-backend> \
  --token <enrollment-token>
```

Что делает:
- определяет архитектуру (`x86_64` → `linux-x64`, `aarch64` → `linux-arm64`)
- скачивает бинарь в `/opt/restoreme-agent/restoreme-agent`
- пишет `/etc/restoreme-agent/config.env` с `RESTOREME_SERVER`, `RESTOREME_ENROLLMENT_TOKEN`, `RESTOREME_STATE_DIR` (mode `0600`)
- создаёт `/var/lib/restoreme-agent/state/`
- ставит и включает `restoreme-agent.service` через systemd

Полезные флаги:
- `--state-dir /custom/path` — другое расположение state-каталога
- `--service-user restoreme` — запускать агент под отдельным non-root пользователем

Проверка:

```bash
sudo systemctl status restoreme-agent
sudo journalctl -u restoreme-agent -f
```

Удаление:

```bash
sudo bash /tmp/install-agent.sh --uninstall          # state остаётся
sudo bash /tmp/install-agent.sh --uninstall --purge  # и state удалить
```

### Установка на Windows

Из elevated PowerShell:

```powershell
$installer = "$env:TEMP\install-agent.ps1"
Invoke-WebRequest `
  -Uri https://<your-backend>/installers/install-agent.ps1 `
  -OutFile $installer -UseBasicParsing
& $installer -Server https://<your-backend> -Token <enrollment-token>
```

Что делает:
- скачивает `restoreme-agent-win-x64.exe` в `C:\Program Files\RestoreMe\Agent\restoreme-agent.exe`
- создаёт `C:\ProgramData\RestoreMe\Agent\state\`
- регистрирует `RestoreMeAgent` Windows Service (автозапуск, restart-on-failure)
- пишет `RESTOREME_SERVER` / `RESTOREME_ENROLLMENT_TOKEN` / `RESTOREME_STATE_DIR` в registry-environment сервиса
- запускает сервис

Полезные параметры:
- `-StateDir 'D:\RestoreMe\state'` — перенести state вне `%ProgramData%`

Проверка:

```powershell
Get-Service RestoreMeAgent
Get-EventLog -LogName Application -Source 'RestoreMe*' -Newest 20
```

Удаление:

```powershell
& $installer -Uninstall         # state остаётся
& $installer -Uninstall -Purge  # и state удалить
```

### Дефолтные state-каталоги

Если не задан ни `--state-dir`, ни `RESTOREME_STATE_DIR`, агент выбирает OS-дефолт. Fallback на `<AppContext.BaseDirectory>/state` — только если дефолт недоступен на запись (типично для `dotnet run` из dev-чекаута):

| ОС | Дефолтный state-каталог |
|---|---|
| Linux | `/var/lib/restoreme-agent/state` |
| Windows | `%ProgramData%\RestoreMe\Agent\state` |
| macOS | `~/Library/Application Support/RestoreMe/Agent/state` |

Стартовая лог-строка `state directory: <path> (source: <origin>)` всегда называет реально использованный путь.

## Модель безопасности агента

### Bootstrap и обычная работа

1. Агент использует `Api:EnrollmentToken` для начальной регистрации и восстановления доступа.
2. После одобрения бэкенд выдаёт отдельный access-токен агента.
3. Агент сохраняет токен в локальном state и использует его для:
   - heartbeat
   - синхронизации политик
   - старта/завершения/ошибки backup-задач
   - регистрации артефактов
   - запросов upload-ticket

### Конфиг агента

- [Backup/Backup.Agent.Worker/appsettings.json](Backup/Backup.Agent.Worker/appsettings.json)

Важные настройки:
- `Api:BaseUrl`
- `Api:EnrollmentToken`
- `Agent:AgentId`
- `Agent:HeartbeatIntervalSeconds`
- `Agent:PolicySyncIntervalSeconds`
- `Agent:PostgreSqlDumpCommand`
- `Agent:MySqlDumpCommand`

> [!WARNING]
> Замените `AgentEnrollment:EnrollmentToken` на бэкенде и `Api:EnrollmentToken` на каждом агенте до использования системы вне локального dev.

### Запуск агента против удалённого бэкенда

Адрес и enrollment-токен агент читает из трёх источников по приоритету:

1. **CLI-флаги** — `--server <url>`, `--enrollment-token <token>`
2. **Окружение** — `RESTOREME_SERVER`, `RESTOREME_ENROLLMENT_TOKEN`
3. **`appsettings.json`** — `Api:BaseUrl`, `Api:EnrollmentToken`

```powershell
BackupAgent --server http://my-backend:8080 --enrollment-token <token>
```

```bash
RESTOREME_SERVER=http://my-backend:8080 \
RESTOREME_ENROLLMENT_TOKEN=<token> \
  ./BackupAgent
```

Агент сохраняет резолвленный URL в `state/agent-state.json`, так что последующие запуски используют тот же бэкенд. При передаче `--server` с другим URL агент обновляет state и логирует `WARNING`.

### Сброс состояния агента

Если агент завис на старом URL, отозванном токене или просто хочется чистого старта:

```powershell
BackupAgent --reset-state
```

Или один раз `RESTOREME_RESET_STATE=1`. Это очищает `state/agent-state.json` и `state/keys/` до старта — следующий запуск делает свежий enrollment. Комбинируйте с `--server` / `--enrollment-token`, чтобы заодно перенаправить.

### Локальное состояние агента

- `state/agent-state.json` — зашифровано ASP.NET Core DataProtection (содержит `AgentId`, `ServerAddress`, `AccessToken`)
- `state/keys/` — key ring DataProtection (сохраняется между рестартами через `PersistKeysToFileSystem` + `SetApplicationName("RestoreMe.Agent")`)

Поведение:
- CLI/ENV overrides выигрывают у локального state — оператор контролирует процесс без правки файлов
- если у агента есть `AgentId`, но нет access-токена, он может восстановить токен через enrollment-flow
- в Docker монтируйте `state/` (включая `state/keys/`) на volume, чтобы зашифрованный state переживал пересоздание контейнера

### Типичные ошибки агента

| Симптом | Вероятная причина | Что сделать |
|---|---|---|
| Лог `Cannot reach RestoreMe backend` | Неправильный URL или бэкенд недоступен | `BackupAgent --server <correct-url> --reset-state` |
| `Backend rejected the agent token` (401 на heartbeat) | Агент отозван в панели, ротирован JWT-ключ или дрейф token version | `BackupAgent --server <url> --enrollment-token <token> --reset-state` |
| Агент после смены конфига продолжает ходить на localhost | Старый `ServerAddress` в state | `--server <url>` для override или `--reset-state` для очистки |
| `Api:BaseUrl is not configured` | Первый старт без CLI/ENV/конфига | Передать `--server <url>` или `RESTOREME_SERVER` |

### Resilience агента

HTTP-клиенты агента используют .NET standard resilience handler — retry с exponential backoff, circuit breaker, per-attempt и total timeouts. Транзиентные сетевые сбои больше не роняют heartbeat и sync навсегда.

### Безопасность restore

Перед перезаписью любого target'а файлового restore агент переименовывает существующий путь в `{path}.pre-restore-{utcTimestamp}` — плохой restore можно откатить вручную. ZIP-архивы распаковываются с per-entry path-проверкой (zip-slip guard) — вредоносный или повреждённый артефакт с записями вида `../../etc/shadow` отвергается до записи каких-либо файлов.

## Модель адресации хранилища

Важны два адреса:
- `Storage:Endpoint` — внутренний адрес MinIO для бэкенда
- `Storage:PublicEndpoint` — внешний адрес, который попадает в upload-URL для агентов

### Простой случай

В типовом случае агенту достаточно адреса бэкенда.

Пример:
- backend: `http://my-server:8080`
- storage: `http://my-server:9000`

Здесь бэкенд автоматически сформирует корректные upload-URL.

### Когда `Storage:PublicEndpoint` нужно задать явно

Задавайте явно, если:
- бэкенд и хранилище опубликованы на разных доменах
- хранилище опубликовано через ещё один reverse-proxy
- агент ходит к бэкенду через один адрес, а к хранилищу — через другой

## База данных и миграции

Миграции лежат в:
- [Backup/Backup.Server.Infrastructure/Migrations](Backup/Backup.Server.Infrastructure/Migrations)

Поведение:
- бэкенд применяет миграции автоматически при старте
- пустая БД инициализируется автоматически
- актуальная БД продолжает старт без действий

Создать новую миграцию вручную:
```powershell
cd Backup
dotnet ef migrations add MigrationName --project .\Backup.Server.Infrastructure\Backup.Server.Infrastructure.csproj --startup-project .\Backup.Server.Api\Backup.Server.Api.csproj --output-dir Migrations
```

## Тесты бэкенда (xUnit)

Тестовый проект — `Backup/Backup.Server.Tests/`. Использует SQLite + DataProtection.

```powershell
cd Backup
dotnet test BackupSystem.slnx                                       # вся solution
dotnet test .\Backup.Server.Tests\Backup.Server.Tests.csproj        # только тесты
dotnet test --filter "FullyQualifiedName~AgentSelectiveDelete"      # один класс/тест
```

CI (`.github/workflows/ci.yml`) на каждом push прогоняет `restore` → `build --configuration Release` → `test --no-build` для бэкенда и `yarn install` → `lint` → `typecheck` → `build` для фронта.

## Frontend

- [Frontend-2.0](Frontend-2.0)
- Подробнее: [Frontend-2.0/README.ru.md](Frontend-2.0/README.ru.md)

```powershell
cd Frontend-2.0
yarn
yarn dev
yarn build
yarn preview
```

Типичный локальный environment:
```env
VITE_API_BASE_URL=http://localhost:8080
VITE_API_MODE=live
```

Режимы:
- `live` — реальный бэкенд
- `mock` — локальные фикстуры

В Docker Compose фронт публикуется на `http://localhost:5173`.

## Политики логического дампа БД

### Требуемые нативные инструменты

На машине с агентом должны быть установлены:
- PostgreSQL: `pg_dump`
- MySQL: `mysqldump`

Для предсказуемого поведения на разных машинах можно задать абсолютные пути в конфиге агента:

```json
{
  "Agent": {
    "PostgreSqlDumpCommand": "C:\\Program Files\\PostgreSQL\\18\\bin\\pg_dump.exe",
    "MySqlDumpCommand": "C:\\Program Files\\MySQL\\MySQL Server 8.0\\bin\\mysqldump.exe"
  }
}
```

### Режимы аутентификации PostgreSQL

PostgreSQL-политики поддерживают:
- `credentials` — рекомендованный универсальный режим
- `integrated` — пароль не хранится в политике; `pg_dump` должен уметь зайти как OS-пользователь, под которым работает агент

Рекомендация:
- для локального Docker PostgreSQL контейнера — `credentials`
- `integrated` — только для специально сконфигурированной локальной инсталляции PostgreSQL

### Ручная проверка перед созданием политики

Credentials:
```powershell
$env:PGPASSWORD = 'your_password'
pg_dump --no-password --host 127.0.0.1 --port 5432 --username restoreme_user --format=plain --file test.sql restoreme_db
```

Integrated:
```powershell
pg_dump --no-password --host 127.0.0.1 --port 5432 --format=plain --file test.sql restoreme_db
```

Если ручная команда падает — политика тоже упадёт.

## Типичный workflow оператора

### Одобрить нового агента
1. Запустите backend и frontend.
2. Запустите worker-агент.
3. Откройте `Pending`.
4. Одобрите машину и присвойте имя агенту.
5. Агент продолжает под выданным `AgentId` и access-токеном.

### Создать политику бэкапа
1. Откройте `Policies`.
2. Выберите одобренного агента.
3. Выберите тип политики.
4. Для `Filesystem` укажите source-путь.
5. Для `PostgreSQL`/`MySQL` укажите параметры БД и режим аутентификации.
6. Задайте интервал.
7. Сохраните.

### Выполнить и проверить бэкап
1. Агент синхронизирует политики.
2. При наступлении срока — стартует backup-задачу.
3. Готовит ZIP-архив или логический дамп.
4. Запрашивает upload-ticket у бэкенда.
5. Бэкенд возвращает presigned MinIO URL.
6. Агент загружает payload напрямую в объектное хранилище.
7. Задача и артефакт появляются в панели.

## Типовые команды

### Backend
```powershell
cd Backup
dotnet build .\Backup.Server.Api\Backup.Server.Api.csproj
dotnet run --project .\Backup.Server.Api\Backup.Server.Api.csproj
dotnet test BackupSystem.slnx
```

### Agent
```powershell
cd Backup
dotnet build .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
dotnet run --project .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
```

### Frontend
```powershell
cd Frontend-2.0
yarn
yarn build
```

### Docker Compose
```powershell
cd docker-compose
docker compose up --build
docker compose down
docker compose logs -f backend
docker compose logs -f frontend-2
docker compose logs -f minio
docker compose logs -f db
```

## Troubleshooting

### Логин на фронте не доходит до бэкенда
Проверьте:
- фронт пересобран после последних изменений на login-странице
- бэкенд реально слушает ожидаемый адрес
- `VITE_API_BASE_URL` указывает на правильный URL бэкенда

### Должен быть только bootstrap-админ, но старые пользователи остались
Причина:
- seed запускается только если таблица пользователей пуста

Исправление:
- использовать чистую БД для свежего первого старта
- или удалить старых пользователей вручную через панель/БД

### Агент продолжает ходить на старый адрес
Причина:
- `ServerAddress` сохранён в `state/agent-state.json`

Исправление:
- `BackupAgent --server <correct-url>` или `--reset-state`

### Агент достучался до бэкенда, но не может загрузить в MinIO
Проверьте:
- порт MinIO доступен с машины агента
- бэкенд вернул правильный public-storage-хост
- `Storage:PublicEndpoint` сконфигурирован, если хост хранилища отличается от хоста бэкенда

### PostgreSQL логический дамп падает с `no password supplied`
Причина:
- режим `integrated` для БД, не настроенной на passwordless-доступ

Исправление:
- переключить политику на `credentials`
- для локального тестирования можно использовать `127.0.0.1` вместо `localhost`

## Дополнительная документация

- [docker-compose/README.ru.md](docker-compose/README.ru.md) — [🇬🇧 English](docker-compose/README.md)
- [Frontend-2.0/README.ru.md](Frontend-2.0/README.ru.md) — [🇬🇧 English](Frontend-2.0/README.md)
- [README.md](README.md) — английская версия этого файла
