# Quota Server — API Reference (для UI)

> Источник истины: репозиторий [`makksesh/Quota`](https://github.com/makksesh/Quota), контроллеры в `Api/Controllers/*` и DTO в `Application/**/DTOs/*`.
> Документ описывает контракты так, как они реально реализованы на сервере, и все DTO/Request, нужные клиенту.

---

## 0. Базовые соглашения

### Транспорт и формат

- Базовый URL по умолчанию: `http://192.168.3.50:5000` (настраивается в клиенте).
- Все эндпоинты — REST поверх HTTP/JSON.
- JSON-сериализация: ASP.NET Core defaults (camelCase property names) **+** `JsonStringEnumConverter` — все enum'ы передаются **строками** (`"Private"`, `"Chat"`, `"Indexed"`, `"User"` и т. д.), а не числами.
- Все даты — UTC (`DateTime` ISO-8601 с суффиксом `Z`).
- Все идентификаторы — `Guid`.
- Размер тела multipart-запроса (`/api/documents/upload`) ограничен `50_000_000` байт (~50 MB).

### Аутентификация

- Большинство эндпоинтов требуют JWT-токен в заголовке:
  ```
  Authorization: Bearer <accessToken>
  ```
- `[AllowAnonymous]`: все `POST /api/auth/{register,login,refresh}`.
- `[Authorize(Roles="Admin")]`: **весь** `/api/models/*` (доступ только администраторам).
- При истёкшем `accessToken` сервер возвращает `401`. Клиент должен:
  1. Вызвать `POST /api/auth/refresh` с `refreshToken`.
  2. Сохранить новые токены.
  3. Повторить исходный запрос с новым `accessToken`.

### Стандартные коды ошибок

| Код | Когда |
|---|---|
| `400` | Невалидное тело запроса (валидация FluentValidation). |
| `401` | Нет/протух `accessToken`, либо вход с неверными кредами. |
| `403` | Нет роли (например, не-Admin зашёл в `/api/models`) или нет прав на ресурс. |
| `404` | Сущность не найдена. |
| `422` | Невалидные семантические параметры (например, RAG-запрос). |
| `429` | Превышен лимит (sliding window: 100 запросов / 10 сек на клиента). |
| `500` | Необработанная ошибка. |

При ошибках сервер возвращает `ProblemDetails` (RFC 7807):
```json
{
  "type": "...",
  "title": "...",
  "status": 400,
  "detail": "...",
  "errors": { "Field": ["msg"] }
}
```

### Rate limit

Глобально: **100 запросов / 10 сек** на клиента, sliding-window. Превышение → `429`.

---

## 1. Auth — `/api/auth`

> Контроллер: `AuthController`, `[AllowAnonymous]` на уровне класса.

### `POST /api/auth/register` — регистрация

Создаёт пользователя и сразу выдаёт пару токенов.

- **Auth**: нет.
- **Body**: `RegisterRequest`
  ```ts
  { username: string; email: string; password: string }
  ```
- **200 OK**: `AuthTokenDto`.

### `POST /api/auth/login` — вход

- **Auth**: нет.
- **Body**: `LoginRequest`
  ```ts
  { identifier: string;  // email ИЛИ username
    password:   string }
  ```
- **200 OK**: `AuthTokenDto`.
- **401**: неверные креды.

### `POST /api/auth/refresh` — ротация токенов

Возвращает **новую пару** access/refresh; старый refresh инвалидируется.

- **Auth**: нет (refresh передаётся в теле).
- **Body**: `RefreshTokenRequest`
  ```ts
  { refreshToken: string }
  ```
- **200 OK**: `AuthTokenDto`.

### `POST /api/auth/logout` — выход

Отзывает refresh-токен. Идемпотентно — повторный вызов с уже отозванным токеном тоже вернёт `204`.

- **Auth**: `Bearer accessToken` обязателен.
- **Body**: `LogoutRequest`
  ```ts
  { refreshToken: string }
  ```
- **204 No Content**.

### `GET /api/auth/me` — профиль текущего пользователя

- **Auth**: `Bearer accessToken`.
- **200 OK**: `UserDto`.

### DTO

```csharp
// Application/Auth/DTOs/AuthTokenDto.cs
public sealed record AuthTokenDto(
    string   AccessToken,
    string   RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    DateTime RefreshTokenExpiresAtUtc,
    Guid     UserId,
    string   Username);

// Application/Auth/DTOs/UserDto.cs
public sealed record UserDto(
    Guid     Id,
    string   Username,
    string   Email,
    DateTime CreatedAtUtc);
```

```csharp
// Api/Models/Requests/Auth/*.cs
public sealed record RegisterRequest(string Username, string Email, string Password);
public sealed record LoginRequest   (string Identifier, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest  (string RefreshToken);
```

---

## 2. Chat — `/api/chat`

> Контроллер: `ChatController`, `[Authorize]` на уровне класса.
> Поддерживаются **project-based** и **global** треды. Большинство ручек универсальны.

### Threads

| Метод & путь | Назначение |
|---|---|
| `GET /api/chat/projects/{projectId}/threads` | Список тредов проекта. |
| `GET /api/chat/threads` | Глобальные треды текущего пользователя. |
| `GET /api/chat/threads/{threadId}/history` | История сообщений треда. |
| `POST /api/chat/threads` | Создать тред в проекте (body: `CreateThreadRequest`). |
| `POST /api/chat/threads/global` | Создать глобальный тред (body: `CreateGlobalThreadRequest`). |
| `PUT /api/chat/threads/{threadId}` | Переименовать тред (body: `RenameThreadRequest`). |
| `POST /api/chat/threads/{threadId}/attach` | Привязать тред к проекту (body: `AttachThreadToProjectRequest`). |
| `DELETE /api/chat/threads/{threadId}/attach` | Отвязать тред от проекта (становится глобальным). |
| `DELETE /api/chat/threads/{threadId}` | Удалить тред (`204`). |

Создание тредов возвращает `201 Created` + `ChatThreadDto`. Переименование/прикрепление — `200 OK` + `ChatThreadDto`. История — `200 OK` + `GetThreadHistoryResult`.

### Messaging — non-streaming

`POST /api/chat/threads/{threadId}/send`
`POST /api/chat/threads/{threadId}/send-global` — alias для глобального треда.

- **Body**: `SendMessageRequest { content: string }`.
- **200 OK**: `SendMessageResult { userMessage, assistantMessage }`.

### Messaging — SSE streaming

`POST /api/chat/threads/{threadId}/stream`
`POST /api/chat/threads/{threadId}/stream-global` — alias.

- **Body**: `SendMessageRequest { content: string }`.
- **Response**: `text/event-stream`.

**Формат стрима — НЕстандартный SSE.** Сервер пишет данные так:

```
 {"token":"Hello"}\n\n
 {"token":" world"}\n\n
event: done
 {}\n\n
```

Особенности, которые **обязан** учитывать парсер клиента:

1. У строк с данными **нет** префикса `data:`. Идёт пробел и сразу JSON `{"token":"..."}`.
2. Каждое событие отделено двойным `\n\n`.
3. Завершение стрима — кадр с заголовком `event: done` и пустым телом `{}`.
4. Парсер должен извлекать поле `token` из каждого JSON и склеивать его в общий ответ ассистента; на `event: done` — закрывать поток.

> Рекомендуется в клиенте поддерживать **обе** формы (с `data:` и без), чтобы быть устойчивым к будущим изменениям.

### DTO

```csharp
// Application/Chat/DTOs/ChatThreadDto.cs
public sealed record ChatThreadDto(
    Guid      Id,
    Guid?     ProjectId,        // null для global
    string    Title,
    int       MessageCount,
    DateTime? LastMessageAtUtc,
    DateTime  CreatedAtUtc)
{
    public bool IsGlobal => ProjectId is null;
}

// Application/Chat/DTOs/ChatMessageDto.cs
public sealed record ChatMessageDto(
    Guid        Id,
    Guid        ThreadId,
    int         SequenceNumber,
    MessageRole Role,
    string      Content,
    int?        TokenCount,
    DateTime    CreatedAtUtc);

// Application/Chat/Queries/GetThreadHistory/GetThreadHistoryQuery.cs
public sealed record GetThreadHistoryResult(
    ChatThreadDto                 Thread,
    IReadOnlyList<ChatMessageDto> Messages);

// Application/Chat/Commands/SendMessage/SendMessageCommand.cs
public sealed record SendMessageResult(
    ChatMessageDto UserMessage,
    ChatMessageDto AssistantMessage);
```

```csharp
// Api/Models/Requests/Chat/*.cs
public sealed record CreateThreadRequest       (Guid ProjectId, string Title);
public sealed record CreateGlobalThreadRequest (string Title);
public sealed record RenameThreadRequest       (string Title);
public sealed record AttachThreadToProjectRequest(Guid ProjectId);
public sealed record SendMessageRequest        (string Content);
```

```csharp
// Domain/Enums/MessageRole.cs
public enum MessageRole { System = 0, User = 1, Assistant = 2 }
// При сериализации передаются как "System" / "User" / "Assistant".
```

Для UI streaming-токенов удобно объявить вспомогательную модель:

```csharp
public sealed record ChatStreamToken(string Token);
```

---

## 3. Projects — `/api/projects`

> Контроллер: `ProjectsController`, `[Authorize]`.
> **Без пагинации** — всегда возвращается полный список.

| Метод & путь | Назначение |
|---|---|
| `GET /api/projects` | Список проектов текущего пользователя (`IReadOnlyList<ProjectDto>`). |
| `GET /api/projects/{projectId}` | Один проект. |
| `POST /api/projects` | Создать проект (body: `CreateProjectRequest`). `201 Created`, `ProjectDto`. |
| `DELETE /api/projects/{projectId}` | Удалить проект. `204`. |
| `PATCH /api/projects/{projectId}/settings` | Обновить настройки (body: `UpdateProjectSettingsRequest`). `204`. |
| `POST /api/projects/{projectId}/folders` | Подключить папку (body: `AddProjectFolderRequest`). `201`, `FolderDto`. |
| `PATCH /api/projects/{projectId}/folder/permission` | Изменить флаги доступа подключённой папки (body: `ChangeFolderPermissionRequest`). `204`. |
| `DELETE /api/projects/{projectId}/folder` | Отвязать директорию от проекта (один проект — одна папка, поэтому без `folderId` в пути). `204`. |

### DTO

```csharp
// Application/Projects/DTOs/ProjectDto.cs
public sealed record ProjectDto(
    Guid              Id,
    string            Name,
    string?           Description,
    ProjectAccessMode AccessMode,
    DateTime          CreatedAtUtc,
    int               FolderCount);

// Application/Projects/DTOs/FolderDto.cs
public sealed record FolderDto(
    Guid             Id,
    Guid             ProjectId,
    string           Path,
    FolderPermission Permission);
```

```csharp
// Api/Models/Requests/Projects/*.cs
public sealed record CreateProjectRequest(
    string            Name,
    string?           Description = null,
    ProjectAccessMode AccessMode  = ProjectAccessMode.Private);

public sealed record UpdateProjectSettingsRequest(
    Guid?  ChatModelEndpointId,        // null — системный дефолт
    Guid?  EmbeddingModelEndpointId,   // null — системный дефолт
    string SystemPrompt,               // 1..4000 символов
    int    MaxTokens,                  // 1..32768
    float  Temperature,                // 0.0..2.0
    int    RagTopK,                    // 1..20
    bool   UseRagContext,
    int    ContextWindowSize = 10);    // 1..50

public sealed record AddProjectFolderRequest(
    string           Path,
    FolderPermission Permission = FolderPermission.None);

public sealed record ChangeFolderPermissionRequest(
    FolderPermission Permission);
```

```csharp
// Domain/Enums/ProjectAccessMode.cs
public enum ProjectAccessMode { Private = 0, Shared = 1 }
// JSON: "Private" / "Shared".

// Domain/Enums/FolderPermission.cs  ([Flags])
[Flags] public enum FolderPermission {
    None   = 0,
    Read   = 1,
    Edit   = 2,
    Delete = 4
}
// JSON: т. к. это [Flags] + JsonStringEnumConverter, передаётся
// как строка с разделителем ", " — например "Read, Edit".
// Если в UI нужен числовой контроль — используйте чекбоксы и
// сериализуйте сами в строку флагов.
```

---

## 4. Documents — `/api/documents`

> Контроллер: `DocumentsController`, `[Authorize]`.

| Метод & путь | Назначение |
|---|---|
| `POST /api/documents/upload` | Загрузить файл (multipart). `201`, `DocumentDto`. |
| `GET /api/documents/projects/{projectId}` | Список документов проекта. |
| `GET /api/documents/{documentId}` | Один документ. |
| `DELETE /api/documents/{documentId}` | Удалить документ. `204`. |

### Upload (multipart/form-data)

Поля формы:

| Поле | Тип | Обязательное |
|---|---|---|
| `file` | binary | да |
| `projectId` | Guid (text) | да |
| `contentType` | string | нет — если пусто, берётся из HTTP-заголовка файла |

Лимит размера запроса: **50 MB**.

### DTO

```csharp
// Application/Documents/DTOs/DocumentDto.cs
public sealed record DocumentDto(
    Guid           Id,
    Guid           ProjectId,
    string         FileName,
    string         OriginalPath,
    string?        ContentType,
    long           SizeBytes,
    DocumentStatus Status,
    string?        ErrorMessage,
    DateTime?      IndexedAtUtc,
    DateTime       CreatedAtUtc,
    int            ChunkCount);
```

```csharp
// Domain/Enums/DocumentStatus.cs
public enum DocumentStatus {
    Uploaded   = 0,
    Pending    = 1,
    Processing = 2,
    Indexed    = 3,
    Failed     = 4
}
// JSON: "Uploaded" / "Pending" / "Processing" / "Indexed" / "Failed".
```

> На сервере **нет** `POST /api/documents/generate` и общего `GET /api/documents` — только пути выше.

---

## 5. Indexing — `/api/indexing`

> Контроллер: `IndexingController`, `[Authorize]`.
> **Документ-ориентированная очередь** (не папка).

| Метод & путь | Назначение |
|---|---|
| `POST /api/indexing/queue` | Поставить документ в очередь (body: `QueueIndexingRequest`). `201`, `IndexingTaskDto`. |
| `GET /api/indexing/status/{documentId}` | Текущая задача индексирования по документу. `200` + DTO **или** `204` если задачи нет. |
| `GET /api/indexing/queue` | Список задач в очереди. |
| `POST /api/indexing/requeue` | Перепоставить задачу в очередь (body: `RequeueTaskRequest`). `200` + DTO. |

### DTO

```csharp
// Application/Indexing/DTOs/IndexingTaskDto.cs
public sealed record IndexingTaskDto(
    Guid               Id,
    Guid               ProjectId,
    Guid               DocumentId,
    IndexingTaskStatus Status,
    int                Attempt,
    DateTime?          StartedAtUtc,
    DateTime?          CompletedAtUtc,
    string?            ErrorMessage,
    DateTime           CreatedAtUtc);
```

```csharp
// Api/Models/Requests/Indexing/*.cs
public sealed record QueueIndexingRequest(Guid DocumentId);
public sealed record RequeueTaskRequest  (Guid TaskId);
```

```csharp
// Domain/Enums/IndexingTaskStatus.cs
public enum IndexingTaskStatus {
    Queued    = 0,
    Running   = 1,
    Completed = 2,
    Failed    = 3
}
// JSON: "Queued" / "Running" / "Completed" / "Failed".
```

> При `GET /status/{documentId}` `204 No Content` означает "задачи ещё нет" — клиент должен трактовать это как "не проиндексирован / не поставлен в очередь", а не как ошибку.

---

## 6. Models — `/api/models` (Admin only)

> Контроллер: `ModelsController`, `[Authorize(Roles="Admin")]`.
> Обычный пользователь получит `403`.

| Метод & путь | Назначение |
|---|---|
| `GET /api/models?modelType=Chat\|Embedding` | Список endpoint-ов. `modelType` — опциональный фильтр. |
| `GET /api/models/{endpointId}` | Один endpoint. |
| `POST /api/models` | Создать endpoint (body: `CreateModelEndpointRequest`). `201`. |
| `PUT /api/models/{endpointId}` | Обновить (body: `UpdateModelEndpointRequest`). `200`. |
| `PATCH /api/models/{endpointId}/enabled` | Переключить enabled (body: `SetModelEndpointEnabledRequest`). `204`. |
| `DELETE /api/models/{endpointId}` | Удалить. `204`. |

> `ApiKey` **никогда** не возвращается в DTO — только пишется через Create/Update.

### DTO

```csharp
// Application/Models/DTOs/ModelEndpointDto.cs
public sealed record ModelEndpointDto(
    Guid      Id,
    string    DisplayName,
    string    ModelName,
    string    BaseUrl,
    ModelType ModelType,
    bool      IsEnabled,
    int       ContextWindowTokens,
    DateTime  CreatedAtUtc);
```

```csharp
// Api/Models/Requests/Models/*.cs
public sealed record CreateModelEndpointRequest(
    string    DisplayName,
    string    ModelName,
    string    BaseUrl,
    ModelType ModelType,
    int       ContextWindowTokens,
    string?   ApiKey = null);

public sealed record UpdateModelEndpointRequest(
    string  DisplayName,
    string  ModelName,
    string  BaseUrl,
    int     ContextWindowTokens,
    string? ApiKey = null);

public sealed record SetModelEndpointEnabledRequest(bool IsEnabled);
```

```csharp
// Domain/Enums/ModelType.cs
public enum ModelType { Chat = 0, Embedding = 1 }
// JSON: "Chat" / "Embedding".
```

---

## 7. Dashboard — `/api/dashboard`

> Контроллер: `DashboardController`, `[Authorize]`.
> Один запрос вместо трёх — для главного экрана Desktop-клиента.

### `GET /api/dashboard/recent`

Возвращает до 5 последних проектов, чатов и документов текущего пользователя.

- **200 OK**: `RecentDashboardResponse`.

### DTO

```csharp
// Application/Dashboard/DTOs/RecentDashboardResponse.cs
public sealed record RecentDashboardResponse(
    IReadOnlyList<RecentItemResponse> Projects,
    IReadOnlyList<RecentItemResponse> Chats,
    IReadOnlyList<RecentItemResponse> Documents);

// Application/Dashboard/DTOs/RecentItemResponse.cs
public sealed record RecentItemResponse(
    Guid     Id,
    string   Title,
    DateTime UpdatedAt);
```

> Для проектов `Title = Name`, `UpdatedAt = CreatedAtUtc`.
> Для чатов — `Title`, `UpdatedAt = LastMessageAtUtc ?? CreatedAtUtc`.
> Для документов — `Title = FileName`, `UpdatedAt = CreatedAtUtc`.

---

## 8. RAG — `/api/rag`

> Контроллер: `RagController`, `[Authorize]`.

### `POST /api/rag/search`

Семантический поиск по проиндексированным документам проекта.

- **Body**: `RagSearchRequest`.
- **200 OK**: `IReadOnlyList<RagChunkDto>`, отсортирован по `Score` убыв.
- **403**: нет доступа к проекту.
- **404**: проект не найден.
- **422**: невалидные параметры (например, `TopK` вне диапазона).

### DTO

```csharp
// Application/Rag/DTOs/RagChunkDto.cs
public sealed record RagChunkDto(
    string                              VectorId,
    string                              Content,
    float                               Score,    // 0.0..1.0
    IReadOnlyDictionary<string, string> Metadata);

// Api/Models/Requests/Rag/RagSearchRequest.cs
public sealed record RagSearchRequest(
    Guid   ProjectId,
    string Query,
    int    TopK = 5);   // 1..20
```

---

## 9. System Metrics — `/api/system`

> Контроллер: `SystemMetricsController`, `[Authorize]`.

### `GET /api/system/metrics`

Снимок текущих CPU / RAM / GPU.

- **200 OK**: `SystemMetricsResponse` (плоский, без вложенных объектов).

### DTO

```csharp
// Api/Models/SystemMetricsResponse.cs
public record SystemMetricsResponse(
    double CpuUsagePercent,
    double CpuFrequencyGHz,
    double CpuTemperatureCelsius,

    double RamUsedGb,
    double RamTotalGb,

    double GpuUsedGb,
    double GpuTotalGb,
    double GpuTemperatureCelsius);
```

---

## 10. Эндпоинты, которых **нет** на сервере

Если в старой документации/референсе встречаются эти пути — на сервере их **нет**, и в UI их использовать нельзя:

- `/api/users/*` — нет `UsersController`. Профиль доступен только через `GET /api/auth/me`.
- `/api/health/*` — нет `HealthController`.
- `/api/ide/*` — нет `IdeController`.
- `/api/attachments/*` — нет `AttachmentsController`. Все вложения — это `documents`.
- `POST /api/documents/generate` и общий `GET /api/documents` — отсутствуют.
- `GET /api/threads/recent`, любые `/api/threads/*` без префикса `chat` — не существуют. Используйте `/api/chat/threads/*`.

---

## 11. Сводка enum'ов (JSON-значения)

| Enum | Значения JSON |
|---|---|
| `MessageRole` | `"System"`, `"User"`, `"Assistant"` |
| `ProjectAccessMode` | `"Private"`, `"Shared"` |
| `FolderPermission` `[Flags]` | `"None"`, `"Read"`, `"Edit"`, `"Delete"`, или комбинация — `"Read, Edit"`, `"Read, Edit, Delete"` |
| `DocumentStatus` | `"Uploaded"`, `"Pending"`, `"Processing"`, `"Indexed"`, `"Failed"` |
| `IndexingTaskStatus` | `"Queued"`, `"Running"`, `"Completed"`, `"Failed"` |
| `ModelType` | `"Chat"`, `"Embedding"` |

---

## 12. Рекомендации по реализации клиента

1. **Один `ApiClient`** с двумя `HttpClient`-ами:
   - `"api"` — с `AuthHeaderHandler`, который автоматически добавляет `Bearer accessToken` и при `401` делает refresh и ретраит.
   - `"bare"` — без авторизации, используется в `AuthService` для `register/login/refresh` и для самой логики refresh (чтобы избежать рекурсии).
2. **`JsonSerializerOptions`**:
   ```csharp
   new JsonSerializerOptions(JsonSerializerDefaults.Web) {
       Converters = { new JsonStringEnumConverter() }
   }
   ```
   — точно совпадает с серверной конфигурацией.
3. **SSE парсер** должен поддерживать формат сервера ` {"token":"..."}\n\n` без префикса `data:` и завершение через `event: done`. Лучший контракт для клиента:
   ```csharp
   public sealed record SseFrame(string? EventName, string Data);
   IAsyncEnumerable<SseFrame> PostSseAsync(...);
   IAsyncEnumerable<string>   StreamAsync(Guid threadId, string content, CancellationToken ct);
   ```
4. **Хранение токенов**: безопасное хранилище (DPAPI на Windows / `libsecret` на Linux / Keychain на macOS). Помимо самих токенов сохранять `AccessTokenExpiresAtUtc` и `RefreshTokenExpiresAtUtc`, чтобы превентивно рефрешить.
5. **Роли**: после `login`/`me` смотреть на claims в `accessToken`. Меню "Models" показывать **только** если `role=Admin` — иначе UI получит `403` от `/api/models/*`.
6. **`/api/indexing/status/{documentId}` → 204**: трактовать как "задачи ещё нет", не как ошибку.
7. **`FolderPermission`** в UI лучше реализовать тремя чекбоксами (Read / Edit / Delete) и сериализовать как строку флагов через запятую — `JsonStringEnumConverter` принимает оба варианта.
8. **Pagination**: на сервере её нет. Не передавайте `?page=` / `?pageSize=` — параметры просто проигнорируются.
9. **Лимит**: 100 запросов / 10 сек на клиента — учитывайте в polling-сценариях (например, индексирование).

---

> Документ синхронизирован с состоянием репозитория `makksesh/Quota` на момент его генерации. При изменениях в контроллерах/DTO — обновлять этот файл первым.
