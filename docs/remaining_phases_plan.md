# ContourAI — оставшиеся фазы реализации

Документ описывает подробный план следующих фаз после завершения базового контура авторизации, shell-экрана и подключения dashboard к реальному API.[cite:50][cite:52][cite:53][cite:75][cite:76]

## Текущее состояние

На текущем этапе в проекте уже есть рабочая основа Avalonia 12.x приложения: login, register, общий shell, dashboard и in-memory настройка IP сервера без порта через `ConnectionSettingsStore`.[cite:48][cite:50][cite:53] Также устранены ключевые блокеры компиляции и XAML-типизации: DTO dashboard вынесены отдельно, для XAML добавлены typed bindings через `x:DataType`, а пустые состояния dashboard больше не зависят от `ObjectConverters.IsZero`.[cite:61][cite:62][cite:69][cite:70][cite:71][cite:72][cite:73][cite:75][cite:76]

## Фазовая карта

| Фаза | Цель | Основной результат |
|---|---|---|
| Фаза 3 | Стабилизация auth/session | Устойчивый lifecycle токенов, logout, refresh, startup-check |
| Фаза 4 | Проекты | Список проектов, создание проекта, открытие проекта, recent projects через реальные данные |
| Фаза 5 | Global Chat | Экран глобального чата, список сообщений, отправка запросов, streaming/response pipeline |
| Фаза 6 | Документы | Список документов проекта, загрузка, удаление, статус индексации, карточки документов |
| Фаза 7 | Индексация | Мониторинг indexing jobs, просмотр статусов, retries, ошибки индексации |
| Фаза 8 | RAG Search | Экран поиска по знаниям, фильтры, выдача результатов, переход к документам |
| Фаза 9 | System Monitor | Базовый мониторинг backend-состояния, health/status, диагностический UI |
| Фаза 10 | Навигация и состояние | Единая навигация shell, selected project, shared stores, route-like view switching |
| Фаза 11 | Обработка ошибок | Нормализованные API errors, retry UI, offline/timeout handling |
| Фаза 12 | Полировка UX | Empty/loading/error states, skeleton UI, адаптация layout, визуальная консистентность |
| Фаза 13 | Подготовка к диплому | Demo flow, тестовые сценарии, документация, финальная стабилизация |

## Фаза 3 — Session и auth lifecycle

Цель фазы — превратить текущий login/register в полноценный пользовательский сеанс, который корректно переживает старт приложения, logout и истечение access token.[cite:60][cite:77][cite:83]

### Что нужно реализовать

1. `AuthSessionStore` или расширение текущего state-слоя, где будут храниться:
   - `AccessToken`,
   - `RefreshToken`,
   - `CurrentUserId`,
   - `CurrentUsername`,
   - `IsAuthenticated`.
2. Логику `TryRestoreSessionOnStartup`, если позже появится постоянное хранение токенов.
3. `RefreshAsync`-сценарий через уже существующий auth API-контур.[cite:60]
4. `LogoutAsync` с вызовом реального backend logout.[cite:60]
5. Централизованную очистку session state при 401/403.

### Архитектура фазы

```text
DevAssistant
└── ContourAI
    ├── Features
    │   └── Auth
    │       ├── LoginView.axaml
    │       ├── RegisterView.axaml
    │       ├── LoginViewModel.cs
    │       └── RegisterViewModel.cs
    ├── Shared
    │   ├── Api
    │   │   └── AuthService.cs
    │   └── State
    │       ├── ConnectionSettingsStore.cs
    │       └── AuthSessionStore.cs
    └── App.axaml.cs
```

### Практический результат

После этой фазы приложение будет не просто логиниться, а иметь нормальную модель пользовательской сессии, которая нужна почти всем следующим экранам.[cite:53][cite:60]

## Фаза 4 — Projects

Серверный API дальше логично раскрывать через проекты, потому что проект станет центральной сущностью для документов, индексации и поиска.[file:2]

### Что должно появиться

- экран списка проектов;
- карточки проектов;
- создание нового проекта;
- открытие проекта;
- хранение `SelectedProjectId` и `SelectedProjectName` в shared state;
- обновление recent-проектов на dashboard после создания или изменения проекта.[cite:52][cite:75]

### Рекомендуемая структура

```text
DevAssistant
└── ContourAI
    ├── Entities
    │   └── Projects
    │       ├── ProjectDto.cs
    │       ├── CreateProjectRequest.cs
    │       └── ProjectSummaryDto.cs
    ├── Features
    │   └── Projects
    │       ├── ProjectsView.axaml
    │       ├── ProjectsView.axaml.cs
    │       ├── ProjectsViewModel.cs
    │       ├── CreateProjectDialog.axaml
    │       └── CreateProjectDialogViewModel.cs
    └── Shared
        ├── Api
        │   └── ProjectsService.cs
        └── State
            └── ProjectContextStore.cs
```

### UI-логика

Проекты лучше строить как master-detail сценарий: слева список, справа детали или summary по выбранному проекту. Это упростит переход к документам и indexing без повторного выбора project context.

## Фаза 5 — Global Chat

Эта фаза нужна для демонстрации основной ценности продукта: общения с системой и работы с AI-потоком поверх серверного API.[file:2]

### Что войдет в фазу

- список chat threads;
- открытие конкретного чата;
- отправка сообщения;
- получение ответа модели;
- обработка статусов ожидания, ошибок и отмены;
- отображение сообщений пользователя и ассистента.

### Важно по архитектуре

Chat не должен жить только во ViewModel. Нужны:
- `ChatService`;
- DTO сообщений;
- отдельный `ChatStateStore` или `ChatConversationStore`;
- возможность переключать активный thread.

### Рекомендуемая структура

```text
DevAssistant
└── ContourAI
    ├── Entities
    │   └── Chat
    │       ├── ChatThreadDto.cs
    │       ├── ChatMessageDto.cs
    │       └── SendMessageRequest.cs
    ├── Features
    │   └── Chat
    │       ├── ChatView.axaml
    │       ├── ChatView.axaml.cs
    │       ├── ChatViewModel.cs
    │       └── MessageItemViewModel.cs
    └── Shared
        ├── Api
        │   └── ChatService.cs
        └── State
            └── ChatStore.cs
```

### UX-цели

Нужно сразу предусмотреть:
- состояние отправки сообщения;
- длинные ответы;
- автоскролл;
- повтор отправки при сетевой ошибке;
- пустой initial state с подсказкой, что можно спросить.

## Фаза 6 — Documents

Эта фаза связывает пользовательские проекты и знаниевую базу.[file:2] Здесь приложение начинает показывать реальную работу с файлами и подготовкой данных для RAG.

### Что нужно реализовать

- список документов в проекте;
- загрузку документа;
- удаление документа;
- отображение типа, имени, даты обновления и статуса;
- фильтрацию по статусу;
- обновление dashboard и indexing views после операций с документами.

### Рекомендуемая структура

```text
DevAssistant
└── ContourAI
    ├── Entities
    │   └── Documents
    │       ├── DocumentDto.cs
    │       ├── UploadDocumentResponse.cs
    │       └── DocumentStatusDto.cs
    ├── Features
    │   └── Documents
    │       ├── DocumentsView.axaml
    │       ├── DocumentsView.axaml.cs
    │       ├── DocumentsViewModel.cs
    │       └── DocumentCardViewModel.cs
    └── Shared
        └── Api
            └── DocumentsService.cs
```

### Ключевые детали реализации

Для Avalonia важно заранее решить, чем будет загрузка файла: через `OpenFilePickerAsync` в desktop shell или через абстракцию выбора файла. Лучше сразу инкапсулировать это в небольшой сервис выбора файлов, чтобы ViewModel не зависела от UI-специфики напрямую.

## Фаза 7 — Indexing

Индексация — одна из самых важных демонстрационных фаз, потому что она показывает переход от загруженного файла к знаниям, доступным в поиске и chat/RAG.[file:2]

### Что должно быть в UI

- список indexing jobs;
- статусы: queued, processing, completed, failed;
- прогресс, если сервер его отдает;
- привязка job к документу и проекту;
- retry/cancel действия, если сервер их поддерживает;
- карточка ошибки для failed jobs.

### Рекомендуемая структура

```text
DevAssistant
└── ContourAI
    ├── Entities
    │   └── Indexing
    │       ├── IndexJobDto.cs
    │       ├── IndexJobStatusDto.cs
    │       └── RetryIndexRequest.cs
    ├── Features
    │   └── Indexing
    │       ├── IndexingView.axaml
    │       ├── IndexingView.axaml.cs
    │       ├── IndexingViewModel.cs
    │       └── IndexJobItemViewModel.cs
    └── Shared
        └── Api
            └── IndexingService.cs
```

### UX-цель

Пользователь должен за 2–3 секунды понимать, что происходит с документом: он еще в очереди, уже обработан, упал с ошибкой, или готов к поиску. Это критично для дипломной демонстрации.

## Фаза 8 — RAG Search

Это функциональная фаза, где система показывает смысл indexing pipeline: пользователь задает запрос и получает релевантные фрагменты или результаты по знаниям.[file:2]

### Что должно быть реализовано

- поле запроса;
- запуск поиска;
- отображение списка результатов;
- источник результата: проект, документ, chunk, score;
- переход к документу или связанной сущности;
- фильтры по проекту или типу данных.

### Рекомендуемая структура

```text
DevAssistant
└── ContourAI
    ├── Entities
    │   └── Search
    │       ├── SearchResultDto.cs
    │       ├── SearchRequest.cs
    │       └── SearchFacetDto.cs
    ├── Features
    │   └── Search
    │       ├── SearchView.axaml
    │       ├── SearchView.axaml.cs
    │       ├── SearchViewModel.cs
    │       └── SearchResultItemViewModel.cs
    └── Shared
        └── Api
            └── SearchService.cs
```

### UX-логика

Результаты поиска не должны быть просто списком строк. Нужны:
- title,
- snippet,
- score/relevance,
- document name,
- updated at,
- действие “открыть”.

Это сделает экран убедительным и понятным для защиты.

## Фаза 9 — System Monitor

Этот экран нужен не только для пользы, но и для дипломной демонстрации технической зрелости системы. Он показывает, что клиент умеет не только работать с бизнес-данными, но и визуализировать техническое состояние backend.[file:2]

### Что туда можно включить

- статус сервера;
- доступность API;
- ping/latency;
- количество активных jobs;
- краткую health-информацию;
- diagnostic badges для сервисов.

### Рекомендуемая структура

```text
DevAssistant
└── ContourAI
    ├── Entities
    │   └── Monitor
    │       ├── HealthStatusDto.cs
    │       └── SystemMetricsDto.cs
    ├── Features
    │   └── Monitor
    │       ├── MonitorView.axaml
    │       ├── MonitorView.axaml.cs
    │       └── MonitorViewModel.cs
    └── Shared
        └── Api
            └── MonitorService.cs
```

### Практический смысл

Этот экран особенно полезен, если login/dashboard работают, а deeper-функции зависят от состояния backend. Тогда пользователь сразу видит, что проблема в сервере, а не в клиенте.

## Фаза 10 — Навигация и shared state

С ростом числа экранов текущее переключение ViewModel нужно перевести в более системную навигацию.[cite:54]

### Что стоит сделать

- `NavigationStore`;
- enum или route keys для экранов;
- selected section в sidebar;
- selected project context;
- события смены контекста;
- возможность централизованного refresh активной страницы.

### Рекомендуемая структура

```text
DevAssistant
└── ContourAI
    ├── Shared
    │   └── State
    │       ├── NavigationStore.cs
    │       ├── ProjectContextStore.cs
    │       └── AuthSessionStore.cs
    └── Features
        └── Shell
            ├── AuthenticatedShellViewModel.cs
            └── ShellNavigationViewModel.cs
```

### Почему это нужно

Без этой фазы новые экраны начнут напрямую знать друг о друге, и проект быстро станет трудно поддерживать. Shared stores дадут нормальную масштабируемость для оставшихся разделов.

## Фаза 11 — Ошибки, retry и сетевые состояния

Когда появится больше реальных API-вызовов, стандартных `try/catch` уже будет недостаточно.[cite:52][cite:60][cite:75]

### Что нужно добавить

- общий `ApiExceptionMapper`;
- понятные пользовательские сообщения для 400/401/403/404/500;
- retry-кнопки;
- timeout handling;
- пустое состояние при отсутствии данных;
- явное состояние offline/сервер недоступен.

### Результат

Эта фаза сильно улучшит ощущение качества приложения. Пользователь должен видеть не только факт ошибки, но и понимать, что делать дальше.

## Фаза 12 — UX полировка и визуальная консистентность

К этой фазе у тебя уже будет достаточно экранов, чтобы возникла необходимость в едином дизайн-слое.

### Что стоит унифицировать

- стили кнопок;
- стили карточек;
- заголовки экранов;
- loading placeholders;
- empty-state блоки;
- error banners;
- spacing и шрифтовую шкалу;
- повторно используемые `UserControl` для секций shell.

### Рекомендуемая структура

```text
DevAssistant
└── ContourAI
    ├── Shared
    │   └── UI
    │       ├── AppTheme.axaml
    │       ├── Colors.axaml
    │       ├── Spacing.axaml
    │       └── ControlStyles.axaml
    └── Widgets
        ├── Common
        │   ├── EmptyStateView.axaml
        │   ├── ErrorBannerView.axaml
        │   ├── LoadingStateView.axaml
        │   └── SectionHeaderView.axaml
        └── Shell
            ├── SidebarView.axaml
            └── TopbarView.axaml
```

### Цель

Визуально приложение должно восприниматься как единый продукт, а не как набор отдельных экранов. Это особенно важно для защиты диплома и общего впечатления от проекта.

## Фаза 13 — Подготовка к диплому

Финальная фаза — это не только код, но и управляемый demo flow.

### Что нужно подготовить

- стабильный демонстрационный сценарий от login до поиска;
- тестовый сервер и тестовые данные;
- 2–3 заранее подготовленных проекта;
- документы в разных статусах;
- хотя бы один failed indexing case;
- хотя бы один успешный RAG search case;
- краткую техническую схему архитектуры клиента.

### Что должно быть зафиксировано в документации

- структура проекта;
- основные фичи по фазам;
- используемые сервисы и stores;
- схема взаимодействия client ↔ API;
- known limitations;
- roadmap после диплома.

## Рекомендуемый порядок реализации

Ниже приведен практический порядок, который даст самый быстрый и стабильный прогресс:

1. Фаза 3 — session/auth lifecycle.
2. Фаза 10 — navigation/shared state.
3. Фаза 4 — projects.
4. Фаза 6 — documents.
5. Фаза 7 — indexing.
6. Фаза 8 — RAG search.
7. Фаза 5 — global chat.
8. Фаза 9 — system monitor.
9. Фаза 11 — unified error handling.
10. Фаза 12 — UX polishing.
11. Фаза 13 — diploma/demo preparation.

Такой порядок полезен потому, что сначала строится инфраструктура приложения, затем рабочие бизнес-сущности, затем демонстрационные AI-сценарии, и только после этого идет полировка.

## Definition of Done по следующим фазам

Фаза считается завершенной, если одновременно выполнены четыре условия:

1. Есть рабочий экран Avalonia с реальным API binding.
2. Есть отдельный service для backend-доступа.
3. Есть DTO/Entities вне ViewModel.
4. Есть loading/error/empty state и понятный пользовательский сценарий.

Именно такая дисциплина позволит не скатиться обратно в мок-архитектуру и сохранить проект управляемым до финальной защиты.[cite:50][cite:61][cite:75][cite:76][cite:83]
