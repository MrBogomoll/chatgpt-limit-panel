# Обновление ChatGPT Limit Panel / Updating ChatGPT Limit Panel

## Русский

1. Останови установленную панель через её меню или карточку управления.
2. Перед переносом между версиями открой Планировщик заданий Windows. Найди собственную задачу с именем, начинающимся с `ChatGPTQuotaOverlay.Host.`. Проверь владельца и действие: путь должен вести к программе именно твоей установленной панели внутри каталога плагина. Удали только эту подтверждённую остановленную задачу. При несовпадении владельца или пути оставь задачу и обратись в Issues.
3. Загрузи готовый ZIP 1.3.0 через меню плагинов и открой ChatGPT Limit Panel.
4. Нажми «Запустить». Будет создана задача для новой программы, а язык, тема, вид, индикаторы и прозрачность сохранятся.

Для первой установки используй [README](README.md). Проверка новой версии открывает страницу выпуска; перенос выполняется вручную.

## English

1. Stop the installed panel through its menu or control card.
2. Before migrating versions, open Windows Task Scheduler and find your own task whose name starts with `ChatGPTQuotaOverlay.Host.`. Verify the owner and action: the executable must belong to your installed panel inside its plugin directory. Remove only that verified stopped task. Leave it unchanged and contact Issues if the owner or path does not match.
3. Import the built 1.3.0 ZIP through the plugin menu and open ChatGPT Limit Panel.
4. Click Start. The new program receives its own task while language, theme, view, indicators and transparency are preserved.

For first installation, follow the [README](README.en.md). Update checking opens the release page; migration is manual.
