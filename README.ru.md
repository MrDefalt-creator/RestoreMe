# RestoreMe

RestoreMe - система управления резервным копированием, состоящая из следующих основных частей:
- `Backup.Server.Api` - backend API на ASP.NET Core
- `Backup.Agent.Worker` - агент, который регистрируется, синхронизирует политики, отправляет heartbeat и выполняет резервное копирование
- `Frontend` - стабильная React-панель администратора для операторов и администраторов
- `Frontend-2.0` - флагманский прототип интерфейса нового поколения, построенный на тех же backend-контрактах

Система использует:
- PostgreSQL для реляционных данных
- MinIO для объектного хранилища
- Docker Compose для локального запуска полного стека

> [!WARNING]
> Перед запуском стека прочитайте этот README и [docker-compose/README.ru.md](docker-compose/README.ru.md). Репозиторий намеренно содержит Docker Compose `.env` и стартовые файлы секретов, чтобы ускорить первый запуск, но все стандартные учетные данные и токены необходимо заменить перед публичным, общим или production-like развертыванием.

## Структура репозитория

```text
RestorMe/
  Backup/
    Backup.Server.Api/
    Backup.Server.Application/
    Backup.Server.Domain/
    Backup.Server.Infrastructure/
    Backup.Agent.Worker/
    Backup.Shared.Contracts/
  Frontend/
  Frontend-2.0/
  docker-compose/
    docker-compose.yml
    .env
    secrets/
  README.md
```

## Основные возможности

### Backend
- слоистая архитектура: API / Application / Domain / Infrastructure / Shared.Contracts
- flow регистрации pending agent и подтверждения агента
- обработка heartbeat
- CRUD политик для файлового резервного копирования и логических дампов баз данных
- жизненный цикл backup jobs: start, fail, complete
- хранение artifacts в MinIO и скачивание artifacts через backend
- автоматическое применение EF Core migrations при запуске
- поддержка файловых секретов через `*_FILE`
- JWT-аутентификация пользователей панели
- ролевая модель: `admin`, `operator`, `viewer`
- bootstrap-защита агентов через enrollment token и выделенные agent access tokens

### Agent
- может получить `AgentId` после pending-регистрации или использовать сохраненный идентификатор
- хранит локальное состояние в `state/agent-state.json`
- хранит адрес backend-сервера и agent access token локально
- отправляет heartbeat и периодически синхронизирует политики
- выполняет filesystem backup policies
- выполняет логические политики дампа PostgreSQL и MySQL
- загружает подготовленные payload напрямую в object storage через upload tickets, возвращенные backend

### Frontend v1
- безопасная страница входа с `Remember me`
- dashboard
- страница agents
- страница pending agents approval
- страница policies
- страница jobs
- страница artifacts
- страница account для самостоятельной смены пароля
- страница users для администрирования доступа
- автоматический polling в live mode

### Frontend 2.0
- флагманский UI-прототип в Apple-like стиле для того же RestoreMe backend
- dark и light themes
- улучшенный dashboard с activity trend, protection mix и attention items
- страница agents с filters, policy coverage и details dialog
- approve/reject flows для pending agents
- policies, jobs и backups/artifacts views, согласованные с текущими backend DTOs
- автоматический polling и query invalidation, настроенные для live operational use

## Требования

### Локальная разработка без Docker
- .NET SDK 10
- Node.js 22+
- Yarn 1.x
- PostgreSQL
- MinIO

### Локальный полный стек через Docker
- Docker Desktop
- Docker Compose

## Рекомендуемые режимы запуска

### Вариант 1. Полный стек через Docker Compose

Это самый простой и рекомендуемый путь локального запуска.

> [!WARNING]
> Перед запуском Compose вне приватной локальной тестовой среды замените все закоммиченные значения в `docker-compose/secrets`, смените bootstrap-пароль администратора после первого входа и замените JWT/enrollment tokens в конфигурации backend и agent.

```powershell
cd docker-compose
docker compose up --build
```

Адреса по умолчанию:
- frontend v1: `http://localhost:5173`
- frontend 2.0: `http://localhost:5174`
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
cd Frontend
yarn
yarn dev
```

Frontend 2.0:
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

## Чеклист первого развертывания

Используйте эту последовательность для чистого локального развертывания или первой настройки рабочей станции.

1. Прочитайте [docker-compose/README.ru.md](docker-compose/README.ru.md).
2. Замените стартовые значения в [docker-compose/secrets](docker-compose/secrets).
3. Проверьте [docker-compose/.env](docker-compose/.env), если стандартные порты уже заняты.
4. Запустите стек командой `docker compose up --build`.
5. Дождитесь, пока backend применит migrations.
6. Откройте `http://localhost:5173` для стабильного frontend или `http://localhost:5174` для Frontend 2.0.
7. Войдите под bootstrap-учетной записью администратора.
8. Смените bootstrap-пароль администратора.
9. Создайте дополнительных пользователей при необходимости.
10. Запустите одного или нескольких агентов отдельно.
11. Подтвердите pending agents в панели.
12. Создайте policies и проверьте jobs/artifacts.

## Секреты и чувствительная конфигурация

### Директория Compose secrets

Локальный Docker-запуск ожидает эти файлы в [docker-compose/secrets](docker-compose/secrets):
- `postgres-password.txt`
- `postgres-connection.txt`
- `minio-access-key.txt`
- `minio-secret-key.txt`

> [!WARNING]
> Эти файлы закоммичены только как локальные стартовые значения. Относитесь к ним как к шаблонам с рабочими defaults: замените их перед публикацией развернутого экземпляра в любую общую сеть, demo server или production-like окружение.

Примеры:

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

### Как backend читает секреты

Backend поддерживает как обычные значения конфигурации, так и файловые секреты.

Примеры:
- `ConnectionStrings:DefaultConnection`
- `ConnectionStrings:DefaultConnection_FILE`
- `Storage:AccessKey`
- `Storage:AccessKey_FILE`
- `Storage:SecretKey`
- `Storage:SecretKey_FILE`

Значение:
- обычные значения удобны для быстрой локальной разработки
- `*_FILE` - предпочтительный способ, когда Docker монтирует secret-файлы в контейнер

### Важные секции backend-конфигурации

Основной файл конфигурации backend:
- [Backup/Backup.Server.Api/appsettings.json](Backup/Backup.Server.Api/appsettings.json)

Важные секции:
- `ConnectionStrings:DefaultConnection`
- `ConnectionStrings:DefaultConnection_FILE`
- `Storage:Endpoint`
- `Storage:PublicEndpoint`
- `Storage:AccessKey`
- `Storage:AccessKey_FILE`
- `Storage:SecretKey`
- `Storage:SecretKey_FILE`
- `Storage:BucketName`
- `Storage:UseSsl`
- `Storage:UploadUrlExpirySeconds`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:SigningKey`
- `AgentEnrollment:EnrollmentToken`

### Production-minded примечание

Для локального развертывания файловые Docker secrets - хорошее улучшение по сравнению с plain YAML values.
Для настоящего production все равно предпочтительнее dedicated secret manager или platform secret store.

## Аутентификация и роли

### Bootstrap-администратор

В `Development` система создает ровно одного начального администратора, если таблица пользователей пустая.

Текущие dev credentials:
- `admin / Admin123!`

> [!WARNING]
> Смените bootstrap-пароль администратора сразу после первого входа. Закоммиченное значение - публичные development bootstrap data, а не секрет.

Источник:
- [Backup/Backup.Server.Api/appsettings.Development.json](Backup/Backup.Server.Api/appsettings.Development.json)

Важное поведение:
- seed запускается только когда в базе еще нет пользователей
- если пользователи уже существуют, seed их не перезаписывает
- для уже заполненной базы управляйте пользователями через панель или напрямую через базу

### Роли панели

- `viewer` - read-only доступ к workspace
- `operator` - может работать с agents, policies, jobs и artifacts
- `admin` - полный доступ, включая user management

### Правила управления пользователями

Реализованные safeguards:
- в системе должен оставаться хотя бы один активный администратор
- текущую учетную запись нельзя удалить из admin table
- текущую учетную запись нельзя отключить из admin table
- текущей учетной записи нельзя сменить роль из admin table
- каждый авторизованный пользователь может изменить собственный пароль на странице `Account`
- только администраторы могут создавать пользователей, менять чужие пароли, отключать пользователей и удалять пользователей

### Поведение Remember me

Страница login позволяет выбрать persistence сессии:
- если `Remember me` включен, сессия хранится в `localStorage`
- если `Remember me` выключен, сессия живет только в `sessionStorage`
- непостоянная сессия исчезает при завершении browser session

## Модель безопасности агента

### Bootstrap и обычная работа

Agent security сейчас работает в две фазы:

1. Агент использует `Api:EnrollmentToken` для первичной регистрации и восстановления доступа.
2. После approval backend выдает dedicated agent access token.
3. Агент хранит этот token в local state и использует его для:
   - heartbeat
   - policy sync
   - backup job start/finish/fail
   - artifact registration
   - upload ticket requests

### Конфигурация агента

Основной файл конфигурации агента:
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
> Замените `AgentEnrollment:EnrollmentToken` на backend и `Api:EnrollmentToken` на каждом agent перед использованием системы вне локальной разработки.

Важное примечание:
- закоммиченные agent defaults указывают на локальный Docker Compose backend по адресу `http://localhost:8080/`
- для другой машины или сервера измените `Api:BaseUrl` на реальный backend address перед запуском агента

### Локальное состояние агента

Агент хранит local state здесь:
- `state/agent-state.json`

В state могут быть:
- `AgentId`
- `ServerAddress`
- `AccessToken`

Поведение:
- если сохраненный `ServerAddress` существует, он имеет приоритет над config `Api:BaseUrl`
- если у агента уже есть `AgentId`, но нет access token, он может восстановить новый token через enrollment flow
- если агент после изменения config все еще подключается к старому backend, обновите или удалите `state/agent-state.json`

## Модель адресации storage

Важны два storage address:
- `Storage:Endpoint` - внутренний MinIO address, используемый backend
- `Storage:PublicEndpoint` - внешний address, используемый в upload URLs, возвращаемых agent

### Простое развертывание

В обычном случае agent нужен только backend address.

Пример:
- backend: `http://my-server:8080`
- storage: `http://my-server:9000`

В этом случае backend может автоматически построить корректные upload URLs для agent.

### Когда нужно явно задавать `Storage:PublicEndpoint`

Задайте его явно, когда:
- backend и storage опубликованы на разных доменах
- storage опубликован через другой reverse proxy
- agent достигает backend через один address, но storage должен быть доступен через другой address

## База данных и migrations

Migrations находятся здесь:
- [Backup/Backup.Server.Infrastructure/Migrations](Backup/Backup.Server.Infrastructure/Migrations)

Поведение:
- backend применяет migrations автоматически при startup
- пустая база инициализируется автоматически
- актуальная база продолжает startup нормально

Создать новую migration вручную:
```powershell
cd Backup
dotnet ef migrations add MigrationName --project .\Backup.Server.Infrastructure\Backup.Server.Infrastructure.csproj --startup-project .\Backup.Server.Api\Backup.Server.Api.csproj --output-dir Migrations
```

## Настройка Frontend

Папка стабильного frontend:
- [Frontend](Frontend)

Полезные команды:
```powershell
cd Frontend
yarn
yarn dev
yarn build
yarn preview
```

Типичная локальная frontend environment:
```env
VITE_API_BASE_URL=http://localhost:8080
VITE_API_MODE=live
```

Режимы:
- `live` - использовать настоящий backend API
- `mock` - использовать local fixtures для offline/demo работы

## Настройка Frontend 2.0

Папка Frontend 2.0:
- [Frontend-2.0](Frontend-2.0)

Полезные команды:
```powershell
cd Frontend-2.0
yarn
yarn dev
yarn build
yarn preview
```

Типичная локальная environment:
```env
VITE_API_BASE_URL=http://localhost:8080
VITE_API_MODE=live
```

Примечания:
- Frontend 2.0 - флагманский UI-прототип, а не основной baseline диплома.
- Он использует те же backend и database, что и оригинальный frontend.
- Данные, созданные в одном frontend, должны быть видны в другом после refetch/polling.
- В Docker Compose он опубликован на `http://localhost:5174`.

## Logical Database Dump Policies

### Требуемые нативные инструменты

На машине агента должны быть установлены native dump tools:
- PostgreSQL: `pg_dump`
- MySQL: `mysqldump`

Для предсказуемого поведения на разных машинах можно указать absolute tool paths в agent config:

```json
{
  "Agent": {
    "PostgreSqlDumpCommand": "C:\\Program Files\\PostgreSQL\\18\\bin\\pg_dump.exe",
    "MySqlDumpCommand": "C:\\Program Files\\MySQL\\MySQL Server 8.0\\bin\\mysqldump.exe"
  }
}
```

### PostgreSQL auth modes

PostgreSQL policies поддерживают:
- `credentials` - рекомендуемый универсальный режим
- `integrated` - пароль не хранится в policy; `pg_dump` уже должен иметь доступ к базе как OS user, под которым запущен агент

Рекомендованное правило:
- для локального Docker Compose PostgreSQL container используйте `credentials`
- используйте `integrated` только для намеренно настроенной локальной PostgreSQL installation

### Ручная проверка перед созданием policy

Пример credentials mode:
```powershell
$env:PGPASSWORD = 'your_password'
pg_dump --no-password --host 127.0.0.1 --port 5432 --username restoreme_user --format=plain --file test.sql restoreme_db
```

Пример integrated mode:
```powershell
pg_dump --no-password --host 127.0.0.1 --port 5432 --format=plain --file test.sql restoreme_db
```

Если ручная команда падает, RestoreMe policy тоже упадет.

## Типовой workflow оператора

### Подтвердить нового агента
1. Запустите backend и frontend.
2. Запустите worker agent.
3. Откройте `Pending`.
4. Подтвердите машину и назначьте agent name.
5. Agent продолжит работу под назначенным `AgentId` и access token.

### Создать backup policy
1. Откройте `Policies`.
2. Выберите approved agent.
3. Выберите policy type.
4. Для `Filesystem` введите source path.
5. Для `PostgreSQL` или `MySQL` введите database settings и auth mode.
6. Задайте interval.
7. Сохраните policy.

### Выполнить и проверить backup
1. Agent синхронизирует policies.
2. Когда policy due, agent запускает backup job.
3. Agent готовит ZIP archive или logical dump file.
4. Agent запрашивает upload ticket у backend.
5. Backend возвращает presigned MinIO upload URL.
6. Agent загружает payload напрямую в object storage.
7. Job и artifact становятся видимыми в panel.

## Частые команды

### Backend
```powershell
cd Backup
dotnet build .\Backup.Server.Api\Backup.Server.Api.csproj
dotnet run --project .\Backup.Server.Api\Backup.Server.Api.csproj
```

### Agent
```powershell
cd Backup
dotnet build .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
dotnet run --project .\Backup.Agent.Worker\Backup.Agent.Worker.csproj
```

### Frontend
```powershell
cd Frontend
yarn
yarn build
```

### Frontend 2.0
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
docker compose logs -f frontend
docker compose logs -f frontend-2
docker compose logs -f minio
docker compose logs -f db
```

## Troubleshooting

### Frontend login не достигает backend
Проверьте:
- frontend был пересобран после последних изменений login page
- backend действительно запущен на ожидаемом address
- `VITE_API_BASE_URL` указывает на правильный backend URL

### Должен существовать только bootstrap admin, но старые пользователи все еще есть
Причина:
- user seeding запускается только когда user table пустая

Исправление:
- используйте чистую database для fresh first startup
- или удалите старых пользователей вручную через panel/database, если хотите вернуться к single-admin state

### Agent продолжает подключаться к старому backend address
Причина:
- `ServerAddress` уже сохранен в `state/agent-state.json`

Исправление:
- обновите `ServerAddress` вручную
- или удалите state file и перезапустите agent

### Agent может достучаться до backend, но не может загрузить в MinIO
Проверьте:
- MinIO port доступен с agent machine
- backend вернул correct public storage host
- `Storage:PublicEndpoint` настроен, если storage host отличается от backend host

### PostgreSQL logical dump падает с `no password supplied`
Причина:
- `integrated` mode используется против database, которая не настроена для passwordless access

Исправление:
- переключите policy на `credentials`
- при необходимости используйте `127.0.0.1` вместо `localhost` для локального теста

## Дополнительная документация

- [docker-compose/README.ru.md](docker-compose/README.ru.md)
- [Frontend/README.ru.md](Frontend/README.ru.md)
- [Frontend-2.0/README.ru.md](Frontend-2.0/README.ru.md)
