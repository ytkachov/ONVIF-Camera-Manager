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

- **Слои:** `*.Core` (POCO, доменные сервисы, интерфейсы, без зависимости от WPF) → `*.Infrastructure` (ONVIF, persistence, IO) → `*.App` (WPF, ViewModel-ы, Views).
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

## Команды

- Сборка: `dotnet build -c Release`
- Тесты: `dotnet test`
- Запуск: `dotnet run --project src/OnvifCameraManager.App`
- Форматирование: `dotnet format`
