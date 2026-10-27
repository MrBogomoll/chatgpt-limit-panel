# Quota Panel 1.3.1 — private preview

**Уточнение:** в окружении Start11 исчезновение осталось. Описанный ниже
механизм оказался недостаточным; 1.3.1 нельзя считать исправлением этого сбоя.
Дальнейшая диагностика — в [предварительной 1.3.2](PREVIEW-1.3.2.md).

**Correction:** disappearance persisted with Start11. The mechanism below was
insufficient; 1.3.1 must not be treated as a verified fix for that failure.

## Русский

Исправлена обработка меню модифицированной панели задач Start11. Его диалог
распознаётся по модулю Start11 внутри Проводника. Когда получение расположения
кнопок временно блокируется меню, уже показанный индикатор сохраняет своё место.
Он остаётся под меню и не получает фокус.

Во время удержания проверяются размеры и масштаб панели задач, её процесс,
нативные соседние элементы и изменения окон приложений. При изменении этих
условий индикатор возвращается к обычной проверке расположения. Этот механизм
не выделяет системное зарезервированное место и не гарантирует совместимость
с каждым сторонним виджетом или модификатором панели задач.

Ползунок прозрачности, выбранный процент, язык, тема, положение и автозапуск
сохраняются. Все возможности предварительной 1.3.0 остаются доступны.

## English

This preview fixes handling of the modified taskbar's Start11 menu. Its dialog
is identified through the Start11 module loaded by Explorer. If the menu
temporarily blocks accessibility geometry queries, an already visible indicator
keeps its existing position beneath the menu without taking focus.

Retention revalidates taskbar bounds, DPI, shell process, native neighboring
controls and application-window changes. Changes revoke retention and return
the indicator to normal placement checks. This does not reserve a system slot
or guarantee interoperability with every third-party taskbar widget or modifier.

The transparency slider, selected percentage, language, theme, position and
autostart preferences are preserved. The 1.3.0 preview features remain available.
