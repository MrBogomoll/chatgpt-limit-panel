# Полное удаление ChatGPT Limit Panel / Uninstalling ChatGPT Limit Panel

## Русский

Эти действия предназначены для полного удаления панели. Если ты собираешься обновить её или установить снова с прежними настройками, используй [инструкцию по обновлению](UPGRADING.md) и не удаляй папку настроек.

1. Останови монитор: нажми **ПКМ по панели или её значку в трее → «Остановить панель»** либо открой карточку плагина и нажми **«Остановить»**.
2. Удали задачу запуска панели. Нажми **Win + R**, введи `taskschd.msc`, открой **Библиотеку планировщика заданий** и найди задачу с именем `ChatGPTQuotaOverlay.Host.<SID>` — вместо `<SID>` указан идентификатор пользователя. Открой её свойства: учётная запись должна принадлежать тебе, а в разделе **«Действия»** запускаться программа установленной панели (`ChatGPTLimitPanelLauncher.exe` либо `QuotaMonitor.exe`) с аргументом `--run-host`. Убедись, что задача не выполняется, и удали только эту задачу. Если она отсутствует, переходи к следующему шагу. Если владелец или действие не совпадает, не удаляй задачу; обратись в [Issues](https://github.com/MrBogomoll/chatgpt-limit-panel/issues).
3. В настольном клиенте открой **Плагины → установленные → ChatGPT Limit Panel → Управлять** и удали плагин. Названия кнопок могут немного отличаться между версиями клиента.
4. Чтобы удалить настройки, сохранённые положения, журнал и другие локальные данные панели, введи в адресной строке Проводника `%LOCALAPPDATA%` и удали папку `ChatGPTLimitPanel`, если она есть. После более старых установок также может оставаться папка `ChatGPTQuotaOverlay`: удали её только если она принадлежит этой панели. Удалённые настройки восстановить будет нельзя.
5. Если хочешь убрать и оставшиеся копии пакета, проверь `%USERPROFILE%\.codex\plugins\cache` и `%USERPROFILE%\.codex\plugins\local-sources`. Если этих папок или копий нет, ничего делать не нужно. Удаляй только каталог самого ChatGPT Limit Panel, предварительно проверив название плагина в его `.codex-plugin/plugin.json`; не удаляй общие каталоги маркетплейса или других плагинов. При необходимости отдельно удали скачанный ZIP там, куда ты его сохранил.

Вход в Codex и файлы авторизации хранятся отдельно. Для удаления этой панели удалять их не требуется.

## English

Use these steps to remove the panel completely. If you intend to update it or reinstall it with the same preferences, follow the [upgrade guide](UPGRADING.md) and keep the settings folder.

1. Stop the monitor: **right-click the panel or its tray icon → Stop panel**, or open its plugin control card and click **Stop**.
2. Remove the panel's launch task. Press **Win + R**, enter `taskschd.msc`, open **Task Scheduler Library**, and find `ChatGPTQuotaOverlay.Host.<SID>`, where `<SID>` is the user's identifier. Open its properties: the account must be yours, and **Actions** must run the installed panel's program (`ChatGPTLimitPanelLauncher.exe` or `QuotaMonitor.exe`) with the `--run-host` argument. Make sure it is not running and delete only that task. If no task exists, continue to the next step. If its owner or action does not match, leave it untouched and contact [Issues](https://github.com/MrBogomoll/chatgpt-limit-panel/issues).
3. In the desktop client, open **Plugins → installed → ChatGPT Limit Panel → Manage** and remove the plugin. Button names can vary slightly by client version.
4. To remove panel preferences, saved positions, logs and other local data, enter `%LOCALAPPDATA%` in File Explorer's address bar and delete `ChatGPTLimitPanel` if present. Older installations may also leave `ChatGPTQuotaOverlay`; delete it only if it belongs to this panel. Deleted preferences cannot be recovered.
5. To remove any remaining package copies, check `%USERPROFILE%\.codex\plugins\cache` and `%USERPROFILE%\.codex\plugins\local-sources`. If those directories or copies do not exist, no action is needed. Delete only the ChatGPT Limit Panel's own directory after checking the plugin name in its `.codex-plugin/plugin.json`; do not delete shared marketplace directories or other plugins. If desired, separately delete the downloaded ZIP from wherever you saved it.

Codex sign-in and authentication files are stored separately and do not need to be deleted to remove this panel.
