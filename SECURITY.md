# Security policy / Политика безопасности

## Local data flow / Локальные данные

The panel requests read-only quota data from the local Codex App Server using your existing Codex sign-in. It does not read or bundle `auth.json`, pass tokens as command-line arguments, or forward raw backend stderr and error messages to the UI. Only validated quota, reset and credit fields are formatted for local state and display. Update checks contact the public GitHub releases API without your Codex credentials.

Starting with version **1.8.0**, the installer no longer copies the entire Codex `config.toml` into its generated package sources: that file could contain an inline API key. Older installers may have left local `config-before-install.toml` backups. These backups are excluded from the release archive and Git; keep them private. The sign-in itself remains managed by Codex. Processes running as your Windows user still have access to that user's files and memory; this is not a separate security boundary against malware on the computer.

Панель запрашивает квоты через локальный Codex App Server с вашим существующим входом. Она не читает и не упаковывает `auth.json`, не передаёт токены аргументами командной строки и не показывает сырые ошибки или stderr сервера. В локальное состояние и интерфейс попадают только проверенные поля квот, сбросов и кредитов. Проверка обновлений обращается к публичному GitHub API без ваших данных входа Codex.

С версии **1.8.0** установщик перестал копировать полный `config.toml` Codex в сгенерированные исходники пакета: в нём может находиться API-ключ. После старых установок могут оставаться локальные копии `config-before-install.toml`; они не включаются в релизный ZIP и Git. Авторизацией продолжает управлять сам Codex. Процессы, запущенные от имени вашего пользователя Windows, имеют доступ к его файлам и памяти — это не отдельная граница защиты от вредоносных программ на компьютере.

## Reporting a vulnerability

Please do not post credentials, tokens, personal logs, or unpatched vulnerability details in a public issue. If private vulnerability reporting is available, use **Security → Report a vulnerability** in this repository. Otherwise, open an issue without sensitive details to arrange a private contact method. Include the affected version, reproduction steps, and potential impact without sharing secrets.

Security fixes are intended for the latest stable release. Older releases may not receive fixes; please update to the latest version before reporting when possible.

## Сообщение об уязвимости

Не публикуйте в открытых Issues учётные данные, токены, личные журналы или подробности неисправленной уязвимости. Если доступна приватная отправка, используйте **Security → Report a vulnerability** в этом репозитории. Иначе создайте обращение без конфиденциальных подробностей, чтобы согласовать приватный способ связи. Укажите версию, шаги воспроизведения и возможные последствия, не передавая секреты.

Исправления безопасности предназначены для последнего стабильного выпуска. Старые версии могут не получать исправления; по возможности обновитесь перед обращением.
