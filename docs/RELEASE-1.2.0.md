# Quota Panel 1.2.0

## Русский

- Добавлен дополнительный индикатор на основной панели задач Windows: остаток
  5-часового лимита сверху, недельного снизу. Включается отдельно; можно выбрать
  предпочтительное размещение слева или справа.
- При включённом индикаторе получение квоты продолжается с закрытым окном ChatGPT.
  Управлять панелью можно через значок Quota Panel в трее.
- В меню индикатора появился ползунок прозрачности фона. Настройка общая с основной
  панелью; текст остаётся непрозрачным. Даже полностью прозрачный фон принимает ПКМ.
- Исправлено исчезновение индикатора при открытии подходящего меню Start11 и ошибка
  определения возраста снимка расположения элементов панели задач.
- Подготовлена единая нумерация выпусков и сравнение установленных бета-версий.
  Проверка обновлений продолжает предлагать только стабильные публичные выпуски.

Для скачивания предназначен `quota-panel-1.2.0-windows-x64.zip` из Assets.
Установка обновления остаётся ручной. Git и вход в GitHub не требуются.

Поддержка изменённых панелей задач экспериментальная. При вызове «Пуска» поверх
игры без рамки индикатор может исчезать; замечено в Honkai: Star Rail и Hollow
Knight: Silksong. Этот случай остаётся известной проблемой для следующего патча.
Windows 10 не проверялась на отдельном компьютере. Исходники и публикация
стабильного архива управляются отдельно; подготовка этой сборки не публикует её.

## English

- Added an optional primary Windows taskbar indicator: five-hour remaining quota
  above weekly remaining quota, with preferred left or right placement.
- Enabled taskbar mode continues fetching quota with the ChatGPT window closed.
  Controls remain available through Quota Panel's notification-area icon.
- Added a background-transparency slider shared with the main panel. Text stays
  opaque, and the fully clear panel area continues accepting right-clicks.
- Corrected disappearance with the matching Start11 context menu and a race in
  taskbar snapshot-age calculation.
- Consolidated release numbering and added installed-prerelease comparisons.
  Update checks continue offering stable public releases only.

Download `quota-panel-1.2.0-windows-x64.zip` from Assets. Updates are installed
manually; Git and GitHub sign-in are not required.

Modified taskbar support remains experimental. Opening Start over a borderless
game can hide the indicator, observed with Honkai: Star Rail and Hollow Knight:
Silksong. This remains a known issue for a subsequent patch. Windows 10 has not
been tested on a separate machine. Preparing this build does not publish it.
