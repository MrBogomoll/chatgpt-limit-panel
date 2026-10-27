# Quota Panel 1.3.0 — private preview

## Русский

Добавлена экспериментальная поддержка основной панели задач у любой из четырёх
границ экрана: снизу, сверху, слева или справа. Индикатор сохраняет две строки:
процент короткого лимита сверху, недельного — снизу. В меню **«Панель задач»**
можно включить индикатор и выбрать сторону размещения вдоль панели: слева или
справа для горизонтальной, сверху или снизу для вертикальной.
Само положение панели задач Windows плагин не меняет.

ПКМ по индикатору на панели задач → **«Прозрачность фона»** открывает ползунок
0–100%. При 0% фон непрозрачный, при 100% полностью прозрачный. По умолчанию
выбрано 50%. Общая настройка сразу применяется к основной панели Quota Panel и
индикатору на панели задач и сохраняется после перезапуска. Она работает в
тёмной и светлой темах, включая автоматический выбор темы. Текст, проценты,
полоски, контур и меню сохраняют прежнюю непрозрачность.

В меню ПКМ **«Проверить новую версию»** относится к выпускам Quota Panel.
Команды **«Приостановить обновление лимитов»** и **«Возобновить обновление
лимитов»** управляют получением квоты.

Уточнена обработка системных меню и выделения области Windows: стандартное
окно ScreenClippingHost и меню больше не считаются обычными полноэкранными
приложениями или постоянно занятым местом панели задач. Индикатор сохраняет
порядок окон, чтобы оставаться под интерфейсом выделения и открытым меню.

Сборка остаётся приватной предварительной версией; публичная стабильная — 1.1.0.
Новая геометрия относится только к основной панели задач. При недостатке места
или неопределённом расположении элементов индикатор скрывается. Windows 10 ещё
требует проверки на реальном компьютере; поддержка всех положений не означает
проведённую проверку каждой конфигурации. Подробности исходного сравнения —
в [исследовании Windows 10/11](research/2026-10-27-windows-taskbar-research.md).

## English

This preview adds experimental support for the primary taskbar at any of the
four screen edges: bottom, top, left or right. The indicator keeps two rows:
short-window percentage above weekly percentage. The **Taskbar** menu enables
the indicator and selects its placement side along the taskbar: Left or Right
for a horizontal taskbar, Top or Bottom for a vertical one.
The plugin does not move the Windows taskbar itself.

Right-click the taskbar indicator → **Background transparency** for the 0–100%
slider. At 0% the background is opaque; at 100% it is fully transparent. The
default is 50%. This shared setting immediately changes the main Quota Panel and
taskbar indicator backgrounds and persists across restarts. It works in dark
and light themes, including automatic theme selection. Text, percentages, bars,
outlines and menus retain their previous opacity.

The context menu distinguishes **Check for a new version** from
**Pause quota refresh** and **Resume quota refresh**.

Windows region selection and native menus are handled as transient UI rather
than ordinary fullscreen apps or permanently occupied taskbar space. The
indicator keeps its Z order beneath the capture interface and open menus.

This is a private preview. The public stable version remains 1.1.0. The new
geometry applies only to the primary taskbar. The indicator hides when space is
insufficient or occupancy is uncertain. Windows 10 still needs validation on a
real computer; support for all positions does not establish acceptance of every
configuration. See the original [Windows 10/11 comparison](research/2026-10-27-windows-taskbar-research.md).
