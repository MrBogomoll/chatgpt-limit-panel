# Quota Panel 1.2.1-beta.3

Локальная тестовая сборка **1.2.1-beta.3**: дочернее прикрепление индикатора к панели задач по образцу NetWorx. Порядок и видимость теперь следуют родительской панели; реальное поведение в играх и меню проходит проверку. Готовый архив 1.2.0 сохранён.

Панель общего остатка лимитов Codex / Work для Windows x64. Работает поверх настольного ChatGPT отдельным окном. Включаемый отдельно индикатор на основной панели задач показывает процент 5-часового лимита сверху и недельного снизу. В Instant отображается тот же общий счётчик Codex / Work.

## Управление

- Открой Quota Panel в меню плагинов: карточка позволяет запустить, остановить монитор, обновить состояние и проверить новую версию. Закрытие карточки не останавливает монитор.
- ЛКМ по обычной панели переключает полный, компактный и ультракомпактный виды. ПКМ открывает выбор вида, языка, темы, графики, лимита контура и автозапуска.
- ПКМ → «Панель задач» → «Показывать проценты», затем «Слева» или «Справа». По умолчанию выключен. Положение выбирается среди измеренных свободных промежутков.
- ПКМ по индикатору → «Прозрачность фона»: общий ползунок 0–100% для обоих отображений. Текст остаётся непрозрачным, полностью прозрачный фон продолжает принимать ПКМ.
- Тема: тёмная, светлая или авто. Язык: русский или English. Настройки сохраняются и не меняют интерфейс ChatGPT.
- При включённом индикаторе панели задач получение квоты продолжается с закрытым окном ChatGPT. Управление доступно из значка Quota Panel в трее.
- «Проверить новую версию» проверяет стабильные публичные выпуски. «Приостановить обновление лимитов» управляет получением квоты. Установка обновлений остаётся ручной.

## Изменения и ограничения

В 1.2.0 объединены новые возможности панели задач, ползунок прозрачности, исправление обработки активного меню Start11 и определения возраста снимка расположения кнопок.

Изменённые панели задач и размещение у четырёх границ экрана поддерживаются экспериментально; место в Windows не резервируется. При нехватке места или неопределённой геометрии индикатор скрывается. Windows 10 не проверялась на отдельном компьютере.

Известная проблема: при вызове «Пуска» поверх игры без рамки индикатор панели задач может исчезать. Замечено в Honkai: Star Rail и Hollow Knight: Silksong; исправление запланировано отдельно.

Прежние 1.2.x/1.3.x были закрытыми промежуточными сборками. Их установку переводят на итоговую 1.2.0 явно. Обычная проверка версий не считает настоящую 1.3.3 старее 1.2.0. Последующие предварительные сборки используют beta.N; поиск публичных обновлений остаётся стабильным.

[Страница проекта и выпуски](https://github.com/MrBogomoll/quota-panel-chatgpt). Установка из архива выполняется через загрузку плагина в поддерживаемом настольном клиенте. Требуется локальный исполнитель Windows x64 и настроенная авторизация Codex; Git и вход в GitHub для работы не требуются.

## English

Quota Panel displays the shared Codex / Work quota in Windows x64. The main panel is a separate overlay beside desktop ChatGPT. An optional primary-taskbar display shows five-hour remaining quota above weekly remaining quota. Instant shows the same Codex / Work counters.

Open Quota Panel in the plugin menu for Start, Stop, Refresh status and Check for updates. Closing the card keeps the monitor running. Left-click the panel to cycle full, compact and ultracompact views; right-click for language, theme, indicators, outline source and autostart settings.

Enable Taskbar → Show percentages, then choose Left or Right. The indicator defaults off and uses measured free gaps. Its right-click Background transparency slider applies 0–100% to both backgrounds while keeping text opaque and preserving mouse input with a fully clear background. Enabled taskbar mode keeps quota polling active with ChatGPT closed; controls remain available in the notification area.

Version 1.2.0 includes the taskbar display, transparency slider, matching Start11 menu handling and a taskbar snapshot-age correction. Modified taskbars and four-edge placement remain experimental. The indicator does not reserve Windows taskbar space and hides when geometry or free space cannot be established. Windows 10 has not been tested on a separate machine.

Known issue: opening Start over a borderless game can hide the taskbar indicator, observed in Honkai: Star Rail and Hollow Knight: Silksong. A separate fix is planned.

Earlier private 1.2.x/1.3.x previews are consolidated into 1.2.0 through an explicit migration. Genuine version precedence is unchanged. Future prereleases use beta.N; public update checks offer stable releases only. Installation stays manual.

[Project and releases](https://github.com/MrBogomoll/quota-panel-chatgpt). Import the plugin archive in a supported desktop client. Windows x64 local execution and configured Codex authentication are required; Git and GitHub sign-in are not required to use the panel.
