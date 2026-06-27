# Analytics module

Этот модуль собирает лёгкую аналитику использования страницы переводчика.

Модуль не сохраняет тексты переводов, содержимое загруженных файлов, имена файлов и raw IP-адреса пользователей.

## Structure

- `Models/Analytics`   - Cosmos DB document models.
- `Analytics/Events`   - внутренние events для analytics queue.
- `Analytics/Queue`    - in-memory очередь для background обработки.
- `Analytics/Background` - фоновый обработчик событий.
- `Analytics/Handlers` - логика обработки событий аналитики.
- `Analytics/Repositories` - работа с документами аналитики в Cosmos DB.
- `Analytics/Tracking` - API, который вызывают контроллеры после успешных действий пользователя.
- `Analytics/GeoIp`    - определение страны, региона и города по IP.
- `Analytics/UserAgent`- browser, OS, and device parsing.
- `Analytics/Security` - IP hashing.
- `Services/Cosmos`    - shared Cosmos DB container provider.

## Flow

Controller
-> AnalyticsTracker
-> AnalyticsQueue
-> AnalyticsBackgroundService
-> AnalyticsEventHandler
-> CosmosVisitAnalyticsRepository
-> Cosmos DB

GeoIP lookup выполняется в фоновом обработчике и не блокирует пользовательский запрос.