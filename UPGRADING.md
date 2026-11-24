# Обновление ChatGPT Limit Panel / Updating ChatGPT Limit Panel

## Русский

### Обновить одной кнопкой

1. Нажми **ПКМ по самой панели лимитов → Проверить новую версию**.
2. Если доступна новая версия, нажми **«Обновить»**. Панель скачает ZIP с GitHub, сверит контрольную сумму и перезапустится сама. ChatGPT можно оставить открытым.
3. Окно можно закрыть кнопкой **«Скрыть окно»**: это не отменяет установку. Чтобы снова увидеть его, выбери **«Ход обновления…»** в меню панели. После перезапуска версию можно сверить через **«Проверить новую версию»**.

Бирюзовая точка показывает, что появился новый стабильный выпуск. Чтобы скрыть её, сними галочку **Настройки → Показывать точку обновления**. В карточке управления кнопка **«Проверить обновления»** может открыть страницу релиза; установка одной кнопкой находится в меню самой панели.

### Если кнопки «Обновить» нет

Если окно сообщает, что установленная версия актуальна или новее стабильного релиза, обновлять ничего не нужно. Если выпуск найден, но доступна только кнопка **«Открыть релиз»**, используй ZIP. Установка одной кнопкой поддерживается для пакетов, зарегистрированных локальным установщиком панели, если у выпуска есть архив и файл с контрольной суммой.

1. Скачай готовый ZIP для Windows x64 из **Assets** в [последнем стабильном выпуске](https://github.com/MrBogomoll/chatgpt-limit-panel/releases/latest). Выбери `chatgpt-limit-panel-<версия>-windows-x64.zip`; **Source code** — это исходники. Контрольная сумма SHA-256 находится в соседнем файле `.sha256`.
2. Останови панель через **«Остановить панель»**. Для удаления старого пакета можно открыть **Настройки плагина → Удалить плагин…** в карточке. Оставь галочку **«Также удалить настройки, положения и журналы»** снятой. Если помощника удаления нет, удали старый пакет в разделе **«Плагины»**.
3. Поставь новую версию: **Плагины → Добавить → Загрузить архив плагина**. Если пользуешься локальным установщиком, распакуй ZIP и запусти его, как описано в [README проекта](https://github.com/MrBogomoll/chatgpt-limit-panel/blob/main/README.md).
4. Открой новый чат в настольном приложении. Введи **@**, выбери **ChatGPT Limit Panel** в списке **«Плагины»** и отправь **«Открой управление панелью лимитов Codex/Work»**. В карточке нажми **«Запустить»**.

Настройки, положение панели и журнал хранятся отдельно от пакета. Для обычного обновления их удалять не нужно. Если панель не запустилась, сначала убедись, что старая остановлена. Если ошибка повторяется, укажи версию панели и текст ошибки в [Issues](https://github.com/MrBogomoll/chatgpt-limit-panel/issues).

## English

### Update in one click

1. **Right-click the limit panel itself → Check for a new version**.
2. If a new version is available, click **Update**. The panel downloads the ZIP from GitHub, verifies its checksum, and restarts itself. ChatGPT can stay open.
3. Use **Hide window** to dismiss the progress window without interrupting the update. Reopen it from **Update progress…** in the panel menu. After restart, use **Check for a new version** to confirm the installed version.

The teal dot marks a new stable release. To hide it, turn off **Settings → Show update dot**. The control card’s **Check for updates** can open the release page; use the panel’s own menu to install in one click.

### If there is no Update button

If the dialog says the installed version is current or ahead of the stable release, there is nothing to install. If a release is available but the dialog only offers **Open release**, use the ZIP. One-click updates require an installation registered by the panel’s local installer and a release with both an archive and a checksum file.

1. Download the built Windows x64 ZIP under **Assets** in the [latest stable release](https://github.com/MrBogomoll/chatgpt-limit-panel/releases/latest). Choose `chatgpt-limit-panel-<version>-windows-x64.zip`; **Source code** archives are for development. The SHA-256 checksum is in the adjacent `.sha256` file.
2. Stop the panel with **Stop panel**. To remove the old package using its card, choose **Plugin settings → Remove plugin…** and leave **Also delete settings, saved positions and logs** unchecked. If the removal helper is unavailable, remove the old package under **Plugins**.
3. Install the new release with **Plugins → Add → Upload plugin archive**. If you use the local installer, extract the ZIP and follow the [project README](https://github.com/MrBogomoll/chatgpt-limit-panel/blob/main/README.en.md).
4. Start a new chat in the desktop app. Type **@**, select **ChatGPT Limit Panel** under **Plugins**, and send **“Open the Codex/Work limit panel controls.”** Click **Start** in the card.

Settings, saved positions and logs are stored separately from the package. Keep them during ordinary upgrades. If the panel will not start, first check that the old panel has stopped. If the problem persists, include the panel version and error message in [Issues](https://github.com/MrBogomoll/chatgpt-limit-panel/issues).
