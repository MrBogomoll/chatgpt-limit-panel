# Стиль описаний релизов

## Единая нумерация

Номер принадлежит плагину и одинаков для исходников, манифеста, архива и
публичного выпуска. Приватный репозиторий хранит разработку; публичный — готовые
выпуски и материалы для пользователей. Независимой нумерации у них нет.

Закрытые промежуточные сборки с обозначениями 1.2.x и 1.3.x объединяются в
публичную версию 1.2.0. Это разовая перенумерация до публикации; установленная
предварительная 1.3.3 переводится на 1.2.0 явно, с сохранением настроек. Не
добавлять ложные правила, по которым настоящая 1.3.3 считалась бы старее 1.2.0.

Будущие доработки готовящегося выпуска обозначать как 1.3.0-beta.1,
1.3.0-beta.2, затем 1.3.0. Несколько новых функций и исправлений могут входить в
один выпуск. Исправления во время бета-подготовки увеличивают номер beta, а
после публичного выпуска — номер patch. Проверка обновлений доставляет только
стабильные версии; поддержка сравнения beta не включает отдельный бета-канал.

## Shared release numbering

Source, manifest, archive and public release use one plugin version. Private
1.2.x/1.3.x previews are consolidated into 1.2.0 before publication. Migrating
an installed 1.3.3 preview is an explicit one-time step, preserving settings;
normal version precedence must not be falsified. Future work uses beta.N
identifiers for the upcoming version, followed by its stable release. Multiple
features and fixes can share one release. Public update checks remain stable-only.

В публичных описаниях выпуска и README писать об изменениях для пользователя:
что добавлено, что исправлено, как обновиться и какие ограничения влияют на работу.
Сохранять русскую и английскую версии пояснений.

Не включать количество пройденных тестов, фразы «пользователь подтвердил»,
пересказ общения с автором и служебный отчёт о ходе разработки. Технические
результаты проверок хранить отдельно в приватном docs/verification.md.

## Выбор номера версии

Номер следующей версии выбирать самостоятельно по масштабу изменения:

- Исправления и настройка существующего поведения повышают последнюю цифру:
  например, 1.2.1 → 1.2.2.
- Новая возможность повышает среднюю цифру и обнуляет последнюю:
  например, 1.2.2 → 1.3.0.
- Большое изменение повышает первую цифру и обнуляет две младшие:
  например, 1.3.0 → 2.0.0.

## Version selection

Choose the next version independently according to the scope of the change:

- Fixes and adjustments to existing behaviour increment the patch number:
  for example, 1.2.1 → 1.2.2.
- A new feature increments the minor number and resets the patch number:
  for example, 1.2.2 → 1.3.0.
- A large change increments the major number and resets both lower numbers:
  for example, 1.3.0 → 2.0.0.
