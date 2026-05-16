# RestoreMe Docker Compose

Эта папка - единая локальная точка входа для запуска полного стека RestoreMe.

> [!WARNING]
> Прочитайте этот файл перед запуском стека. Репозиторий содержит `.env` и стартовые файлы в `secrets/` для удобства, но эти значения являются публичными development defaults и должны быть заменены перед любым shared, demo или production-like развертыванием.

Содержимое:
- `docker-compose.yml` - описание полного стека
- `.env` - несекретные порты и frontend mode
- `secrets/` - локальные secret-файлы, монтируемые в контейнеры

## Сервисы

Текущий стек включает:
- `db` - PostgreSQL 18
- `minio` - object storage
- `backend` - ASP.NET Core API
- `frontend` - стабильный RestoreMe frontend, раздаваемый Apache
- `frontend-2` - флагманский прототип Frontend 2.0, раздаваемый Apache

## Первый запуск

Используйте этот порядок при развертывании стека на чистой рабочей станции.

1. Откройте [.env](.env) и проверьте, свободны ли default ports.
2. Замените стартовые secret-файлы внутри [secrets](secrets).
3. Выполните `docker compose up --build`.
4. Дождитесь, пока backend применит migrations.
5. Откройте стабильный frontend на `http://localhost:5173` или Frontend 2.0 на `http://localhost:5174`.
6. Войдите под bootstrap-учетной записью администратора.
7. Смените bootstrap-пароль администратора.
8. Создайте дополнительных пользователей, если требуется.
9. Запустите одного или нескольких агентов отдельно.

## Bootstrap-администратор

При первом запуске backend в `Development` система создает одну учетную запись администратора, только если user table пустая.

Текущие dev credentials:
- `admin / Admin123!`

> [!WARNING]
> Смените этот пароль после первого входа. Закоммиченная bootstrap-учетная запись добавлена только для первичного локального setup.

Важное поведение:
- если пользователи уже существуют в database, seed не перезаписывает их
- если нужен действительно чистый first-start state, используйте clean database volume

## Запуск и остановка

Запустить стек:
```powershell
cd docker-compose
docker compose up --build
```

Запустить в фоне:
```powershell
docker compose up -d --build
```

Остановить стек:
```powershell
docker compose down
```

## Порты по умолчанию

По умолчанию стек публикует:
- stable frontend: `http://localhost:5173`
- frontend 2.0: `http://localhost:5174`
- backend: `http://localhost:8080`
- MinIO API: `http://localhost:9000`
- MinIO Console: `http://localhost:9001`
- PostgreSQL: `localhost:5432`

Их можно изменить в `.env`.

## Secrets

Ожидаемые secret-файлы в [secrets](secrets):
- `postgres-password.txt`
- `postgres-connection.txt`
- `minio-access-key.txt`
- `minio-secret-key.txt`

> [!WARNING]
> Не используйте закоммиченные стартовые значения для deployed instance. Замените PostgreSQL password, PostgreSQL connection string, MinIO access key и MinIO secret key вместе перед тем, как exposing stack.

### Примеры значений

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

### Почему есть два PostgreSQL secret-файла

`postgres-password.txt` используется самим PostgreSQL container.

`postgres-connection.txt` используется backend-ом, потому что backend читает полный connection string из `ConnectionStrings__DefaultConnection_FILE`.

Так startup контейнера и startup backend остаются независимыми и явными.

## Как Compose передает секреты в приложение

### PostgreSQL container

Database container читает:
- `POSTGRES_PASSWORD_FILE=/run/secrets/postgres-password`

Secret-файл должен содержать только пароль.

### Backend container

Backend читает:
- `ConnectionStrings__DefaultConnection_FILE=/run/secrets/postgres-connection`
- `Storage__AccessKey_FILE=/run/secrets/minio-access-key`
- `Storage__SecretKey_FILE=/run/secrets/minio-secret-key`

Это означает, что backend не требует hardcoded database или MinIO secrets в `docker-compose.yml`.

## Важное поведение Compose

- frontend API URLs вычисляются из `API_PORT` во время сборки frontend images
- backend CORS в `Development` принимает localhost и loopback origins на любом порту
- backend автоматически применяет EF Core migrations при startup
- backend обращается к MinIO внутри сети по `minio:9000`
- backend возвращает public upload URLs на основе `Storage__PublicEndpoint` или incoming backend host
- агентам обычно нужен только backend address в простых deployments
- локальный Docker PostgreSQL лучше тестировать через `credentials` mode для logical dump policies

## Storage addressing в Compose

Compose использует два разных storage address:
- внутренний backend-to-MinIO address: `minio:9000`
- внешний/public address для agents: обычно `http://localhost:9000` в локальном сценарии

### Простой сценарий

Если agent запущен на той же машине и достигает backend на `http://localhost:8080`, backend обычно может вернуть upload URLs, которые также указывают на `http://localhost:9000`.

### Другая машина в LAN

Если agent запущен на другой машине, `localhost` для этого agent уже неверен.
Backend и MinIO нужно публиковать через реальный LAN IP или domain.

Пример:
- backend: `http://192.168.1.50:8080`
- storage: `http://192.168.1.50:9000`

В этом случае обновите:
- backend address агента
- `Storage__PublicEndpoint` в Compose, если требуется

## Настройка агента против Compose stack

Agent запускается отдельно от этого Compose stack.

Рекомендуемые локальные значения для текущего стека:
- backend URL: `http://localhost:8080/`
- enrollment token: `restoreme-agent-enrollment-dev-token`

> [!WARNING]
> Замените enrollment token в backend и agent configuration перед использованием agents в любой shared network. Default token является публичными repository data.

Важное примечание:
- закоммиченный agent appsettings уже указывает на локальный Compose backend по адресу `http://localhost:8080/`
- перед тестированием against another host укажите agent реальный backend URL, который нужно использовать

Agent state file:
- `state/agent-state.json`

Если agent продолжает использовать old server address, обновите или удалите этот state file.

## User Login и поведение сессии

Frontend login page поддерживает два режима:
- `Remember me` включен - session сохраняется в `localStorage`
- `Remember me` выключен - session хранится только для текущей browser session

Это не меняет backend security rules; меняется только frontend session persistence.

## Версии Frontend в Compose

Compose stack запускает обе UI-версии против одного backend, database и object storage:

- `frontend` на `http://localhost:5173` - стабильный diploma baseline.
- `frontend-2` на `http://localhost:5174` - прототип UI нового поколения.

Оба frontend используют один API и должны показывать одних и тех же agents, policies, jobs и artifacts после polling/refetch.

Полезный comparison flow:
1. Создайте или обновите policy в одном frontend.
2. Откройте другой frontend.
3. Убедитесь, что та же policy появилась там.
4. Дайте agent выполнить policy.
5. Убедитесь, что resulting job и artifact появились в обоих frontends.

## Полезные команды

Показать статус сервисов:
```powershell
docker compose ps
```

Показать logs:
```powershell
docker compose logs -f backend
docker compose logs -f frontend
docker compose logs -f frontend-2
docker compose logs -f minio
docker compose logs -f db
```

Пересобрать только backend:
```powershell
docker compose up -d --build backend
```

Пересобрать только frontend:
```powershell
docker compose up -d --build frontend
```

Пересобрать только Frontend 2.0:
```powershell
docker compose up -d --build frontend-2
```

Удалить containers, но сохранить named volumes:
```powershell
docker compose down
```

Удалить containers и named volumes:
```powershell
docker compose down -v
```

Используйте последнюю команду только когда намеренно хотите сбросить PostgreSQL и MinIO data.

## Тестирование Logical Database Dump с Compose

Для bundled local PostgreSQL container рекомендуемый первый тест:
- `Policy type`: `PostgreSQL logical dump`
- `Auth mode`: `credentials`
- `Host`: `127.0.0.1`
- `Port`: `5432`
- `Database`: `restoreme_db`
- `Username`: PostgreSQL user из connection string
- `Password`: PostgreSQL password из secret

Почему это рекомендуемый путь:
- compose PostgreSQL instance достигается по TCP
- passwordless local auth не является default для этого setup
- `integrated` mode предназначен для намеренно настроенной local PostgreSQL installation, а не для default compose database container

Перед созданием logical dump policy также убедитесь, что на машине агента установлен required native dump tool:
- PostgreSQL: `pg_dump`
- MySQL: `mysqldump`

При необходимости задайте absolute tool path в agent config.

## Troubleshooting

### Frontend открывается, но login не работает
Проверьте:
- backend container запущен
- frontend image был пересобран после последних login-related changes
- frontend указывает на правильный backend URL
- вы используете текущие seeded admin credentials на clean или expected database

### Frontend 2.0 недоступен на port 5174
Проверьте:
- `.env` содержит `FRONTEND_2_PORT=5174`
- `frontend-2` container есть в `docker compose ps`
- image был пересобран через `docker compose up -d --build frontend-2`
- другой local process уже не использует выбранный port

### Должен быть только один bootstrap admin, но пользователей больше
Причина:
- database уже была populated до последних seed rules

Исправление:
- используйте clean database volume для fresh first startup
- или удалите лишних пользователей через panel/database вручную

### Agent может достичь backend, но не может загрузить archives
Проверьте:
- MinIO port доступен с agent machine
- backend вернул upload URL с правильным external host
- `Storage__PublicEndpoint` корректен для вашей topology

### PostgreSQL logical dump падает без пароля
Обычно это значит, что policy использует `integrated` mode против compose PostgreSQL container. Переключите policy на `credentials` и используйте `127.0.0.1:5432`.

### Agent не может найти `pg_dump` или `mysqldump`
Установите matching native dump tool на agent machine или настройте absolute path в agent settings.

### Frontend route возвращает Not Found в Docker
Это уже должно обрабатываться frontend container rewrite rules. Если проблема все еще есть, пересоберите frontend image.

## Связанная документация

- [../README.ru.md](../README.ru.md)
- [../Frontend/README.ru.md](../Frontend/README.ru.md)
- [../Frontend-2.0/README.ru.md](../Frontend-2.0/README.ru.md)
