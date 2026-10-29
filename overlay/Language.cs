using System;
using System.Collections.Generic;
using System.IO;

namespace QuotaOverlay {
  internal static class PanelLanguage {
    static readonly Dictionary<string,string[]> Copy=new Dictionary<string,string[]> {
      {"refresh",new[]{"Обновить лимиты","Refresh limits"}},
      {"checkUpdates",new[]{"Проверить новую версию","Check for a new version"}},
      {"checkingUpdates",new[]{"Проверка обновлений…","Checking for updates…"}},
      {"updatesTitle",new[]{"ChatGPT Limit Panel — обновления","ChatGPT Limit Panel — updates"}},
      {"updateAvailable",new[]{"Доступна ChatGPT Limit Panel {0}.\nУстановлена: {1}.","ChatGPT Limit Panel {0} is available.\nInstalled: {1}."}},
      {"updateCurrent",new[]{"Установлена актуальная ChatGPT Limit Panel {0}.","ChatGPT Limit Panel {0} is up to date."}},
      {"updateAhead",new[]{"Установленная версия {0} новее последнего релиза {1}.","Installed {0} is newer than the latest release {1}."}},
      {"updateError",new[]{"Не удалось проверить обновления. Проверь соединение и повтори попытку.","Could not check for updates. Check your connection and try again."}},
      {"updateRateLimit",new[]{"Достигнут лимит запросов GitHub. Повтори проверку позже.","GitHub request limit reached. Try again later."}},
      {"updateTimeout",new[]{"Время проверки обновлений истекло. Повтори попытку.","The update check timed out. Try again."}},
      {"openRelease",new[]{"Открыть релиз","Open release"}},
      {"dismiss",new[]{"Закрыть","Close"}},
      {"openReleaseError",new[]{"Не удалось открыть страницу релиза.","Could not open the release page."}},
      {"view",new[]{"Вид панели","Panel view"}},
      {"resetPosition",new[]{"Расположение по умолчанию","Reset position"}},
      {"taskbar",new[]{"Панель задач","Taskbar"}},
      {"taskbarShow",new[]{"Показывать проценты","Show percentages"}},
      {"taskbarLeft",new[]{"Слева","Left"}},
      {"taskbarRight",new[]{"Справа","Right"}},
      {"taskbarTop",new[]{"Сверху","Top"}},
      {"taskbarBottom",new[]{"Снизу","Bottom"}},
      {"taskbarOff",new[]{"Выключено","Off"}},
      {"taskbarActive",new[]{"Работает","Active"}},
      {"taskbarWaiting",new[]{"Ожидание свободного места","Waiting for free space"}},
      {"full",new[]{"Полный","Full"}},
      {"compact",new[]{"Компактный","Compact"}},
      {"ultra",new[]{"Ультракомпактный","Ultracompact"}},
      {"theme",new[]{"Тема","Theme"}},
      {"backgroundTransparency",new[]{"Прозрачность фона","Background transparency"}},
      {"dark",new[]{"Тёмная","Dark"}},
      {"light",new[]{"Светлая","Light"}},
      {"auto",new[]{"Авто","Auto"}},
      {"language",new[]{"Язык / Language","Language / Язык"}},
      {"ru",new[]{"Русский","Русский"}},
      {"en",new[]{"English","English"}},
      {"autostart",new[]{"Автозапуск с ChatGPT","Start with ChatGPT"}},
      {"indicator",new[]{"Индикатор","Indicator"}},
      {"bars",new[]{"Полоски","Bars"}},
      {"outline",new[]{"Контур","Outline"}},
      {"both",new[]{"Полоски и контур","Bars and outline"}},
      {"off",new[]{"Без индикатора","No indicator"}},
      {"source",new[]{"Лимит контура","Outline limit"}},
      {"short",new[]{"5 часов","5 hours"}},
      {"weekly",new[]{"Неделя","Weekly"}},
      {"shortLabel",new[]{"5 ч","5 h"}},
      {"weekLabel",new[]{"Нед.","Week"}},
      {"stale",new[]{"устар.","stale"}},
      {"pauseUpdates",new[]{"Приостановить обновление лимитов","Pause quota refresh"}},
      {"resumeUpdates",new[]{"Возобновить обновление лимитов","Resume quota refresh"}},
      {"stop",new[]{"Остановить панель","Stop panel"}},
      {"pause",new[]{"Приостановить обновление лимитов","Pause quota refresh"}},
      {"resume",new[]{"Возобновить обновление лимитов","Resume quota refresh"}},
      {"trayTitle",new[]{"ChatGPT Limit Panel","ChatGPT Limit Panel"}},
      {"refreshNow",new[]{"Обновить сейчас","Refresh now"}},
      {"close",new[]{"Закрыть монитор","Close monitor"}},
      {"autostartError",new[]{"Не удалось изменить автозапуск. Проверь задачу панели в Планировщике Windows.","Could not change autostart. Check the panel task in Windows Task Scheduler."}},
      {"running",new[]{"Панель запущена.","Panel is running."}},
      {"stopped",new[]{"Панель остановлена.","Panel is stopped."}},
      {"commandError",new[]{"Не удалось выполнить команду панели. Проверь локальный Codex и повтори запуск.","Could not complete the panel command. Check local Codex and try again."}},
      {"unknownCommand",new[]{"Неизвестная команда монитора.","Unknown monitor command."}},
      {"launchError",new[]{"Не удалось запустить монитор.","Could not start the monitor."}},
      {"startError",new[]{"Монитор не запустился. Проверь вход в локальный Codex и файлы плагина.","The monitor did not start. Check your local Codex sign-in and the plugin files."}},
      {"codexMissing",new[]{"Не найден локальный Codex. Открой локальный чат Codex в ChatGPT и повтори запуск панели.","Local Codex was not found. Open a local Codex chat in ChatGPT and try starting the panel again."}},
      {"timeout",new[]{"Панель не ответила вовремя.","The panel did not respond in time."}},
    };
    internal static string Normalize(string value) {return value=="en"?"en":"ru";}
    internal static string Text(string language,string key) {return Copy[key][Normalize(language)=="en"?1:0];}
    internal static string Read(string settingsFile) {
      try {object value;return Preferences.Read(settingsFile).TryGetValue("language",out value)?Normalize(value as string):"ru";}catch{return "ru";}
    }
    internal static string Current {get{return Read(Path.Combine(Preferences.DataDirectory(),"settings.json"));}}
    internal static string Period(string language,string original,int index) {
      if(index==1)return Text(language,"weekLabel");
      string candidate=(original??"").Replace(" ч"," h");
      return language=="en"?candidate.Replace(" ч"," h").Replace(" мин"," min"):candidate.Replace(" h"," ч").Replace(" min"," мин");
    }
  }
}
