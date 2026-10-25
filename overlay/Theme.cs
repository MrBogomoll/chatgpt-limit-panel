using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace QuotaOverlay {
  public sealed class ThemePalette {
    public static readonly ThemePalette Dark=new ThemePalette(false),Light=new ThemePalette(true);
    public readonly string Surface,Border,Divider,Primary,Secondary,Date,Warning,Critical,Short,Weekly,Track;
    public readonly string MenuSurface,MenuText,MenuBorder,MenuHover,MenuCheck,MenuArrow,MenuDivider;
    ThemePalette(bool light) {
      Surface=light?"#F3F3F4":"#242526";Border=light?"#C8CBD0":"#494A4C";Divider=light?"#D4D7DC":"#424345";
      Primary=light?"#25282D":"#ECECEE";Secondary=light?"#626873":"#B1B2B5";Date=light?"#5D626B":"#BFC0C2";
      Warning=light?"#955416":"#E3B382";Critical=light?"#B23B42":"#E6A0A1";
      Short=light?"#207D6B":"#7ADBC5";Weekly=light?"#2B74AB":"#87BEEB";Track=light?"#DADEE3":"#41464B";
      MenuSurface=light?"#FFFFFF":"#292A2D";MenuText=light?"#25282D":"#ECECEE";MenuBorder=light?"#C9CDD3":"#484A4E";
      MenuHover=light?"#EBEDF0":"#414347";MenuCheck=light?"#207D69":"#9CDCCB";MenuArrow=light?"#646B76":"#B4B6BB";MenuDivider=light?"#D8DCE2":"#44464B";
    }
    public string Tone(string tone) {return tone=="critical"?Critical:tone=="warning"?Warning:tone=="unknown"?Secondary:Primary;}
  }

  public sealed class ThemeResolver {
    readonly string configPath;
    readonly Func<bool> windowsLight;
    public string AppPreference {get;private set;}
    public string Source {get;private set;}
    static readonly Regex DesktopSection=new Regex(@"^\s*\[\s*(?:desktop|""desktop""|'desktop')\s*\]\s*$",RegexOptions.CultureInvariant);
    static readonly string ThemeValue="(?:\"{3}(dark|light|system)\"{3}|'{3}(dark|light|system)'{3}|\"(dark|light|system)\"|'(dark|light|system)')\\s*(?:#.*)?$";
    static readonly Regex Entry=new Regex("^\\s*(?:appearanceTheme|\"appearanceTheme\"|'appearanceTheme')\\s*=\\s*"+ThemeValue,RegexOptions.CultureInvariant);
    static readonly Regex DottedEntry=new Regex("^\\s*(?:desktop|\"desktop\"|'desktop')\\s*\\.\\s*(?:appearanceTheme|\"appearanceTheme\"|'appearanceTheme')\\s*=\\s*"+ThemeValue,RegexOptions.CultureInvariant);
    public ThemeResolver(string path,Func<bool> readWindowsLight) {
      configPath=path;windowsLight=readWindowsLight??ReadWindowsLight;AppPreference="system";Source="system-default";
    }
    public static string DefaultConfigPath() {
      string home=Environment.GetEnvironmentVariable("CODEX_HOME");
      if(String.IsNullOrWhiteSpace(home))home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
      return Path.Combine(home,"config.toml");
    }
    public static bool ReadWindowsLight() {
      try {return Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",0))!=0;}catch{return false;}
    }
    public string Resolve(string mode) {
      if(mode=="light"||mode=="dark"){Source="manual";return mode;}
      try {
        using(var stream=new FileStream(configPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
        using(var reader=new StreamReader(stream,Encoding.UTF8,true))AppPreference=ParseAppTheme(reader.ReadToEnd());
        Source="app";
      }catch(IOException){Source="last-known-app";}catch(UnauthorizedAccessException){Source="last-known-app";}
      if(AppPreference=="dark"||AppPreference=="light")return AppPreference;
      Source=Source=="app"?"app-system":"system-fallback";
      try{return windowsLight()?"light":"dark";}catch{return "dark";}
    }
    public static string ParseAppTheme(string toml) {
      bool desktop=false,root=true;char multiline='\0';
      foreach(string raw in (toml??"").TrimStart('\uFEFF').Split(new[]{'\n'})) {
        string line=raw.TrimEnd('\r');
        if(multiline=='\0') {
          Match match=(desktop?Entry:root?DottedEntry:Entry).Match(line);
          if((desktop||root)&&match.Success)for(int i=1;i<match.Groups.Count;i++)if(match.Groups[i].Success)return match.Groups[i].Value;
        }
        bool continued=multiline!='\0';string clean=OutsideMultiline(line,ref multiline).Trim();
        if(continued)continue;
        if(clean.StartsWith("[",StringComparison.Ordinal)){desktop=DesktopSection.IsMatch(clean);root=false;}
      }
      return "system";
    }
    static string OutsideMultiline(string line,ref char multiline) {
      var result=new StringBuilder();int i=0;
      while(i<line.Length) {
        char c=line[i];
        if(multiline!='\0') {
          int slashes=0;for(int before=i-1;before>=0&&line[before]=='\\';before--)slashes++;
          if(i+2<line.Length&&c==multiline&&line[i+1]==c&&line[i+2]==c&&(c=='\''||slashes%2==0)){multiline='\0';i+=3;}else i++;
        } else if(c=='#')break;
        else if((c=='\''||c=='"')&&i+2<line.Length&&line[i+1]==c&&line[i+2]==c){multiline=c;result.Append(' ');i+=3;}
        else if(c=='\''||c=='"') {
          char quote=c;result.Append(c);i++;
          while(i<line.Length) {char part=line[i++];result.Append(part);if(quote=='"'&&part=='\\'&&i<line.Length){result.Append(line[i++]);continue;}if(part==quote)break;}
        } else {result.Append(c);i++;}
      }
      return result.ToString();
    }
  }
}
