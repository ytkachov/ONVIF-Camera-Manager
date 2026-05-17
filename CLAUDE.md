# ONVIF Camera Manager — Claude guidance

## Правила работы

1. **НЕ указывать Claude соавтором в коммитах.** Никаких `Co-Authored-By: Claude ...`, никаких `🤖 Generated with Claude Code` в сообщениях коммитов и PR. Авторство — только пользователь.
2. **Сообщения коммитов и PR — только по-английски.** Артефакты проекта (README, docs, code comments, identifiers) — тоже по-английски.
3. Отвечать пользователю по-русски, технические термины и идентификаторы — на английском.
4. Перед нетривиальными изменениями — короткий план, потом код.
5. Не добавлять комментарии, объясняющие *что* делает код; только *почему*, если это не очевидно.

## Стек

- **Платформа:** .NET 8 (LTS до ноября 2026). Не апгрейдить до 9/10 без явного указания.
- **UI:** WPF (`<UseWPF>true</UseWPF>` в csproj, `<TargetFramework>net8.0-windows</TargetFramework>`).
- **Язык:** C# 12 (`<LangVersion>latest</LangVersion>` на .NET 8 = C# 12), `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`.
- **MVVM:** `CommunityToolkit.Mvvm` — source generators (`[ObservableProperty]`, `[RelayCommand]`, `[NotifyPropertyChangedFor]`). Не писать boilerplate-`INotifyPropertyChanged` руками.
- **DI:** `Microsoft.Extensions.Hosting` + `Microsoft.Extensions.DependencyInjection`. Создавать `IHost` в `App.OnStartup`, регистрировать `MainWindow` и ViewModel-ы, разрешать через `ServiceProvider`.
- **Логирование:** `Microsoft.Extensions.Logging` через абстракцию `ILogger<T>`; провайдер по умолчанию — `Serilog.Extensions.Hosting` с sink-ами `Console` и `File` (rolling).
- **Конфиг:** `Microsoft.Extensions.Configuration` (`appsettings.json` + `appsettings.{Environment}.json`).
- **Тесты:** `xUnit` + `FluentAssertions` + `NSubstitute`. UI-тестов на WPF не делаем; ViewModel-тесты — обязательны.

## ONVIF-специфика

- Для ONVIF-клиента — пакет `Onvif.Core` / самосгенерированные WCF-прокси через `dotnet-svcutil` из WSDL (`devicemgmt`, `media`, `ptz`, `imaging`, `events`).
- Discovery — WS-Discovery через `System.ServiceModel.Discovery` или `OnvifDiscovery` NuGet.
- Аутентификация ONVIF — WS-UsernameToken (PasswordDigest). Не использовать plain text.
- Превью/живое видео — RTSP (`LibVLCSharp.WPF` или `FFME.Windows`); прямые H.264-декодеры через `FFmpeg.AutoGen` только если есть причина.
- Все ONVIF-вызовы — асинхронные, `CancellationToken` пробрасывается до UI.

## Архитектурные правила

- **Проекты:**
  - `OnvifManager.Core` — `net8.0`, без WPF. POCO-модели (`Models/`) и ONVIF-сервисы (`Services/`). Запрещено тянуть `System.Windows.*` и любые UI-зависимости.
  - `OnvifManager` — `net8.0-windows`, `UseWPF=true`. ViewModels + Views + DI-композиция. Ссылается на `OnvifManager.Core`.
  - `OnvifManager.Cli` — `net8.0`, `Exe`, `AssemblyName=onvif`. Консольный фронтенд для скриптинга и troubleshooting. Ссылается только на `OnvifManager.Core`, без WPF-сборок.
  - Любая новая ONVIF-логика (сервисы, парсеры, модели) — в `Core`, чтобы оба фронтенда могли её использовать. `*.Infrastructure` (отдельный слой persistence/IO) пока не выделен — добавится, когда понадобится.
- **MVVM строго:** во View — никакого code-behind с бизнес-логикой; только обработчики, которые нельзя выразить через binding/Behaviors.
- **Async везде:** в ViewModel-ах — `async Task`, `IAsyncRelayCommand`. На UI-потоке не блокируем `.Result` / `.Wait()`. Для библиотечного кода — `ConfigureAwait(false)`; в коде ViewModel-ов (WPF) — можно опускать.
- **IDisposable / IAsyncDisposable:** все ONVIF-клиенты, RTSP-сессии, `CancellationTokenSource` — освобождать. ViewModel-ы, держащие подписки/таймеры — реализуют `IDisposable` и вычищаются при закрытии View.
- **Конфигурация цели сборки:** `x64`, `WindowsAppSDK` не нужен (это WPF, не WinUI).

## Что НЕ делать

- Не использовать `PropertyChanged.Fody` (есть source generators в `CommunityToolkit.Mvvm`).
- Не писать `Prism` / `Caliburn.Micro` без явной причины — `CommunityToolkit.Mvvm` + DI покрывают 95%.
- Не использовать `BackgroundWorker`, `Thread.Sleep`, `Dispatcher.Invoke` где работает `await` / `IProgress<T>`.
- Не ловить `Exception` без логирования и проброса дальше; не глотать ошибки молча.
- Не хранить пароли камер в открытом виде — `DPAPI` (`ProtectedData`) или Windows Credential Manager.

## CLI (`onvif.exe`)

- Парсер аргументов — `System.CommandLine` (2.0-beta4). Структура verb-noun: `onvif discover`, `onvif get device-info`, `onvif set hostname`.
- Общие опции подключения — `--host`, `--port`, `--user`, `--pass`, `--timeout`; пароль может быть в env `ONVIF_PASSWORD`.
- Вывод — текст по умолчанию, `--json` для машинно-читаемого; ошибки идут в stderr.
- Exit codes: `0` ok, `1` generic, `2` invalid args (System.CommandLine), `3` HTTP/connection, `4` auth (`401/403`).
- Новые `get`/`set` команды добавляются в `OnvifManager.Cli/Commands/` поверх существующих сервисов из `OnvifManager.Core` (см. шаблон в `HostnameGetCommand.cs` / `HostnameSetCommand.cs`).
- DI/IHost для CLI пока не подключён — каждая команда локально конструирует `OnvifClientProvider` через `CommandSupport.CreateProvider`. Полный `IHost` + Serilog добавятся, когда понадобятся `appsettings.json` и файловый лог.

## Команды

- Сборка: `dotnet build -c Release`
- Тесты: `dotnet test`
- Запуск WPF: `dotnet run --project OnvifManager`
- Запуск CLI: `dotnet run --project OnvifManager.Cli -- <command>` (например `... -- discover`, `... -- get device-info --host 192.168.1.10 --user admin --pass <pwd>`)
- Форматирование: `dotnet format`
