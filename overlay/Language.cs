using System;
using System.Collections.Generic;
using System.IO;

namespace QuotaOverlay {
  internal static class PanelLanguage {
    static readonly Dictionary<string,string[]> Copy=new Dictionary<string,string[]> {
      {"refresh",new[]{"Обновить лимиты","Refresh limits"}},
      {"checkUpdates",new[]{"Проверить обновления","Check for updates"}},
      {"checkingUpdates",new[]{"Проверка обновлений…","Checking for updates…"}},
      {"updatesTitle",new[]{"Quota Panel — обновления","Quota Panel — updates"}},
      {"updateAvailable",new[]{"Доступна Quota Panel {0}.\nУстановлена: {1}.","Quota Panel {0} is available.\nInstalled: {1}."}},
      {"updateCurrent",new[]{"Установлена актуальная Quota Panel {0}.","Quota Panel {0} is up to date."}},
      {"updateAhead",new[]{"Установленная версия {0} новее последнего релиза {1}.","Installed {0} is newer than the latest release {1}."}},
      {"updateError",new[]{"Не удалось проверить обновления. Проверь соединение и повтори попытку.","Could not check for updates. Check your connection and try again."}},
      {"updateRateLimit",new[]{"Достигнут лимит запросов GitHub. Повтори проверку позже.","GitHub request limit reached. Try again later."}},
      {"updateTimeout",new[]{"Время проверки обновлений истекло. Повтори попытку.","The update check timed out. Try again."}},
      {"openRelease",new[]{"Открыть релиз","Open release"}},
      {"dismiss",new[]{"Закрыть","Close"}},
      {"openReleaseError",new[]{"Не удалось открыть страницу релиза.","Could not open the release page."}},
      {"view",new[]{"Вид панели","Panel view"}},
      {"full",new[]{"Полный","Full"}},
      {"compact",new[]{"Компактный","Compact"}},
      {"ultra",new[]{"Ультракомпактный","Ultracompact"}},
      {"theme",new[]{"Тема","Theme"}},
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
      {"pauseUpdates",new[]{"Приостановить обновление","Pause updates"}},
      {"resumeUpdates",new[]{"Продолжить обновление","Resume updates"}},
      {"stop",new[]{"Остановить панель","Stop panel"}},
      {"pause",new[]{"Приостановить","Pause"}},
      {"resume",new[]{"Продолжить","Resume"}},
      {"trayTitle",new[]{"Лимиты Codex / Work","Codex / Work limits"}},
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
    internal static string Current {get{return Read(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChatGPTQuotaOverlay","settings.json"));}}
    internal static string Period(string language,string original,int index) {
      if(index==1)return Text(language,"weekLabel");
      string candidate=(original??"").Replace(" ч"," h");
      return language=="en"?candidate.Replace(" ч"," h"):candidate.Replace(" h"," ч");
    }
  }
}
