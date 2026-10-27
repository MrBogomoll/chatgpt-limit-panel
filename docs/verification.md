# Проверка приватной беты

Дата: 25 октября 2026. Версия: 0.2.0-beta.3. Среда: Windows x64, Node.js 24.22.1, .NET Framework 4.x, Microsoft Edge.

Из подготовленной для GitHub папки исходников выполнен полный набор относящихся к действующему плагину тестов: **106 прошли, 0 ошибок, 0 пропусков**. Включены чтение и нормализация квоты, MCP-карточка управления, упаковка и установка, жизненный цикл, автозапуск, настройки и собственные окна WPF вне экрана.

Сборка QuotaOverlay.exe и QuotaMonitor.exe из этой папки завершилась успешно. Отдельная проверка геометрии охватила полный и компактный виды, DPI, границы окна и отступ для кнопки обновления.

В исходниках, подготовленных для загрузки, не обнаружены авторские абсолютные пути, данные входа или ключи по проверенным шаблонам. В Git не включены рабочие настройки, журналы, кэш авторизации и собранные исполняемые файлы. Лицензия Node входит в готовый пакет и сохранена в third-party.

Готовая Windows-сборка передана отдельным архивом:

```text
quota-panel-0.2.0-beta.3-windows-x64.zip
35 159 130 bytes
SHA-256: 6f812c99975e01be84c150162e538b091bd596f1348a83832b5af36f8b48d8a1
```

Архив соответствует установленной и отдельно проверенной сборке. В живой Windows проверены принадлежность пользовательской задачи, её триггер входа, переключение автозапуска и запуск установленного монитора. Реальная перезагрузка Windows не выполнялась. Автоматические проверки собственных окон не заменяют проверку всех физических мониторов, DPI и будущих версий ChatGPT.

Миграция задачи при смене версии/пути установленного плагина и автоматическая очистка задачи при удалении остаются пунктами доработки перед публичным релизом.

## Билингвальная сборка для проверки

25 октября 2026 добавлены русский и английский интерфейсы с единым сохраняемым выбором языка. В основной рабочей папке прошли 122/122 теста; из чистой папки исходников GitHub — 110/110 относящихся к действующему плагину тестов. Разница — старые проверки прежних экспериментов, не включённых в репозиторий.

Дополнительно проверены английские подписи и графика в настоящем собственном окне вне экрана: 112 сочетаний вида, индикатора и масштаба. Изображения полного, компактного и ультракомпактного вида просмотрены отдельно. Карточка управления проверена в браузере: английские кнопки, сообщения, ошибка соединения и возврат на русский.

Установленная копия прошла проверку запуска/остановки, повторного запуска без дубликата, получения настоящей квоты и работы после закрытия MCP-транспорта. Все 21 файл установленного пакета совпадают с подготовленной сборкой. Язык установленной копии оставлен русским, автозапуск включён. Реальный перезапуск Windows по-прежнему не выполнялся.

Новый архив находится в отдельной приватной предварительной сборке с языками; предыдущий архив сохранён:

```text
quota-panel-0.2.0-beta.3-windows-x64.zip
SHA-256: 89369a96b207f9debe49575aad9df7f88dd08add2ac0c08c9f27cd8644265b32
```

Внутренняя версия оставлена 0.2.0-beta.3. Версия 1.0 ожидает проверки и решения пользователя.

## Обновление карточки после установки перевода

Исправлен прежний постоянный адрес UI control-v1.html. Новые карточки используют адрес, полученный из содержимого HTML и JavaScript: изменение любого файла меняет адрес и позволяет обойти старую кэшированную разметку. Один запущенный сервер держит неизменный снимок своего UI, чтобы адрес и содержимое не расходились при обновлении файлов. Старый адрес сохранён как совместимый запрос для прежних сохранённых разговоров и отдаёт текущий интерфейс.

Проверки затронутых компонентов: 21/21. Отдельно проверен установленный MCP-сервер с настоящим нативным статусом: выбран English, монитор запущен, новый адрес ресурса получен при обнаружении инструментов. Загруженная из этого сервера карточка проверена в изолированном браузерном окружении: кнопки, статус и пояснение полностью на английском. Это проверка установленного сервера и браузерной карточки; уже открытый документ внутри ChatGPT автоматически не заменялся.

Готовый архив исправления карточки:

```text
SHA-256: 096015d8b9cce7859504674dfa9b3cec8dfa8978b332232b83017a4b1a7b2470
```

## Версия 1.0.0 — 25 октября 2026

Пользователь одобрил переход на 1.0. Во всех манифестах, MCP, карточке и сборщике установлена версия 1.0.0. Добавлен PNG утверждённой иконки 512×512. Из папки исходников релиза прошли 111/111 тестов, без ошибок и пропусков; EXE собраны успешно.

На текущем компьютере выполнена проверенная миграция принадлежащей Quota Panel пользовательской задачи со старого пути беты на путь cache/quota-panel-local/quota-panel/1.0.0. Определение прежней задачи и прежний исходный пакет сохранены вне Git. Первая попытка зарегистрировать тот же marketplace по новому пути была отклонена CLI; прежний монитор восстановлен, затем обновление выполнено через уже зарегистрированный источник. Другие marketplace и задачи не изменялись.

Все 22 файла установленного пакета совпадают с подготовленным пакетом. Язык English и включённый автозапуск сохранены. Живая проверка 1.0 подтвердила запуск/остановку без дубликатов, получение настоящей квоты, сохранение процесса после закрытия MCP-транспорта и правильный адрес карточки. Карточка проверена с установленным сервером в отдельном браузерном окружении: кнопки, статус и пояснение на английском.

```text
quota-panel-1.0.0-windows-x64.zip
SHA-256: d2a3d1831f9d61dc7c096cef18724d21ac9daf3e20bcbff99ac8bd30c5a6bed6
```

Это версия для ручной установки; готовность публичной заявки в каталог не заявляется. Нужны допуск организации для локального MCP и материалы из PUBLISHING-RU.md. Реальная перезагрузка Windows, чистая установка на другом компьютере и пользовательские сценарии портала в этом прогоне не выполнялись.

## Версия 1.0.1 — видимость и перемещение окна

25 октября 2026 из подготовленной приватной папки исходников прошли **116/116
проверок**, без ошибок и пропусков. EXE собраны из тех же исходников. Проверены
сохранение панели при потере фокуса, перекрытие другими приложениями, переход
между normal/topmost, сворачивание/возврат, пауза и отсутствие изменения фокуса.

Тест перемещения воспроизвёл прежний дефект на первом шаге: отложенный callback
не переносил панель сразу за окном. Исправление прошло 40 непосредственных
перемещений, пропуск события с подхватом таймером и поступление новой квоты во
время движения без записи PNG/состояния/стартовой диагностики. Отложенные записи
после завершения содержат последние данные. Отдельный процесс с собственным
окном вне экрана проверил 20 настоящих внешних WinEvent, включая флаг пропуска
своего процесса. В итоговом полном прогоне максимальное наблюдаемое время от
подтверждения тестового перемещения до совпадения координат составило 21,1 мс;
это измерение тестовой среды, а не гарантия для любого рабочего стола.

Все 22 файла установленного пакета совпали с выпуском. Использован прежний
зарегистрированный локальный источник и штатная перезагрузка плагина через CLI;
существующая задача 1.0.1, English, автоматическая тема и автозапуск сохранены.
Проверка установленного сервера подтвердила запуск/остановку без дубликатов,
настоящую квоту и продолжение работы после закрытия MCP-транспорта. Пользователь
отдельно подтвердил сохранение панели при щелчке по рабочему столу и нормальное
следование за быстро перетаскиваемым окном ChatGPT.

```text
quota-panel-1.0.1-windows-x64.zip
SHA-256: 37213f2eb724c4145fc68615ac1558b3f0430d1fcf22f7ef3f708a763b4b2e7f
```

Исходники остаются приватными. Публичный репозиторий содержит только готовые
сборки, изображения и инструкции обновления. Проверка реальной перезагрузки
Windows и чистой установки на другом компьютере в этот прогон не входила.

## Версия 1.1.0 — проверка обновлений

25 октября 2026: итоговый прогон из приватной папки исходников — 126/126,
без ошибок и пропусков. Тесты охватывают числовое сравнение версий, stable-only
GitHub ответ и Windows asset, отклонение внешних ссылок, ошибки/тайм-аут,
объединение повторных запросов, отмену при остановке хоста, независимость от
запуска монитора и квоты. Карточка проверена в Edge: оба языка, темы и узкая
ширина, сохранение результата при обновлении статуса, явное ui/open-link и отказ
хоста при открытии. Изображения светлого и тёмного интерфейса просмотрены.

Исправлен найденный при независимом обзоре риск перехвата фокуса: окно результата
показывается с ShowActivated=false. Тест сначала воспроизвёл прежнее поведение,
затем проверил реальный production Show собственного окна вне экрана и сохранение
фокуса. Проверки исходных квот, отображения, движения, настроек и автозапуска
сохранились. Собраны оба EXE и пакет из 24 файлов.

Установленная копия сверена с пакетом по всем файлам. Собственная задача Windows
перенесена с 1.0.1 на 1.1.0 с проверкой принадлежности и резервной копией. English,
тема Auto, полный вид, оба индикатора и включённый автозапуск сохранены. Проверка
установленного MCP подтвердила 5 инструментов, новую карточку и настоящий ответ
GitHub: до публикации 1.1.0 корректно определена как ahead относительно 1.0.1.
Запуск/остановка, настоящая квота и продолжение работы после закрытия транспорта
проверены отдельно. Реальный рестарт Windows и другой компьютер не проверялись.

```text
quota-panel-1.1.0-windows-x64.zip
SHA-256: b275caf8059ef579d2ff842011f84618b5f9f8733a4b3191c8a874a08abea5ca
```

После публикации v1.1.0 установленный MCP повторно запросил GitHub: state=current,
currentVersion=latestVersion=1.1.0, без ссылки на понижение или изменения состояния
монитора. Публичное скачивание без авторизации совпало с контрольной суммой.
Публичные пояснения выпуска не содержат числа тестов или пользовательских подтверждений.

## Приватная предварительная 1.2.0 — индикатор панели задач, 27 октября 2026

Это предварительная сборка ветки codex/taskbar-indicator. Публичный стабильный
выпуск остаётся 1.1.0; совместимость экспериментального индикатора со всеми
конфигурациями Windows не заявляется.

Реализован отдельный выключенный по умолчанию индикатор: две строки процентов,
короткий лимит сверху и недельный снизу, выбор левой или правой половины основной
горизонтальной панели задач снизу. Управление доступно в RU/EN через обычную
панель и значок в трее. При включённом режиме host продолжает чтение квоты без
видимого окна ChatGPT, сохраняя явную паузу, остановку и отключение режима.
Неизвестные значения не подставляются; устаревшие известные данные приглушены.

Индикатор использует собственное окно без активации в измеренном свободном
промежутке. UIA и нативная геометрия читаются без изменения чужих окон или
настроек Проводника. Неизвестная занятость, недостаток места, неподдерживаемая
геометрия, скрытая панель задач и полноэкранное приложение приводят к скрытию
индикатора. Это не зарезервированный системный слот и не гарантия совместимости
с любым сторонним виджетом. Сравнение Windows 10/11, NetWorx и исходников аналогов
с первичными ссылками записано в docs/research.

После возобновления в 09:15 выполнены свежая сборка обоих EXE и полный набор
затронутых проверок: **139/139**, без ошибок и пропусков. Отдельный обзор не нашёл
существенных проблем в привязке проверки обновлений к неизменному ID репозитория,
проверке канонических URL и явном открытии релиза. Старый адрес latest после
переименования репозитория возвращал redirect, который прежний checker правильно
отклонял; новый endpoint по ID 1433486830 устраняет эту конкретную несовместимость,
сохраняя запрет перехода к другому владельцу.

Окончательный пакет:

```text
quota-panel-1.2.0-windows-x64.zip
SHA-256: 3539316a353713836f10e51ad148e3fcbaeec075c3b159dbd13d4f38ae52497b
```

Сборка обновлена через прежний зарегистрированный локальный источник и штатный
CLI. Все 24 файла установленной копии совпали с подготовленным пакетом. Повторная
миграция задачи Windows не выполнялась: переход на путь 1.2.0 уже был завершён
ранее. English, Auto, полный вид, оба индикатора и включённый автозапуск сохранены;
проверка нативного launcher подтвердила running=true и настроенную включённую
задачу. Индикатор панели задач оставлен выключенным по умолчанию.

При обновлении той же версии первоначально обнаружена блокировка кеша старым
собственным MCP-сервером 1.1.0. CLI при такой операции переименовывает родительское
дерево плагина, поэтому недостаточно освободить только процессы 1.2.0. После
штатной остановки монитора закрыт только сервер с проверенным собственным путём
и командой server/index.mjs; процессам ChatGPT и Проводника, правам доступа и
файлам установленного кеша напрямую изменения не вносились. Обычная повторная
установка после освобождения собственного процесса прошла успешно, монитор
восстановлен.

Проверка установленного MCP подтвердила версию 1.2.0, пять инструментов и новую
карточку. Настоящий запрос GitHub вернул state=ahead,
currentVersion=1.2.0/latestVersion=1.1.0; язык, работа монитора и автозапуск после
проверки не изменились. Источник кода проверен как приватный репозиторий
MrBogomoll/quota-panel-code (id1433242334), публичные релизы —
MrBogomoll/quota-panel-chatgpt (id1433486830).

Фактическая проверка рабочего стола выполнена только на Windows 11
build 26300.9711/26H2, 150%: прочитана реальная геометрия основной панели задач с
NetWorx; собственный индикатор с настоящей квотой кратко показан в левой и правой
свободных областях без изменения фокуса. Проверки нативных переходов, DPI,
неопределённой занятости, свежести геометрии и меню используют собственные окна
тестового процесса, размещённые вне экрана.

На Windows 10 и другом компьютере проверка не проводилась. Реальные перезагрузка
и вход Windows, перезапуск Проводника, сон/пробуждение, физическая смена монитора
или DPI, все переходы автоскрытия/полноэкранного режима и исчерпывающее
взаимодействие с другими виджетами в этот прогон не входили. Тесты хоста и
геометрии не заменяют эти проверки. Индикатор продолжает оставаться приватной
предварительной возможностью.

## Версия 1.2.1 — лёгкая прозрачность фона

27 октября 2026: по запросу автора фон основной панели и индикатора панели задач
получил alpha E6 (230/255, около 90% непрозрачности). Изменены только Surface
палитры светлой и тёмной тем; родительская Opacity, текст, графика и MenuSurface
не менялись. Версия синхронизирована в манифестах, MCP, карточке и упаковщике.

Оба нативных исполняемых файла собраны заново. Целевой набор проверок охватил
нативные темы/режимы, taskbar, упаковку/установку, MCP, карточку и обновления.
В первом прогоне 70 проверок прошли, одна остановилась на старом регулярном
выражении имени stage 1.2.0. После исправления ожидания полный файл проверок
упаковки прошёл 7/7. Повторный прогон незатронутых частей не выполнялся.

Локальный зарегистрированный источник обновлён штатным Codex CLI. С проверкой
принадлежности и резервной копией перенесена только собственная задача с 1.2.0
на 1.2.1; язык English, работа монитора и включённый автозапуск сохранены.
Все 24 файла stage, источника и установленного пакета совпали по SHA-256.
Чтение собственного установленного EXE через reflection подтвердило поверхности
#E6242526/#E6F3F3F4 и прежние непрозрачные Primary/MenuSurface.

Установленный MCP сообщает 1.2.1 и пять инструментов. Настоящая проверка GitHub
возвращает ahead относительно публичной стабильной 1.1.0, без изменения состояния
монитора. Публичная публикация не выполнялась; ограничения проверки Windows 10
и сценариев рабочего стола из предварительной 1.2.0 сохраняются.

```text
quota-panel-1.2.1-windows-x64.zip
SHA-256: 9611e154ac377a2e750ca78904a78b0d43de4a6b914d067aa00590857214cda9
```

## Версия 1.3.0 — прозрачность, четыре стороны и системные меню

Добавлен общий сохраняемый ползунок прозрачности фона 0–100% в ПКМ индикатора
панели задач, значение по умолчанию 50%. Текст, рамка, полоски и меню не получают
прозрачность родительского окна. Проверки используют настоящее событие Slider
ValueChanged, обе палитры, перезапуск, границы диапазона и повреждённые настройки.
Геометрия теперь поддерживает четыре полных края основной панели задач; выбор
стороны на вертикальной панели отображается как верх/низ. Основная и трей-панели
получили отдельные ясные названия проверки версии и паузы получения квоты.

Первый свежий общий прогон 1.3.0: 142/142. После первой установленной сборки
проверка на рабочем столе выявила сохраняющееся исчезновение при ПКМ. Трасса
события зафиксировала Explorer Xaml_WindowedPopupClass, foreground Shell_TrayWnd,
Fullscreen=false и потерю доступности общего UIA-списка: защищённых прямоугольников
становилось 9 вместо 37. Это не было доказательством полноэкранного приложения.

Исправление использует свежие UIA-запросы непосредственно к конкретным нативным
поддеревьям. При недоступном поддереве защищается весь его свежий нативный
прямоугольник; старый снимок не продлевается. Валидные прямоугольники временно
помеченных offscreen элементов по-прежнему защищены. XAML popup признаётся меню
только при совпадении PID с текущим Shell_TrayWnd; меню обнаруживаются до
возможного раннего выхода из UIA. Сохранены срок снимка, общий лимит элементов,
один фоновый сканер и закрытое поведение при неизвестной геометрии.

Windows ScreenClippingHost распознаётся по точному защищённому системному пути.
Порядок окон сохраняется при меню и захвате. Исправлена найденная при обзоре
регрессия: предыдущая подтверждённая полноэкранная блокировка сохраняется при
transient UI, поэтому скриншот поверх fullscreen не показывает ранее скрытый
индикатор. Неизвестное начальное состояние и другой монитор остаются закрытыми.

Новые проверки: 92 утверждения геометрии четырёх сторон, 67 политики transient,
131 в fixture окна/занятости, включая независимый UIA provider и защиту
нечитаемого поддерева. В общем прогоне после последнего исправления остальные
проверки прошли; сценарий быстрого перемещения поймал смену foreground во время
работы за компьютером. Немедленный отдельный повтор motion прошёл 2/2. Ошибок
сборки, проверок меню, прозрачности и источников квоты не осталось.

Финальный пакет установлен повторной загрузкой той же 1.3.0 через прежний
зарегистрированный источник и CLI; все 24 хеша stage/source/cache совпали.
Собственная задача, язык English, автоматическая тема, полный вид, правое
положение и включённый индикатор сохранены. Выбранные автором 29% прозрачности
сохранены. Настоящая проверка GitHub отвечает ahead относительно stable 1.1.0,
не изменяя состояние монитора. Read-only проба после завершения Computer Use
видит существующий индикатор и оба допустимых промежутка.

```text
quota-panel-1.3.0-windows-x64.zip
SHA-256: c43161b21ed3d2c61af14b379315a14ee2e82e6d0dbff9f5e99bba122f16e4c4
```

Повторная проверка ПКМ 1.3.0 выявила исчезновение примерно через две секунды;
это продолжено в исправлении 1.3.1 ниже. Фактическая проверка
Windows 10, перезапуска Windows/Explorer, сна и всех положений Start11 на этом
компьютере не заявляется. Публичный стабильный выпуск не обновлялся.

## Версия 1.3.1 — модальное меню Start11

Уточнение конфигурации: панель задач модифицирована Start11. Неблокирующая
трасса собственного TaskbarSnapshotSource показала foreground #32770, работающий
UIA worker и возраст последнего известного снимка до 17 353 мс. После 1600 мс
источник возвращал null и реальный HWND индикатора скрывался. После закрытия
диалога запросы возобновлялись. Срок кеша 1600 мс при исправлении не увеличен.

В живом Explorer найден captionless popup #32770 с owner=0 и GWLP_HINSTANCE,
совпадающим с BaseAddress защищённого Stardock/Start11/Start10_64.dll. По имени
процесса или общему классу #32770 исключение не выдаётся. Проверяются стиль,
положение у панели задач, текущий PID shell и точный путь/база модуля; штатные
диалоги, чужие виджеты и поддельные пути остаются исключёнными.

Добавлено отдельное удержание только уже видимого проверенного прямоугольника.
Оно не продлевает UIA-снимок и не ищет новый промежуток. Каждый тик проверяет
живые HWND/PID/bounds/DPI, раскрытое состояние, fullscreen, нативных соседей,
перекрывающие окна и хеш набора окон приложений с их заголовками и геометрией.
Заголовки не записываются в диагностику удержания. Любое расхождение скрывает
виджет; ранее скрытый виджет этим механизмом не восстанавливается. После меню
есть ограниченное окно восстановления 1600 мс с теми же нативными проверками.

Нативный отпечаток остаётся консервативной эвристикой: он не доказывает каждую
возможную перестановку виртуальных WinUI-кнопок внутри неизменного HWND.
Зарезервированный системный слот и универсальная совместимость не заявляются.

Проверки: отдельный автомат политики — 75 утверждений; распознавание меню — 106;
настоящее собственное WPF-окно с зависшим worker, возрастом 1700/15000 мс,
неизменным Configure, Native NOZORDER и отзывом удержания — 147. Найденное при
обзоре продолжение отозванного удержания через ещё действующий кеш сначала
воспроизведено (RED), затем исправлено (GREEN). Итоговый общий прогон — 143/143;
после последнего уточнения поиска вспомогательного диалога — 3/3 целевых файлов.

Readonly-проба распознала настоящий скрытый диалог Start11 и получила стабильный
ненулевой нативный отпечаток для реальной позиции справа. Никакие DLL не
внедрялись и настройки Start11/Проводника не менялись.

Установлена 1.3.1 через прежний зарегистрированный источник и CLI. Собственная
задача перенесена с 1.3.0 с проверкой принадлежности и резервной копией. Все 24
хеша stage/source/cache совпали. Сохранены работа монитора, English, Auto/dark,
полный вид, правое положение, включённый taskbar и выбранные 29% прозрачности.
Настоящая проверка GitHub — ahead 1.3.1 против публичной 1.1.0, состояние не меняет.

```text
quota-panel-1.3.1-windows-x64.zip
SHA-256: 4018c0a8f8cddc2bd425688dad34b5e39b8818a856d08ec58ba7fea5e5b80691
```

Живой повтор Start11 после установки 1.3.1 пока ожидает ответа. Границы проверки
Windows 10, перезагрузки, сна, нескольких мониторов и всех положений сохраняются.

## 1.3.2 private preview — appearance, measurement receipts and actual GUI diagnostics

The Start11 taskbar-menu disappearance reported against 1.3.1 remains unverified.
The real installed GUI now logs transition-only metadata to taskbar-debug.jsonl;
no app captions, content or tokens are written. The bounded spool performs native
metadata collection and file IO off the UI thread (8 pending + 1 inflight,
200ms minimum interval, 1MB file and one previous file). Event and worker times
are distinct. Injected taskbar fixtures do not log to user AppData.

Modern slider: 4DIP rounded track, 14DIP visual thumb, 22DIP pointer target,
live dark/light resources. Alpha1/255 input background at transparency100 keeps
empty layered-window pixels clickable; visual panel alpha contract and opaque
text are unchanged. Own tests check input pixels, HTCLIENT, native styles,
focus preservation, value changes, endpoints and production persistence.

A separate race was reproduced before fixing: captured A age450ms incorrectly
returned1601 after B publication. Production now captures immutable measurement
receipts atomically and computes age independently of newer publication. Explicit
invalidation/dispose, true expiry, reverse clock and object reuse remain guarded.
A behavioral RED/GREEN was recorded; this is not yet established as Start11 cause.

Final combined suite145/145 passed; taskbar fixture163 assertions, purelease75,
appearance32, diagnosticspool22. Native build passed. Version staging assertion
initially retained1.3.1 in an escaped regex; corrected and whole suite rerun.

Installed through registered source + CLI, owned task1.3.1→1.3.2 migrated with XML
backup dist/v132-upgrade-backup. All24 stage/source/cache payload hashes matched.
Actual GUI startup62932 showed exactly the prior GUI preferences: Russian,
Auto/dark, full, both indicators, shortcontour, taskbar-on/right,29%transparency,
autostart-on, pausedfalse. Taskbar visible at1833,1380/72x48px, aboveShell_TrayWnd.
MCP5tools reports1.3.2; realGitHub checkahead vs publicstable1.1.0 changed no lifecycle.

Important diagnostic caveat: packaged shell/launcher AppData reads differ from
scheduledGUI AppData for files shadowed under the ChatGPT package LocalCache.
Shell settings.json85bytes saysEnglish/compact, while actualGUI startupprefs are
Russian/full/right/29%. The packageLocalCacheLocal settings has the same85bytes.
Do not infer actualGUI language/visible/paused from the stale shell-side state.
Compare actualGUI startup or fresh nonshadowed diagnostic metadata. No migration
of those storage layers was attempted in this patch; this is a follow-up concern.

Package: quota-panel-1.3.2-windows-x64.zip
SHA256:25b792ef05dee29a1fd6bdb3d174c002b605f58a50a8b2252573462370787772
Public stable remains1.1.0. No public publication or universal Win10/11/Start11
compatibility claim. One real Start11 repro requested asynchronously remains
pending; at last inspection the log contains startup/visible transitions only.
## 1.3.3 private preview — actual Start11 menu activation

The requested real GUI repro finally captured the exact failing branch in1.3.2.
At13:25:50Z actualGUI62932 concealed the widget as unavailable when the UIA
source worker remained busy and its last measurement reached1613ms. Transient
was false. Native widget visibility was false; this was explicitHide, not Z-order
occlusion. A second opening at13:26:51Z repeated the same signature and1653ms.

Foreground menu was ExplorerPID17040, thread17224 identical to Shell_TrayWnd,
class#32770, owner0, HINSTANCE0, style96000001, ex00080088. Bounds were354x372px,
12px above the bar; the previous hidden Start11 instance-proven dialog was a
different window. The actual popup was destroyed after closing. Readonly checks
verified the protected Start10_64.dll loaded in the same liveExplorer. Existing
hidden controls distinguish genuine nullHINSTANCE/error0 from WndProc/DlgProc
access-denied5; they are not substituted for the actual popup's identity.

New fallback activates only the existing fixed-footprint lease through IsActive.
It does not alter IsMenuWindow, fullscreen exceptions or collision/occupancy
exemptions. Required: actual visible/uncloaked foreground, genuine zero instance,
unowned #32770, observed toolpopup style bits, same live shellPID and UIthread,
valid nearbar geometry entirely outside the bar, protected loadedStart11module.
Loaded-only cache decisions expire within1sec with live held process handles.
Foreground, shell handle, PID and thread are rechecked after module enumeration.
Module presence is constrained observational evidence, not per-HWNDcreatorproof.
Native footprint/fingerprint checks still make the final retention decision.

Targeted policy RED/GREEN recorded; finalpolicy140assertions. Fullnativebuild and
combinedsuite145/145 passed. Independent read-only review found no blocker.
TaskbarDiagnostics additionally records instanceReadError to distinguish genuine
zero values from native read errors on the next real opening.

Installed through registered source and CLI with ownedtask1.3.2→1.3.3 migration,
backup dist/v133-upgrade-backup. All24stage/source/cache payload hashes match.
ActualGUI59288 startup is visible and abovebar. LanguageRU, Auto/dark, fullmode,
bothindicators, shortcontour, taskbar-on/right, autostarton and pausedfalse match.
Transparency now56 versus the old1.3.2 first30updates snapshot29; that oldstartup
is not a fresh preference baseline. No migration/code path writes transparency;
newGUI reads the persisted current value. Do not restore29 from that stalefile.
MCP5tools current1.3.3; realGitHubcheckaheadvs1.1.0 changed no lifecycle.

Package: quota-panel-1.3.3-windows-x64.zip
SHA256:9a741ac92ee09c1890b190bbaa0695406ed97f0aa794d4afcabbfc49001bcdc5

One async post-install Start11 acceptance question is pending. Do not claim the
live failure fixed solely from purepolicy/nativelease tests. Publicstable1.1.0
and existing Win10/monitor/sleep/fullscreen limitations remain unchanged.
## 1.3.3 Start11 desktop acceptance completed

User reports that the indicator stays visible throughout the post-install
Start11 menu check. ActualGUI59288 log corroborates active#32770 menu-held/held,
transienttrue, native+WPFvisibletrue, widgetabovetaskbartrue, HINSTANCE0/error0.
Records14:09:31.511/32.843 and35.949/37.236 cross the1600ms source lifetime while
holding unchanged native evidence. The second opening's recorded boundaries
span17.308seconds (user described20seconds separately). Source age reaches17126ms
and leaseage18048ms while visibility remains true. No conceal/unavailable/hidden
record or dropped/coalesced diagnostic entry occurs during the active-menu span.
At14:09:53.465 ordinary fresh visible/recorded placement resumes after closure.
A single belowbar frame belongs after the first menuclosed, not an activemenu.

This closes the reported Start11 context-menu disappearance on this machine.
No code/build/install/version change followed acceptance;1.3.3 stays installed.
Win+Shift+S, other Start11 versions and Windows10 are not newly desktop-validated
by this check. Public release policy and experimental compatibility limits stand.
## Consolidated 1.2.0 release build (2026-10-27)

User explicitly requested build1.2.0 after choosing shared release numbering.
All private1.2.x/1.3.x previews consolidate into this first public successor to1.1.0.
Source prepared on codex/release-1.2.0; no public release or visibility changes.

Metadata now consistently1.2.0 in package/manifests/MCP/controlcard/updater/archive.
Installed prerelease comparison supports beta.N with numeric core and SemVer
identifier precedence. Public latest-feed validation stays stable-only; drafts,
beta tags, malformed versions and unverified URLs/assets remain rejected. No
special case lies about genuine1.3.3 precedence; the local preview transition
is an explicit one-time migration. Beta parser RED/GREEN inherited from partial
agent work was inspected and rerun (10update tests). Combinedsuite147/147 passes.
Nativebuild and package entry integrity checks passed.

Temporary taskbar diagnostics are opt-in via QUOTA_PANEL_DIAGNOSTICS=1; disabled
by default in this build. This gates diagnostic work only, preserves the fixed
Start11 placement behavior and has a focused RED/GREEN test. Newknown issue was
recorded without investigating or changing fullscreen behavior: user reports
indicator missing when opening Start over borderless Honkai:StarRail and Hollow
Knight:Silksong. User explicitly prioritizes completing1.2.0 before that fix.

Local migration1.3.3→1.2.0 completed through original registeredsource+CLI. All24
stage/source/cache hashes match. Owned task migration backed up itsXML and source
under dist/v120-final-upgrade-backup; autostart/logontrigger state preserved.

Stronger settings proof: read-only ownWinexe snapshot via a temporary user-level
manual task runs outside the MSIXshadow and resolves realsettingspath. Snapshot
before/after compares ALL settings semantically; exactmatch. ActualGUIlanguageRU,
Auto/dark, fullmode,bothindicators,shortcontour,right/taskbar-on,opacity100,pausedfalse
preserved. The temporary snapshot tasks were removed. Oldshadowedsettings/state
were never copied over real preferences; no system executionpolicy changed.
Aftermigration actualMCP5tools current1.2.0, stableGitHub latest1.1.0 ->ahead, no
lifecyclechanges. All checks are manual and installation remains manual.

Archive: quota-panel-1.2.0-windows-x64.zip (33.6MiB), all24ZIPentries byte-identical
to reviewed stage. SHA2565a38c46c5be302261bf5b2b3f65bff232e06e5ba186845aefb8a6f95b66efb4b.
Metadata dist/v120-final-package.json /v120-final-installed.json /
v120-final-installed-check.json; helpers rootdist/package-v120-final.mjs,
migrate-v133-to-v120-final.mjs,capture-release-prefs.mjs. No public publication.