# Полное удаление ChatGPT Limit Panel / Uninstalling ChatGPT Limit Panel

## Русский

Начиная с **1.7.7**, удаление доступно в карточке самого плагина. Сначала открой её:

1. Открой новый чат в настольном приложении, где установлен плагин.
2. В поле сообщения введи **@**, найди **ChatGPT Limit Panel** и выбери его в разделе **«Плагины»**.
3. Отправь: **«Открой управление панелью лимитов Codex/Work»**. В ответе появится карточка с кнопками **«Запустить»**, **«Остановить»** и **«Обновить состояние»**.
4. Внизу этой карточки раскрой **«Настройки плагина»** и нажми **«Удалить плагин…»**.
5. Откроется отдельное окно подтверждения. Для полного удаления данных отметь **«Также удалить настройки, положения и журналы»**; без галочки они сохранятся для повторной установки.
6. Нажми **«Удалить плагин»** в окне подтверждения. Чтобы отказаться, нажми **«Отмена»** — само открытие окна ничего не удаляет.

Если ChatGPT Limit Panel отсутствует в меню @, проверь его установку и включение в разделе **«Плагины»**. После обновления используй новый чат. Если карточка открылась, но пункта удаления ещё нет, проверь, что установлена версия **1.7.7 или новее**; для старых версий инструкция ниже.

Помощник сам остановит монитор, уберёт его задачу запуска Windows, удалит регистрацию плагина и кэш. Проверенные копии пакета, созданные локальным установщиком, также очищаются. Открывать Планировщик и искать папки вручную не требуется. Пункта удаления в меню панели по ПКМ нет.

Во время удаления карточка плагина может закрыться. Результат остаётся в отдельном окне. Если очистку не удалось завершить, помощник сообщит об этом; незнакомые или изменённые файлы и перенаправленные папки не удаляются автоматически. Скачанный ZIP и твои исходники остаются на месте.

Вход в Codex, файлы авторизации и другие плагины сохраняются. Для обычного обновления используй [инструкцию по обновлению](UPGRADING.md), сохранив настройки.

<details>
<summary>Ручное удаление версий 1.7.6 и старше</summary>

1. Останови монитор: нажми **ПКМ по панели или её значку в трее → «Остановить панель»** либо открой карточку плагина и нажми **«Остановить»**.
2. Удали задачу запуска панели. Нажми **Win + R**, введи `taskschd.msc`, открой **Библиотеку планировщика заданий** и найди задачу с именем `ChatGPTQuotaOverlay.Host.<SID>` — вместо `<SID>` указан идентификатор пользователя. Открой её свойства: учётная запись должна принадлежать тебе, а в разделе **«Действия»** запускаться программа установленной панели (`ChatGPTLimitPanelLauncher.exe` либо `QuotaMonitor.exe`) с аргументом `--run-host`. Убедись, что задача не выполняется, и удали только эту задачу. Если она отсутствует, переходи к следующему шагу. Если владелец или действие не совпадает, не удаляй задачу; обратись в [Issues](https://github.com/MrBogomoll/chatgpt-limit-panel/issues).
3. В настольном клиенте открой **Плагины → установленные → ChatGPT Limit Panel → Управлять** и удали плагин. Названия кнопок могут немного отличаться между версиями клиента.
4. Чтобы удалить настройки, сохранённые положения, журнал и другие локальные данные панели, введи в адресной строке Проводника `%LOCALAPPDATA%` и удали папку `ChatGPTLimitPanel`, если она есть. После более старых установок также может оставаться папка `ChatGPTQuotaOverlay`: удали её только если она принадлежит этой панели. Удалённые настройки восстановить будет нельзя.
5. Если хочешь убрать и оставшиеся копии пакета, проверь `%USERPROFILE%\.codex\plugins\cache` и `%USERPROFILE%\.codex\plugins\local-sources`. Если этих папок или копий нет, ничего делать не нужно. Удаляй только каталог самого ChatGPT Limit Panel, предварительно проверив название плагина в его `.codex-plugin/plugin.json`; не удаляй общие каталоги маркетплейса или других плагинов. При необходимости отдельно удали скачанный ZIP там, куда ты его сохранил.

</details>

## English

Starting with **1.7.7**, removal is available in the plugin's own control card. Open it first:

1. Start a new chat in the desktop app where the plugin is installed.
2. Type **@** in the message box, find **ChatGPT Limit Panel**, and select it under **Plugins**.
3. Send: **"Open the Codex/Work limit panel controls."** The reply will contain the card with **Start**, **Stop** and **Refresh status** buttons.
4. Expand **Plugin settings** at the bottom of this card and click **Remove plugin…**.
5. A separate confirmation window opens. Select **Also delete settings, saved positions and logs** to remove all panel data; leave it unchecked to keep preferences for reinstalling.
6. Click **Remove plugin** in that window. Choose **Cancel** to keep the installation; opening the window alone does not remove anything.

If ChatGPT Limit Panel is missing from the @ menu, check that it is installed and enabled under **Plugins**. Start a new chat after updating. If the card has no removal action, check that version **1.7.7 or later** is installed; instructions for older versions are below.

The helper stops the monitor, removes its Windows launch task, and uninstalls the plugin registration and cache. Verified package copies made by the local installer are cleaned too. You do not need to open Task Scheduler or find folders manually. The panel's right-click menu has no uninstall action.

The plugin card may close during removal; the separate window keeps showing the result. Incomplete cleanup is reported. Unrecognized or modified files and redirected directories are retained. Your downloaded ZIP and source checkouts are kept.

Codex sign-in, authentication files and other plugins are preserved. For a normal update use the [upgrade guide](UPGRADING.md) and keep your preferences.

<details>
<summary>Manual removal for versions 1.7.6 and earlier</summary>

1. Stop the monitor: **right-click the panel or its tray icon → Stop panel**, or open its plugin control card and click **Stop**.
2. Remove the panel's launch task. Press **Win + R**, enter `taskschd.msc`, open **Task Scheduler Library**, and find `ChatGPTQuotaOverlay.Host.<SID>`, where `<SID>` is the user's identifier. Open its properties: the account must be yours, and **Actions** must run the installed panel's program (`ChatGPTLimitPanelLauncher.exe` or `QuotaMonitor.exe`) with the `--run-host` argument. Make sure it is not running and delete only that task. If no task exists, continue to the next step. If its owner or action does not match, leave it untouched and contact [Issues](https://github.com/MrBogomoll/chatgpt-limit-panel/issues).
3. In the desktop client, open **Plugins → installed → ChatGPT Limit Panel → Manage** and remove the plugin. Button names can vary slightly by client version.
4. To remove panel preferences, saved positions, logs and other local data, enter `%LOCALAPPDATA%` in File Explorer's address bar and delete `ChatGPTLimitPanel` if present. Older installations may also leave `ChatGPTQuotaOverlay`; delete it only if it belongs to this panel. Deleted preferences cannot be recovered.
5. To remove any remaining package copies, check `%USERPROFILE%\.codex\plugins\cache` and `%USERPROFILE%\.codex\plugins\local-sources`. If those directories or copies do not exist, no action is needed. Delete only the ChatGPT Limit Panel's own directory after checking the plugin name in its `.codex-plugin/plugin.json`; do not delete shared marketplace directories or other plugins. If desired, separately delete the downloaded ZIP from wherever you saved it.

</details>
