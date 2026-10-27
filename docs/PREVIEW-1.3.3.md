# Quota Panel 1.3.3 — предварительная сборка

Уточнено распознавание активного меню Start11, из-за которого индикатор мог
исчезать через несколько секунд после ПКМ по панели задач.

Пока подходящее меню открыто, индикатор удерживает последнее проверенное место.
Проверки геометрии, масштаба и перекрытий продолжают работать. Такое удержание
разрешено только для уже показанного индикатора и не резервирует место в Windows.

Сохраняются ползунок прозрачности и настройки из 1.3.2. Поддержка модифицированных
панелей задач остаётся экспериментальной. Это приватная предварительная сборка.

## English

Detection of the active Start11 context menu has been refined to address the
indicator disappearing a few seconds after right-clicking the taskbar.

While a matching menu is open, the indicator retains its last verified position.
Geometry, DPI and overlap checks continue to run. Retention applies only to an
already visible indicator and does not reserve taskbar space in Windows.

The transparency slider and settings from 1.3.2 are preserved. Modified taskbar
support remains experimental. This is a private preview.
