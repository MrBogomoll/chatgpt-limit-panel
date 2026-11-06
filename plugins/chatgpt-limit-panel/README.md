# ChatGPT Limit Panel 1.7.2

«Показывать сбросы» в меню панели и трея включает блок над лимитами. Полный вид: количество, дата и время; компактный: количество и дата; ультракомпактный: `↻ 3`. Выбор сохраняется во всех режимах.

ПКМ по панели или значку в трее → «Доступные сбросы» открывает подменю со всеми известными сроками истечения по порядку. Пункт «Содержимое панели → Показывать кредиты» включает остаток кредитов Codex между сбросами и лимитами во всех режимах. Баланс API источник не возвращает. В полном и компактном видах более светлая линия толщиной 2 px отделяет дополнительные сведения от основных лимитов; в ультракомпактном линия тонкая. До 10 дней включительно — оранжевый цвет, до 3 дней — красный. Просмотр не использует сброс.

Знак C обозначает кредиты в полном и компактном видах; в ультракомпактном остаётся только число. Индикатор процентов на панели задач можно перетаскивать; пункт «Сбросить положение» возвращает автоматическое расположение.

Панель оставшихся лимитов Codex / Work в настольном ChatGPT для Windows x64. Показывает проценты и серверные даты сброса в боковой полосе и, при включении, на панели задач Windows. В Instant отображаются те же лимиты.

Для установки открой **Плагины → Добавить → Загрузить архив плагина** и выбери этот ZIP. Затем открой ChatGPT Limit Panel в меню плагинов и нажми «Запустить» в карточке управления. ПКМ по панели открывает вид, индикаторы, тему, язык, автозапуск и настройки панели задач. ЛКМ переключает полный, компактный и ультракомпактный виды. При включённом индикаторе панели задач лимиты обновляются и с закрытым окном ChatGPT.

Нужны Windows x64, локальный исполнитель и установленный Codex с выполненным входом. API-ключ, Git и отдельная установка Node.js не требуются. Язык, тема и остальные настройки сохраняются.

[Страница проекта, установка и выпуски](https://github.com/MrBogomoll/chatgpt-limit-panel).

ПКМ по панели или значку в трее → «Расположение → Закрепить на боковой панели». Сними галочку для свободного перемещения ЛКМ внутри окна ChatGPT. После снятия закрепления панель остаётся у боковой полосы и перемещается только вручную. Положение в свободном режиме сохраняется при перезапуске; «Расположение по умолчанию» сбрасывает положение текущего режима.

ПКМ по самой панели → «Непрозрачность фона»: 100% даёт сплошной фон без просвечивания переписки. Настройка не меняет прозрачность индикатора панели задач.

## English

A Windows x64 panel for the shared Codex / Work limits in desktop ChatGPT. Shows remaining percentages and server reset times beside the app and optionally on the Windows taskbar. Instant uses the same counters.

To install, open **Plugins → Add → Upload plugin archive** and select this ZIP. Then open ChatGPT Limit Panel from plugins and click Start in its control card. Right-click for view, indicators, theme, language, autostart and taskbar settings. Left-click cycles full, compact and ultracompact. Enabled taskbar mode continues refreshing with the ChatGPT window closed.

Requires a Windows x64 local executor and installed Codex signed in to your account. No API key, Git or separate Node.js installation is needed. Preferences are preserved.

[Project, installation and releases](https://github.com/MrBogomoll/chatgpt-limit-panel).

Right-click the panel or tray icon → Placement → Pin to sidebar. Uncheck to drag the panel freely inside the ChatGPT window. Unpinning starts at the current sidebar location. Floating positions survive restart; Reset position resets the active placement mode.

Right-click the panel → Background opacity: 100% gives a solid background. The setting is independent of taskbar indicator transparency.

[Обновление / Updating](UPGRADING.md) · [Удаление / Uninstalling](UNINSTALL.md) · [MIT License](LICENSE)
