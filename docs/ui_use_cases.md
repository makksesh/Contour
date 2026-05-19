Репозиторий для выполнения задачи: GitHub LocalServerAI.
Задача:
Ты — senior backend engineer (.NET/C#). Задача: спроектировать и реализовать
improved Indexing + RAG для ASP.NET Core проекта DevAssistant.

## Контекст проекта

Архитектура: Clean Architecture (Domain / Application / Infrastructure),
CQRS через MediatR, PostgreSQL (EF Core), ChromaDB (векторное хранилище),
отдельные endpoints для chat и embedding моделей.

Существующие сущности (менять нельзя, только расширять):
- Document: OriginalPath, FileName, ProjectId,
  Status (Uploaded/Pending/Processing/Indexed/Failed), Chunks
- DocumentChunk: Content, TokenCount, StartOffset, EndOffset, ExternalVectorId
- IndexingTask: статусы Queued/Running/Completed/Failed
- ProjectSettings: EmbeddingModelEndpointId, ChatModelEndpointId,
  RagTopK (1–20, default 5), UseRagContext,
  ContextWindowSize (1–50, rolling window сообщений — НЕ токенный лимит),
  MaxTokens, Temperature
- RagChunkDto: record(VectorId, Content, Score,
  Metadata: IReadOnlyDictionary<string,string>)
- Существующий интерфейс: IDocumentParser.ParseToTextAsync(filePath, ct)
- API: api/indexing/*, api/rag/search
- Namespace: DevAssistant.*

Текущий недостаток: BuildChunks использует фиксированный split
по 2000 символов + overlap 250 символов. Нужно заменить семантическим
chunking'ом без смены внешних API-контрактов.

---

## Принцип расширяемости (обязательно соблюдать везде)

Архитектура должна поддерживать добавление новых форматов файлов
(.py, .docx, .pdf, .ipynb и др.) и новых языков программирования
без изменения Domain и пайплайна индексирования.

Правила:
- Extraction (извлечение текста из формата) и Chunking (семантическое
  разбиение по языку) — это две независимые оси расширения.
  Реализовывать их как отдельные registry-based интерфейсы.
- ChunkKind + ChunkSubKind (двухуровневый) вместо плоского enum,
  чтобы Python-функция и C#-метод были одним SubKind=MemberMethod.
- ParsedDocument должен нести язык и MIME — chunker не определяет язык сам.
- Boost-правила retrieval вынести в IRetrievalBoostProvider,
  не хардкодить в сервисе.
- FallbackTextChunker применяется ко всем нераспознанным типам
  после извлечения текста.

---

## 1. Расширение DocumentChunk (Domain)

Добавить поля через новый статический фабричный метод CreateSemantic()
без изменения существующего Create():

```csharp
// Новые поля DocumentChunk:
ChunkKind    Kind          // Code | Document | Config | FallbackText
ChunkSubKind SubKind       // TypeSummary | MemberMethod | ControllerAction |
                           // Dto | Enum | Interface | EntryPoint |
                           // Section | CodeFence | TableBlock |
                           // ConfigSection | Custom
string?      SemanticPath  // "ChatService > SendMessageAsync"
string?      HeadingPath   // "H1 > H2 > H3" (для Document)
string?      Language      // "csharp" | "python" | "markdown" | "json" | ...
Guid?        ParentChunkId // ссылка на summary-чанк того же файла
string       ContentHash   // SHA256 первые 16 hex
string?      RootEntityName
int          LineStart
int          LineEnd
```

---

## 2. Расширение метаданных ChromaDB

Дополнить BuildChunkMetadata() (сейчас: 6 полей) до:

projectId, documentId, chunkId, fileName, filePath, fileExtension,
language, mimeType, chunkKind, chunkSubKind, semanticPath, headingPath,
lineStart, lineEnd, tokenCount, parentChunkId, rootEntityName,
contentHash, indexedAtUtc, embeddingModel

Дополнительно для ControllerAction:
httpMethod, routeTemplate, requestType, responseType, authorizeAttributes

---

## 3. Интерфейсы (Application layer)

### 3.1 Content Extraction (замена IDocumentParser)

```csharp
// ParsedDocument — общий результат извлечения
public sealed record ParsedDocument(
    string FilePath,
    string Language,               // определяется экстрактором
    string MimeType,
    IReadOnlyList<string> Lines,
    string FullText);

// Экстрактор для конкретного формата файла
public interface IContentExtractor
{
    bool CanHandle(string fileExtension, string? mimeType = null);
    Task<ParsedDocument> ExtractAsync(string filePath, CancellationToken ct);
}

// Фасад — существующий IDocumentParser делегирует сюда
public interface IContentExtractorRegistry
{
    Task<ParsedDocument> ExtractAsync(string filePath, CancellationToken ct);
}
```

Реализовать экстракторы:
- PlainTextExtractor (.cs, .md, .json, .yml, .yaml, .csproj, .txt и др.)
- FallbackBinaryExtractor (возвращает пустой FullText, не падает)

Зарезервировать расширение: DocxExtractor, PdfExtractor, NotebookExtractor
(описать как TODO с указанием NuGet: DocumentFormat.OpenXml, PdfPig,
System.Text.Json для .ipynb).

### 3.2 Semantic Chunking

```csharp
public interface ISemanticChunker
{
    bool CanHandle(string fileExtension, string? mimeType = null);
    IReadOnlyList<SemanticChunk> Chunk(ParsedDocument document);
}

public sealed record SemanticChunk(
    string Content,
    ChunkKind Kind,
    ChunkSubKind SubKind,
    string? SemanticPath,
    string? HeadingPath,
    int LineStart,
    int LineEnd,
    int EstimatedTokens,
    Guid? ParentChunkId,
    string? RootEntityName,
    IReadOnlyDictionary<string, string> ExtraMetadata);

public interface IChunkerRegistry
{
    ISemanticChunker Resolve(string fileExtension, string? mimeType = null);
}
```

### 3.3 Retrieval Boost

```csharp
public sealed record RetrievalBoostRule(
    string[] QueryKeywords,
    ChunkKind TargetKind,
    ChunkSubKind? TargetSubKind,
    float BoostFactor);

public interface IRetrievalBoostProvider
{
    IReadOnlyList<RetrievalBoostRule> GetRules();
}
```

Реализовать DefaultRetrievalBoostProvider с правилами:
- "где", "как работает", "endpoint", "метод", "dto" →
  ControllerAction/MemberMethod boost x1.3
- "архитектура", "опиши", "структура", "модуль" →
  TypeSummary boost x1.2
- (расширяется через DI без изменения retrieval-сервиса)

### 3.4 RAG Retrieval Service

```csharp
public interface IRagRetrievalService
{
    Task<RagContextResult> RetrieveAsync(
        Guid projectId,
        string query,
        ProjectSettings settings,
        CancellationToken ct);
}

public sealed record RagContextResult(
    IReadOnlyList<RagChunkDto> Chunks,
    string FormattedContext,    // готов для вставки в промпт
    int TotalTokensEstimate);
```

---

## 4. Правила chunking по типам файлов

### 4.1 C# (.cs) — через Roslyn (Microsoft.CodeAnalysis.CSharp)

Уровни разбиения:
1. Файл → 1 Summary-чанк (SubKind=TypeSummary): namespace + все type names
    + public member signatures. ParentChunkId = null.
2. Небольшой тип (≤ 600 токенов) → 1 CodeType-чанк (SubKind=TypeSummary).
3. Большой тип → TypeSummary-чанк + MemberMethod/MemberProperty на каждый
   public метод/свойство/конструктор.
4. Controller: TypeSummary + ControllerAction на каждый action.
5. DTO, record, enum, interface → SubKind Dto/Enum/Interface (type-level чанк).
6. Метод > 800 токенов → делить по region/блокам if-switch/локальным функциям,
   НЕ разрывать сигнатуру с телом.
7. Private helpers < 80 токенов → присоединить к ближайшему public методу.
8. Program.cs → SubKind=EntryPoint, блоки по логическим секциям
   (services, middleware, auth, endpoints mapping).

Размеры: 200–600 токенов (цель), мягкий максимум 800, жёсткий 1000.

Overlap (семантический):
Каждый member-чанк содержит: namespace, имя типа, сигнатуру метода/свойства,
атрибуты ([HttpGet], [Authorize], [FromBody] и др.), 3–5 строк контекста.
Overhead: 30–80 токенов. Тело метода не дублировать между соседними чанками.

### 4.2 Markdown (.md) — через Markdig

- 1 секция ## или ### = 1 Section-чанк при ≤ 900 токенов.
- Длинная секция → делить по абзацам/спискам, сохранять HeadingPath.
- Code fence > 100 токенов → SubKind=CodeFence, ParentChunkId = текстовый родитель.
- Маленькие соседние секции одного уровня можно объединять.

Размеры: 250–700 токенов (цель), мягкий 900, жёсткий 1100.

Overlap: breadcrumb H1 > H2 > H3 + опционально первый абзац родителя.
Overhead: 20–60 токенов.

### 4.3 Config (.json, .yml, .yaml, .csproj, .props)

- Разбивать по верхнеуровневым секциям/логическим группам.
- Не резать посередине объекта или массива.
- SubKind = ConfigSection.
- Размеры: 100–400 токенов, максимум 700.
- Overlap: только путь секции + имя файла.

### 4.4 Fallback (все остальные / нераспознанные)

- Фиксированный split: 300–500 токенов, overlap 40–70 токенов посимвольный.
- Kind=FallbackText, SubKind=Custom.
- Применяется в том числе к .docx и .pdf после извлечения текста,
  если специфичного chunker ещё нет.

### 4.5 Зарезервировано для будущего расширения

Описать как TODO-заглушки в IChunkerRegistry с указанием подхода:
- Python (.py): tree-sitter-python — разбиение по def/class/decorator
- Jupyter (.ipynb): JSON-парсинг → cell-chunks (code cell / markdown cell)
- DOCX (.docx): DocumentFormat.OpenXml → разбиение по стилям заголовков
  (Heading1/2/3) аналогично Markdown
- PDF (.pdf): PdfPig → разбиение по параграфам/страницам

---
## 5. Пайплайн индексирования
Заменить метод BuildChunks() в ProcessIndexingTaskHandler,
сохранив транзакционную схему (блок 1 до SaveChanges, блок 2 best-effort):
1.	FileFilter
      o	Проверить расширение, пропустить бинарные и > 5 МБ
      o	Исключить: .git/, bin/, obj/, node_modules/, venv/, dist/
2.	HashCheck
      o	Вычислить ContentHash (SHA256 от содержимого, первые 16 hex)
      o	Пропустить файл если hash совпадает — идемпотентность
3.	Extract
      o	IContentExtractorRegistry.ExtractAsync(filePath) → ParsedDocument
      o	При ошибке: логировать, Document.MarkFailed, не терять остальные файлы
4.	Chunk
      o	IChunkerRegistry.Resolve(ext, mimeType).Chunk(parsedDocument)
      o	Отфильтровать чанки < 10 токенов
5.	Embed
      o	Батчи по 20 чанков к embedding endpoint
      o	Retry 3 раза с Polly (exponential backoff)
      o	Пропускать пустые чанки
6.	Persist (в рамках транзакции)
      o	DocumentChunk.CreateSemantic(...) для каждого чанка
      o	SetExternalVectorId
      o	document.MarkIndexed(chunks), task.Complete()
      o	SaveChangesAsync
7.	VectorUpsert (best-effort, после SaveChanges)
      o	VectorStore.UpsertAsync с расширенными метаданными
      o	Ошибки логировать, не откатывать PostgreSQL-состояние

---

## 6. Пайплайн retrieval

Алгоритм IRagRetrievalService.RetrieveAsync:
1.	Если UseRagContext = false → вернуть пустой RagContextResult
2.	Embedding запроса через EmbeddingModelEndpointId проекта
3.	Поиск в ChromaDB:
      where = { projectId: settings.ProjectId }
      topN = max(RagTopK * 3, 15) кандидатов
4.	Post-filtering:
      a. Убрать дубликаты по ContentHash
      b. Не более 3 чанков из одного файла (filePath в metadata)
      c. Score threshold: отбросить score < 0.30
      d. Применить IRetrievalBoostProvider.GetRules() к оставшимся
5.	Взять top-RagTopK после ре-ранкинга
6.	Собрать FormattedContext:
      Для каждого чанка — citation header:
      "// [fileName:lineStart] semanticPath" + Content
      Ограничить суммарный объём: ~35% от MaxTokens настроек проекта
7.	Вернуть RagContextResult(Chunks, FormattedContext, TotalTokensEstimate)


---

## 7. Интеграция с chat (Application/Chat)

В ChatService.SendMessageAsync и StreamAsync перед формированием промпта:

```csharp
if (settings.UseRagContext)
{
    var ragResult = await _ragRetrievalService.RetrieveAsync(
        project.Id, userMessage, settings, ct);

    systemPrompt = BuildSystemPromptWithContext(
        settings.SystemPrompt, ragResult.FormattedContext);
}
```

Поддержать оба пути: обычный ответ и SSE-стрим.

---

## 8. Sane defaults

| Параметр              | Рекомендация    | Текущий default        |
|-----------------------|-----------------|------------------------|
| RagTopK               | 6–8             | 5 → увеличить до 6     |
| Первичный retrieval   | RagTopK × 3     | —                      |
| Score threshold       | 0.30            | —                      |
| Max chunks per file   | 3               | —                      |
| Context budget        | ~35% MaxTokens  | —                      |
| ContextWindowSize     | 10 сообщений    | 10 (корректно)         |
| Chunk target (code)   | 200–600 токенов | 500 символов ≈ 125 ❌  |

Примечание: ContextWindowSize — rolling window истории сообщений (1–50),
не токенный лимит. Задокументировать явно в XML-комментарии.

---

## 9. Этапы реализации

1. **Domain**: расширить DocumentChunk (новые поля через CreateSemantic),
   добавить ChunkKind + ChunkSubKind enums
2. **Application contracts**: ParsedDocument, SemanticChunk,
   IContentExtractor, IContentExtractorRegistry, ISemanticChunker,
   IChunkerRegistry, IRetrievalBoostProvider, IRagRetrievalService
3. **Infrastructure — Extractors**:
   PlainTextExtractor, FallbackBinaryExtractor
4. **Infrastructure — Chunkers**:
   CSharpRoslynChunker, MarkdownChunker, ConfigChunker, FallbackTextChunker
5. **Application**: заменить BuildChunks в ProcessIndexingTaskHandler
6. **Application**: реализовать IRagRetrievalService
7. **Application/Chat**: интегрировать retrieval перед send/stream
8. **Observability**: логировать chunk stats (count, avg token size, hit-rate),
   согласовать с ILogger<T>

---

## 10. Критерии качества

- Чанки .cs совпадают со смысловыми единицами, сигнатуры не разрываются
- Markdown индексируется по заголовочной иерархии
- Overlap семантический — сигнатура + контекст, не дублирование тела
- Retrieval возвращает конкретные методы/классы/endpoints, не шумные обрезки
- Метаданные достаточны для citation в ответе LLM
- Индексирование идемпотентно (ContentHash + DeleteChunks перед re-index)
- Добавление нового типа файла = 1 новый класс IContentExtractor или
  ISemanticChunker + регистрация в DI, без изменения пайплайна

---

## Формат ответа

1. C#-скелеты всех интерфейсов и Domain-изменений
2. Полная реализация CSharpRoslynChunker с примерами Roslyn SyntaxWalker
3. Полная реализация MarkdownChunker
4. Изменения в ProcessIndexingTaskHandler
5. Реализация IRagRetrievalService + DefaultRetrievalBoostProvider
6. TODO-заглушки для Python/DOCX/PDF/ipynb с указанием NuGet-пакетов
7. Список компромиссов: почему это лучше naive fixed-size split

