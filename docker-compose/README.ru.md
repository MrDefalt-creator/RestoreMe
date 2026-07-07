# RestoreMe Docker Compose

[🇬🇧 English](README.md) · 🇷🇺 Русский

Этот каталог — единственная локальная точка входа для запуска всего стека RestoreMe.

> [!WARNING]
> Прочитайте этот файл перед запуском стека. Репозиторий специально содержит `.env` и стартовые файлы в `secrets/` для удобства, но это публичные dev-дефолты — их нужно заменить до любого shared, demo или production-подобного развёртывания.

Содержимое:
- `docker-compose.yml` — определение всего стека (нейтральный baseline; без окружения)
- `docker-compose.override.yml` — **авто-подхватывается** локально; ставит `ASPNETCORE_ENVIRONMENT=Development` и открывает консоль MinIO
- `docker-compose.prod.yml` — опциональный overlay для production-подобного запуска
- `.env` — не-секретные порты и режим фронта
- `secrets/` — локальные секрет-файлы, монтируемые в контейнеры (`*.example.txt` — закоммиченные шаблоны, реальные `*.txt` git-игнорируются по умолчанию)

## Сервисы

Текущий состав стека:
- `db` — PostgreSQL 18
- `minio` — объектное хранилище
- `backend` — ASP.NET Core API
- `frontend-2` — админ-панель RestoreMe (раздаётся Apache)
- `agent-builder` — opt-in one-shot сервис, который публикует self-contained бинари агента (linux-x64 / linux-arm64 / win-x64) в shared volume, читаемый бэкендом
- `control-plane-backup` — sidecar самобэкапа: по расписанию складывает дамп PostgreSQL-метаданных и ключи DataProtection в `./backups/` (см. ниже)

## Первый запуск

Используйте этот порядок при развёртывании стека на чистой машине.

1. Откройте [.env](.env) и убедитесь, что дефолтные порты свободны.
2. Замените стартовые секрет-файлы в [secrets](secrets).
3. Выполните `docker compose up --build`.
4. Дождитесь применения миграций бэкендом.
5. Откройте админ-панель на `http://localhost:5173`.
6. Войдите под bootstrap-администратором.
7. Смените пароль bootstrap-администратора.
8. Создайте дополнительных пользователей при необходимости.
9. Запустите одного или нескольких агентов отдельно.

## Bootstrap-администратор

При первом запуске бэкенда в `Development` система создаёт ровно одного начального администратора — **только если таблица пользователей пуста**.

Текущие dev-креды:
- `admin / Admin123!`

> [!WARNING]
> Поменяйте этот пароль после первого входа. Закоммиченный bootstrap-аккаунт нужен только чтобы первая локальная установка вообще была возможна.

Важно:
- если пользователи уже есть, seed не перезаписывает их
- для действительно чистого первого старта используйте чистый volume БД

## Старт и стоп

Запуск стека (development — авто-overlay подхватывается):
```powershell
cd docker-compose
docker compose up --build
```

В фоне:
```powershell
docker compose up -d --build
```

Production-подобный запуск (без dev-overlay, с prod-overlay):
```powershell
cd docker-compose
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

Необходимое окружение (экспортировать или положить в `.env.prod`):
- `CORS_ORIGIN` — публичный origin фронта, например `https://restoreme.example.com`
- `API_PUBLIC_URL` — публичный URL бэкенда, вшивается в Vite-бандл и в CSP `connect-src` фронта

Бэкенд откажется стартовать в Production, если `Cors:AllowedOrigins` пуст или содержит только loopback-хосты — убедитесь, что `CORS_ORIGIN` выставлен до подъёма стека.

Стоп:
```powershell
docker compose down
```

## Дефолтные порты

По умолчанию стек публикует:
- frontend: `http://localhost:5173`
- backend: `http://localhost:8080`
- MinIO API: `http://localhost:9000`
- MinIO Console: `http://localhost:9001`
- PostgreSQL: `localhost:5432`

Меняются в `.env`.

## Самобэкап control plane (DR)

Sidecar `control-plane-backup` поднимается вместе со стеком. По расписанию
(`BACKUP_INTERVAL_HOURS`, по умолчанию 24 ч) он пишет в `./backups/`:

- `db-<UTC-метка>.dump` — `pg_dump --format=custom` базы метаданных
- `keys-<UTC-метка>.tar.gz` — ключи DataProtection (volume `backend_keys`)

и хранит по `BACKUP_KEEP` свежих копий каждого вида (по умолчанию 14). Обе
настройки задаются в `.env`. Контейнер становится **unhealthy**, когда
свежайший бэкап старше двух циклов.

Базу и ключи восстанавливать нужно **парой** — секреты каналов уведомлений
в базе зашифрованы именно этими ключами.

> [!WARNING]
> `./backups/` лежит на той же машине, что и защищаемые данные. Настройте
> регулярную выгрузку наружу (rsync/rclone/restic). Данные артефактов в
> MinIO сюда НЕ входят — варианты описаны в runbook.

Полная процедура восстановления и заметки о репетиции: [../docs/DR-RUNBOOK.md](../docs/DR-RUNBOOK.md) (EN).

## Секреты

Ожидаемые секрет-файлы в [secrets](secrets):
- `postgres-password.txt`
- `postgres-connection.txt`
- `minio-access-key.txt`
- `minio-secret-key.txt`

У каждого есть `*.example.txt` шаблон рядом. Текущие `.txt` поставляются с dev-дефолтами, чтобы первый `docker compose up` работал без подготовки; правило в `.gitignore` блокирует *любые новые* `.txt`, чтобы реальные продовые секреты не попали в коммит случайно.

> [!WARNING]
> Не переиспользуйте закоммиченные стартовые значения на развёрнутом инстансе. Замените пароль PostgreSQL, connection-string PostgreSQL, access-key и secret-key MinIO вместе до публикации стека.

### Пример значений

`postgres-password.txt`
```text
my_strong_postgres_password
```

`postgres-connection.txt`
```text
Host=postgres;Port=5432;Database=restoreme_db;Username=restoreme_user;Password=my_strong_postgres_password
```

`minio-access-key.txt`
```text
restoreme_minio_dev
```

`minio-secret-key.txt`
```text
restoreme_minio_dev_ChangeMe_2026!
```

### Зачем два секрет-файла для PostgreSQL

`postgres-password.txt` — для самого контейнера PostgreSQL.

`postgres-connection.txt` — для бэкенда, потому что бэкенд читает полную connection-string из `ConnectionStrings__DefaultConnection_FILE`.

Это держит старт контейнера и старт бэкенда независимыми и явными.

## Как Compose доставляет секреты в приложение

### Контейнер PostgreSQL

База читает:
- `POSTGRES_PASSWORD_FILE=/run/secrets/postgres-password`

Секрет-файл должен содержать только пароль.

### Контейнер бэкенда

Бэкенд читает:
- `ConnectionStrings__DefaultConnection_FILE=/run/secrets/postgres-connection`
- `Storage__AccessKey_FILE=/run/secrets/minio-access-key`
- `Storage__SecretKey_FILE=/run/secrets/minio-secret-key`

То есть в `docker-compose.yml` не должны быть зашиты ни секреты БД, ни секреты MinIO.

## Важное поведение Compose

- URL API фронта формируется из `API_PORT` на этапе сборки image (в prod — override через `API_PUBLIC_URL`)
- backend CORS в `Development` принимает localhost и loopback origin'ы на любом порту; в Production бэкенд отказывается стартовать без явного non-loopback `Cors:AllowedOrigins`
- CORS затрагивает только **браузерный** трафик (админ-панель). Agent → backend трафик идёт через plain HTTP-клиент, без `Origin` и preflight — поэтому агент на другой машине достучится до бэкенда независимо от CORS-allowlist'а, лишь бы пускала сеть/firewall
- все сервисы шарят сеть `restoreme-internal`, объявленную в `docker-compose.yml`; межсервисные имена — это имена сервисов (`db`, `minio`, `backend`)
- бэкенд применяет EF Core миграции автоматически при старте
- бэкенд ходит к MinIO внутри по `minio:9000`
- бэкенд возвращает публичные upload/download URLs на основе `Storage__PublicEndpoint` или входящего хоста бэкенда
- в простых развёртываниях агенту обычно нужен только адрес бэкенда
- локальный Docker PostgreSQL лучше всего тестировать через режим `credentials` для политик логических дампов
- бэкенд хранит DataProtection-ключи на именованном volume `backend_keys`, чтобы cookie-bound JWT переживали `docker compose up --build`
- контейнер бэкенда работает от non-root пользователя `app`. **Свежий** volume `backend_keys` автоматически получает правильного владельца, но volume, созданный старым (root-овым) образом, остаётся root-owned — бэкенд не сможет писать ключи при старте. Разовый фикс (или `docker volume rm docker-compose_backend_keys`, если готовы к повторному логину всех пользователей):

  ```powershell
  docker compose run --rm --user root --entrypoint chown backend -R app:app /app/keys
  ```
- `/health` подключён к healthcheck бэкенда и требует доступности и PostgreSQL, и MinIO

## Адресация хранилища в Compose

Compose использует два разных адреса хранилища:
- внутренний backend↔MinIO: `minio:9000`
- внешний/публичный для агентов: обычно `http://localhost:9000` в локальном сценарии

### Простой сценарий

Если агент работает на той же машине и ходит к бэкенду на `http://localhost:8080`, бэкенд обычно сможет вернуть upload-URL, тоже указывающие на `http://localhost:9000`.

### Другая машина в LAN

Если агент работает на другой машине, `localhost` для него уже не подходит. Опубликуйте backend и MinIO через реальный LAN-IP или домен.

Пример:
- backend: `http://192.168.1.50:8080`
- storage: `http://192.168.1.50:9000`

В этом случае обновите:
- адрес бэкенда у агента
- `Storage__PublicEndpoint` в Compose при необходимости

## Настройка агента против Compose-стека

Агент запускается отдельно от этого Compose-стека.

Рекомендуемые локальные значения для текущего стека:
- URL бэкенда: `http://localhost:8080/`
- enrollment-токен: `restoreme-agent-enrollment-dev-token`

### Сборка бинарей агента

Мастер установки генерирует команду, которая тянет **и** установщик-скрипт, **и** бинарь агента с самого бэкенда (никакой GitHub-зависимости — это self-hosted путь). Скрипты-установщики зашиты в образ бэкенда, а бинари агента производятся по требованию one-shot сервисом, чтобы образ бэкенда оставался лёгким и чтобы версии backend/agent можно было патчить независимо.

Запустите один раз после свежего `compose up` (и снова — каждый раз, когда меняется код агента):

```powershell
cd docker-compose
docker compose --profile build-agents up agent-builder
```

Эта команда публикует `linux-x64`, `linux-arm64` и `win-x64` self-contained one-file бинари в shared volume (`agent_binaries`), который бэкенд монтирует read-only в `/app/wwwroot/installers/binaries/`. Бинари становятся доступны через мастер установки сразу — рестарт бэкенда не нужен.

Если оператор пропустит этот шаг, URL мастера установки всё равно отвечает (installer-скрипт скачается), но скрипт упадёт на загрузке бинаря с подсказкой обратно к этой секции.

> [!WARNING]
> Замените enrollment-токен в конфигах бэкенда и агента до использования агентов в любой shared-сети. Дефолтный токен — это публичные данные репозитория.

### Почему удалённый агент не должен быть в CORS-allowlist'е

CORS — это браузерная фича безопасности. Браузеры отказываются доставлять cross-origin XHR-ответы странице, если `Access-Control-Allow-Origin` сервера не содержит origin страницы. **Агенты — не браузеры**: worker ходит через `HttpClient` POST/GET-ом, без `Origin`, без preflight — сервер не применяет CORS к ответу.

Поэтому:

- Добавить нового агента на `192.168.1.50`, пока CORS-allowlist бэкенда содержит только `http://localhost:5173` — **нормально**, агент подключится.
- Через LAN/Internet должны быть доступны TCP-порт бэкенда (`API_PORT`, дефолт `8080`) и MinIO-эндпоинт через `Storage__PublicEndpoint`.
- CORS важен только когда оператор открывает админ-панель с другого хоста, не указанного в allowlist'е — тогда расширяете `Cors:AllowedOrigins`.

Важно:
- закоммиченный appsettings агента уже указывает на локальный Compose-бэкенд `http://localhost:8080/`
- перед тестированием против другого хоста направьте агента на реальный URL бэкенда

State-файл агента:
- `state/agent-state.json`

Если агент продолжает использовать старый адрес сервера — обновите или удалите этот state-файл (`--reset-state` делает это автоматически).

## Логин и поведение сессии

Логин-страница поддерживает два режима:
- `Remember me` включён — кука получает явный `Expires`, профиль в `localStorage`
- `Remember me` выключен — кука session-only, профиль в `sessionStorage`

Это не меняет серверные правила безопасности, только персистентность сессии на клиенте. Сам JWT всегда живёт в HttpOnly-куке `access_token` — JS его не читает.

## Полезные команды

Статус сервисов:
```powershell
docker compose ps
```

Логи:
```powershell
docker compose logs -f backend
docker compose logs -f frontend-2
docker compose logs -f minio
docker compose logs -f db
```

Пересобрать только бэкенд:
```powershell
docker compose up -d --build backend
```

Пересобрать только фронт:
```powershell
docker compose up -d --build frontend-2
```

Снести контейнеры, оставив именованные volumes:
```powershell
docker compose down
```

Снести контейнеры **и** именованные volumes:
```powershell
docker compose down -v
```

Последнюю команду используйте только когда намеренно хотите сбросить данные PostgreSQL и MinIO.

## Тестирование логического дампа БД с Compose

Для встроенного локального PostgreSQL-контейнера рекомендуемая первая проверка:
- `Policy type`: `PostgreSQL logical dump`
- `Auth mode`: `credentials`
- `Host`: `127.0.0.1`
- `Port`: `5432`
- `Database`: `restoreme_db`
- `Username`: PostgreSQL-пользователь из вашей connection-string
- `Password`: пароль PostgreSQL из вашего секрета

Почему именно так:
- Compose PostgreSQL-инстанс доступен через TCP
- passwordless local auth — не дефолт этой конфигурации
- режим `integrated` рассчитан на специально настроенную локальную инсталляцию PostgreSQL, не на дефолтный compose-контейнер

Перед созданием логической политики убедитесь, что на машине агента установлен нужный нативный инструмент:
- PostgreSQL: `pg_dump`
- MySQL: `mysqldump`

При необходимости задайте абсолютный путь к инструменту в конфиге агента.

## Troubleshooting

### Фронт открывается, но логин не работает
Проверьте:
- контейнер бэкенда поднят
- image фронта был пересобран после последних изменений логина
- фронт указывает на правильный URL бэкенда
- вы используете актуальные seed-креды на чистой или ожидаемой БД

### Фронт недоступен на порту 5173
Проверьте:
- в `.env` стоит `FRONTEND_2_PORT=5173`
- контейнер `frontend-2` есть в `docker compose ps`
- image был пересобран через `docker compose up -d --build frontend-2`
- другой локальный процесс не занял выбранный порт

### Должен быть только один bootstrap-админ, но пользователей больше
Причина:
- БД была заселена до текущих правил seed'а

Исправление:
- использовать чистый volume БД для свежего первого старта
- или удалить лишних пользователей через панель/БД руками

### Агент достучался до бэкенда, но не может загрузить архивы
Проверьте:
- порт MinIO доступен с машины агента
- бэкенд вернул upload-URL с правильным внешним хостом
- `Storage__PublicEndpoint` корректен для вашей топологии

### PostgreSQL логический дамп падает без пароля
Обычно это означает, что политика использует `integrated` против compose PostgreSQL контейнера. Переключите на `credentials` и используйте `127.0.0.1:5432`.

### Агент не находит `pg_dump` или `mysqldump`
Установите нужный нативный инструмент на машине агента или укажите абсолютный путь в настройках агента.

### Маршрут фронта возвращает Not Found в Docker
Должно уже обрабатываться rewrite-правилами контейнера фронта. Если всё равно видите — пересоберите image фронта.

## Связанные документы

- [../README.ru.md](../README.ru.md) — [🇬🇧 English](../README.md)
- [../Frontend-2.0/README.ru.md](../Frontend-2.0/README.ru.md) — [🇬🇧 English](../Frontend-2.0/README.md)
- [README.md](README.md) — английская версия этого файла
