# Quota Panel 1.2.0 — private preview

## Русский

В предварительной версии добавлен дополнительный индикатор на панели задач:
сверху процент оставшегося короткого лимита Codex/Work, снизу — недельного.
Включение: **ПКМ → Панель задач → Показывать проценты**. В том же меню можно
выбрать левую или правую сторону. Эти команды доступны и через значок Quota Panel
в трее, когда окно ChatGPT закрыто.

При включённом индикаторе квота обновляется независимо от окна ChatGPT. Пауза и
остановка сохраняют своё действие. Индикатор приглушает устаревшие значения и
показывает прочерк, если значение неизвестно. Проверка обновлений теперь учитывает
переименование GitHub-репозитория релизов. Уточнено отображение фактической
длительности короткого лимита и обработка некорректного времени сброса.

По умолчанию возможность выключена. Сейчас поддерживается основная горизонтальная
панель задач снизу. Индикатор занимает измеренное свободное место и скрывается,
если места недостаточно или расположение элементов неизвестно. Это отдельное
окно, поэтому общий системный слот и совместимость со всеми сторонними виджетами
не гарантируются. Проверка выполнена на Windows 11; Windows 10 ещё требует живой
проверки. Подробности — в [сравнении Windows 10/11](research/2026-10-27-windows-taskbar-research.md).

Это приватная предварительная сборка. Публичная стабильная версия остаётся 1.1.0.

## English

This preview adds an optional taskbar indicator: remaining short-window
Codex/Work percentage above the weekly percentage. Enable it through
**Right-click → Taskbar → Show percentages**, then choose **Left** or **Right**.
The Quota Panel tray icon provides the same controls when ChatGPT is closed.

With the indicator enabled, quota polling continues independently of the ChatGPT
window. Pause and Stop still apply. Stale values are dimmed; unknown values use a
dash. Update checks now tolerate renaming the GitHub release repository. The
preview also improves actual short-window duration labels and invalid reset-time
handling.

The feature defaults off. Initial support covers the primary horizontal bottom
taskbar. The indicator uses a measured free gap and hides when space is
insufficient or occupancy is uncertain. It is a separate window, so a reserved
shell slot and compatibility with every third-party widget are not guaranteed.
Desktop checks were performed on Windows 11; Windows 10 still needs live
validation. See the [Windows 10/11 comparison](research/2026-10-27-windows-taskbar-research.md).

This is a private preview. The public stable version remains 1.1.0.
