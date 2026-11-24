# ChatGPT Limit Panel 1.9.1

## Русский

Панель лимитов Codex / Work для настольного ChatGPT в Windows x64. Показывает остаток лимитов и время их сброса, доступные сбросы и кредиты Codex. Нужен установленный Codex со входом в тот же аккаунт. Отдельные API-ключ, Git и Node.js не требуются.

### Установка и первый запуск

Выбери **Плагины → Добавить → Загрузить архив плагина** и укажи ZIP. Для установки с поддержкой обновления одной кнопкой распакуй ZIP и выполни в PowerShell из распакованной папки `.\runtime\node.exe .\overlay\install-plugin.mjs .`. Команда `codex --version` должна работать в том же PowerShell.

Открой новый чат, введи **@**, выбери **ChatGPT Limit Panel** в разделе **«Плагины»** и отправь **«Открой управление панелью лимитов Codex/Work»**. В появившейся карточке нажми **«Запустить»**. Если плагина нет в списке @, проверь, что он установлен и включён в разделе **«Плагины»**.

### Управление панелью

- Левый клик меняет размер; правый открывает меню.
- **Содержимое панели** — показ сбросов и кредитов, выбор видимых лимитов. Если пятичасового ограничения нет, панель по умолчанию скрывает его и переносит контур на недельный. **«Доступные сбросы»** показывает известные сроки истечения, не расходуя сбросы.
- **Расположение → Панель задач → Показывать индикатор** включает индикатор Windows; **«Что показывать»** выбирает проценты или кредиты. **Расположение → Живая иконка в трее** включается отдельно. Любой из этих индикаторов позволяет обновлять лимиты с закрытым окном ChatGPT.
- Кредиты на панели — баланс Codex. Баланс API не отображается.

### Обновление и удаление

**ПКМ по панели → Проверить новую версию → Обновить**. Если кнопка доступна, панель проверит архив и перезапустится; ChatGPT закрывать не нужно. Если есть только **«Открыть релиз»**, используй [ручную инструкцию](UPGRADING.md). Точку нового выпуска можно скрыть через **Настройки → Показывать точку обновления**.

Удаление: открой карточку в новом чате через **@ → ChatGPT Limit Panel**, раскрой **Настройки плагина → Удалить плагин…** и подтверди. Чтобы сохранить данные, оставь галочку **«Также удалить настройки, положения и журналы»** снятой. Подробнее: [полное удаление](UNINSTALL.md).

Если лимиты не обновляются, панель сохраняет последние значения и пробует запросить данные снова. В `%LOCALAPPDATA%\ChatGPTLimitPanel\quota-failures.json` хранятся последние 10 сбоев с этапом и длительностью попыток, без токенов, переписки и содержимого ответов.

## English

This Windows x64 panel shows Codex / Work quotas and reset times, available resets, and Codex credits in desktop ChatGPT. Install Codex and sign in to the same account. No separate API key, Git, or Node.js installation is needed.

### Install and start

Choose **Plugins → Add → Upload plugin archive** and select the ZIP. To enable one-click updates, extract the ZIP and run `.\runtime\node.exe .\overlay\install-plugin.mjs .` in PowerShell from the extracted folder instead. `codex --version` must work in that PowerShell session.

Start a new chat, type **@**, choose **ChatGPT Limit Panel** under **Plugins**, and send **“Open the Codex/Work limit panel controls.”** Click **Start** in the reply. If the plugin is missing from the @ menu, check that it is installed and enabled under **Plugins**.

### Panel controls

- Left-click to cycle through sizes; right-click for settings.
- **Panel content** controls resets, credits, and visible quotas. If there is no 5-hour limit, the panel hides it and uses the weekly limit for the outline by default. **Available resets** lists known expirations without using any resets.
- Enable **Placement → Taskbar → Show indicator**, then use **Content** to choose percentages or credits. **Placement → Live tray icon** is independent. Either indicator lets the panel continue refreshing while the ChatGPT window is closed.
- The panel shows Codex credits. It does not show the API billing balance.

### Update or remove

**Right-click the panel → Check for a new version → Update**. When offered, the panel verifies the archive and restarts itself while ChatGPT stays open. If only **Open release** appears, follow the [manual upgrade guide](UPGRADING.md). Hide the update dot under **Settings → Show update dot**.

To remove the plugin, open its control card in a new chat via **@ → ChatGPT Limit Panel**, then choose **Plugin settings → Remove plugin…** and confirm. Leave **Also delete settings, saved positions and logs** unchecked to keep your data. See the [uninstall guide](UNINSTALL.md).

If a request fails, the panel keeps the last known limits and retries. The local `%LOCALAPPDATA%\ChatGPTLimitPanel\quota-failures.json` records the stage and duration of the last 10 incidents without tokens, chats, or response contents.

[Project and releases](https://github.com/MrBogomoll/chatgpt-limit-panel) · [MIT License](LICENSE)
