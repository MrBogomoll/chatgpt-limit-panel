# ChatGPT Limit Panel 1.4.0

## Русский

- Индикатор панели задач можно перетащить вдоль панели задач, а боковую панель —
  вверх или вниз. Положение сохраняется; размещение учитывает занятые элементы.
- Исправлен поиск свободного места на вертикальной панели задач при расположении
  «Пуска» сверху, включая избыточную область классической раскладки Start11.
- Снижена нагрузка при слежении за окном ChatGPT и получении координат элементов.
- Сохранены язык, тема, вид, индикаторы, прозрачность и настройки автозапуска.

**Известные ограничения:** автоматический переход из компактного вида в
ультракомпактный при дальнейшем уменьшении окна пока отсутствует. При
перетаскивании боковая панель может кратковременно скрываться или оставаться
поверх предпросмотра Snap Layouts Windows 11. Ультракомпактный вид доступен
вручную. Эти замечания отложены для возможного следующего патча.

Скачивайте готовый **chatgpt-limit-panel-1.4.0-windows-x64.zip** вместе с файлом
контрольной суммы. Для установки ZIP не нужны Git и отдельная установка Node.js.
Проверка обновлений открывает страницу выпуска; установка выполняется вручную.
См. [инструкцию обновления](../UPGRADING.md) и [известные ограничения](KNOWN-ISSUES.md).

Совместимость с Windows 10 пока не подтверждена; исследование продолжается отдельно.

## English

- Drag the taskbar indicator along the taskbar or move the sidebar panel up or
  down. Positions are saved and placement accounts for occupied controls.
- Fix free-space detection on vertical taskbars with Start at the top, including
  redundant classic Start11 layout occupancy.
- Reduce window-tracking and accessibility-geometry overhead.
- Preserve language, theme, view, indicators, transparency and autostart preferences.

**Known limitations:** compact view does not yet switch automatically to
ultracompact when the window is reduced further. During dragging, the sidebar
can briefly hide or remain above the Windows 11 Snap Layouts preview. Ultracompact
is available manually. These reports are deferred for a possible following patch.

Download **chatgpt-limit-panel-1.4.0-windows-x64.zip** and its checksum file. The
complete ZIP needs neither Git nor a separate Node.js installation. Update
checking opens the release page; installation remains manual. See the
[upgrade guide](../UPGRADING.md) and [known issues](KNOWN-ISSUES.md).

Windows 10 compatibility is not yet confirmed and remains a separate investigation.
