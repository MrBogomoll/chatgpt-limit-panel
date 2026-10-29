# Известные ограничения / Known issues

## 1.4.3 / 1.4.2

Совместимость с Windows 10 пока не подтверждена: этап и причина сообщённого сбоя ещё не установлены. Исследование продолжится отдельно.

Windows 10 compatibility is not yet confirmed. The failure stage and cause of the reported problem remain unknown and will be investigated separately.

## 1.4.1

При использовании Snap Layouts в Windows 11 боковая панель может оставаться видимой поверх полупрозрачного предпросмотра окна. Совместимость с Windows 10 исследуется: причина сообщения о неработающей панели пока неизвестна.

The sidebar may remain visible over the translucent Windows 11 Snap Layouts preview. Windows 10 compatibility is under investigation; the cause of the reported failure is not yet known.

## 1.4.0 — Русский

Эти наблюдения сохранены по сообщению пользователя от 28 октября 2026 года.
В сборке 1.4.0 они намеренно оставлены без изменений. Причины ещё не установлены;
работа над исправлениями начнётся после отдельного решения о следующем патче.

1. **Автоматический размер боковой панели.** При уменьшении окна ChatGPT полный
   вид переходит в компактный, но дальнейшее уменьшение не переключает его
   автоматически в ультракомпактный. На меньшем окне панель может скрыться.
   Ультракомпактный вид можно выбрать вручную через ПКМ → «Вид панели» или ЛКМ
   при переключении видов.
2. **Перетаскивание и Snap Layouts Windows 11.** Боковая панель может временно
   исчезать примерно на пару секунд при перемещении окна и затем появляться.
   Во время перетаскивания в верхнюю область выбора раскладки Windows 11 она
   может оставаться видимой поверх полупрозрачного предпросмотра, хотя основное
   окно уже не отображается обычным способом. Это относится к боковой панели,
   которая рисуется отдельным окном.

Совместимость с Windows 10 исследуется отдельно: есть сообщение о неработающей
панели, но точная причина пока неизвестна. См. [состояние проверки](WINDOWS-COMPATIBILITY.md).

## 1.4.0 — English

These observations were reported on October 28, 2026. They are intentionally
unchanged in build 1.4.0. Their causes have not been established, and fixes are
deferred until a separate decision about a following patch.

1. **Automatic sidebar sizing.** Shrinking the ChatGPT window changes full view
   to compact view, but further shrinking does not automatically select
   ultracompact. The panel may hide in a smaller window. Select ultracompact
   manually through right-click → Panel view or by cycling views with left-click.
2. **Dragging and Windows 11 Snap Layouts.** The sidebar panel can briefly
   disappear for approximately a couple of seconds during window movement and
   then return. When dragging into the Windows 11 top-edge layout picker, it can
   remain visible above the translucent preview while the main window no longer
   appears normally. This concerns the sidebar's independent overlay window.

Windows 10 compatibility remains a separate investigation. A nonworking panel
has been reported, but its precise cause is unknown. See the
[compatibility status](WINDOWS-COMPATIBILITY.md).
