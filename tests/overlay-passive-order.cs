using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using QuotaOverlay;

// Native API contract fixture only. Every HWND belongs to this test, stays fully
// transparent and offscreen, and uses WS_EX_NOACTIVATE. No foreign HWND is changed.
internal static class PassiveOrderContractTest {
  const uint NoSize=0x0001,NoMove=0x0002,NoZOrder=0x0004,NoActivate=0x0010,Show=0x0040;
  const long Topmost=0x00000008,Transparent=0x00000020,Tool=0x00000080,Layered=0x00080000,NoActivateStyle=0x08000000;
  static readonly IntPtr Top=new IntPtr(-1);
  static readonly List<IntPtr> windows=new List<IntPtr>();
  static int assertions;
  [StructLayout(LayoutKind.Sequential)]struct Rect {public int Left,Top,Right,Bottom;}
  [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rect bounds);
  [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out Rect bounds,int size);
  static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
  static bool IsTopmost(IntPtr window){return (Long(window,-20).ToInt64()&Topmost)!=0;}
  static bool Above(IntPtr candidate,IntPtr reference){IntPtr cursor=reference;for(int i=0;i<4096;i++){cursor=GetWindow(cursor,3);if(cursor==IntPtr.Zero)return false;if(cursor==candidate)return true;}throw new Exception("Fixture Z order exceeded bounded traversal");}
  static bool ProtectedOrder(IntPtr own,IntPtr boundary,params IntPtr[] canvases){if(!Above(own,boundary))return false;foreach(IntPtr canvas in canvases)if(!Above(canvas,own))return false;return true;}
  static void Position(IntPtr window,IntPtr anchor,uint flags){Check(SetWindowPos(window,anchor,0,0,0,0,NoMove|NoSize|NoActivate|flags),"Position own fixture HWND succeeds");}
  static IntPtr Window(bool topmost,bool canvas){uint extended=(uint)(Tool|Layered|NoActivateStyle|(topmost?Topmost:0)|(canvas?Transparent:0));uint style=0x80000000U|(canvas?0x08000000U:0);
    IntPtr window=CreateWindowEx(extended,"STATIC",null,style,-30000,-30000,72,48,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);Check(window!=IntPtr.Zero,"Create own offscreen native fixture window");windows.Add(window);
    Check(SetLayeredWindowAttributes(window,0,0,2),"Fixture remains fully transparent");return window;
  }
  static TaskbarArea FixtureArea(IntPtr window){Rect native,frame;Check(GetWindowRect(window,out native),"Own fixture native bounds are readable");Check(DwmGetWindowAttribute(window,9,out frame,Marshal.SizeOf(typeof(Rect)))==0&&native.Left==frame.Left&&native.Top==frame.Top&&native.Right==frame.Right&&native.Bottom==frame.Bottom,"Fixture uses production physical native/DWM coordinates");return new TaskbarArea{X=native.Left,Y=native.Top,Width=native.Right-native.Left,Height=native.Bottom-native.Top};}
  static void CheckAlphaZero(IntPtr window){uint key,flags;byte alpha;Check(GetLayeredWindowAttributes(window,out key,out alpha,out flags)&&alpha==0&&(flags&2)!=0,"Initial zero opacity is explicitly established");}
  static void FirstPlacementAndMenuOrder(){
    IntPtr before=GetForegroundWindow();
    IntPtr boundary=Window(true,false),nearestCanvas=Window(true,true),upperCanvas=Window(true,true),own=Window(false,false);
    Position(boundary,Top,Show);Position(nearestCanvas,Top,Show);Position(upperCanvas,Top,Show);
    Check(Above(upperCanvas,nearestCanvas)&&Above(nearestCanvas,boundary),"Fixture has two topmost canvases above its fake taskbar");
    Check(!IsTopmost(own)&&!IsWindowVisible(own),"Own first placement begins hidden and non-topmost");CheckAlphaZero(own);
    Position(own,nearestCanvas,Show);
    Check(IsWindowVisible(own),"Direct initial anchor placement shows the own window");
    Check(IsTopmost(own),"Insertion behind a topmost anchor establishes the topmost band on this Windows runtime");
    Check(ProtectedOrder(own,boundary,nearestCanvas,upperCanvas),"Nearest eligible canvas anchor puts own window below every canvas and above its taskbar");
    Check(GetWindow(own,4)==IntPtr.Zero,"Anchoring does not give the own window a foreign owner");
    CheckAlphaZero(own);

    var sample=new TaskbarSnapshot{Handle=boundary,Bounds=FixtureArea(boundary),Visible=true,OccupancyKnown=true};
    FixtureArea(nearestCanvas);FixtureArea(upperCanvas);
    var footprint=sample.Bounds;
    Func<IntPtr,bool> eligible=delegate(IntPtr item){return item==nearestCanvas||item==upperCanvas;};
    var anchor=TaskbarNative.PlacementAnchor(sample,footprint,own,false,eligible);
    
    Check(anchor.HasValue&&anchor.Value==nearestCanvas,"production selects the overlapping canvas nearest the taskbar");
    Check(TaskbarNative.PlacementAnchor(sample,footprint,own,true,eligible).HasValue,"production verifies the existing below-canvas above-taskbar placement");

    IntPtr popup=Window(true,false);Position(popup,Top,Show);
    Check(Above(popup,upperCanvas)&&ProtectedOrder(own,boundary,nearestCanvas,upperCanvas),"An own fixture popup can open without disturbing the safe order");
    Position(own,Top,NoZOrder);
    Check(ProtectedOrder(own,boundary,nearestCanvas,upperCanvas)&&Above(popup,own),"SWP_NOZORDER retains an already safe canvas/taskbar order and popup priority");

    // Model an unrelated raise before a menu starts. NOZORDER must never be
    // treated as a repair: its hWndInsertAfter argument is ignored by user32.
    Position(own,popup,0);
    Check(Above(popup,own)&&Above(own,upperCanvas),"Unsafe menu fixture places own above the canvases but still below the popup");
    Check(!ProtectedOrder(own,boundary,nearestCanvas,upperCanvas),"Preserve-order precondition detects that unsafe placement");
    Check(!TaskbarNative.PlacementAnchor(sample,footprint,own,true,eligible).HasValue,"production refuses unsafe preservation above a passive canvas");
    Position(own,nearestCanvas,NoZOrder);
    Check(!ProtectedOrder(own,boundary,nearestCanvas,upperCanvas)&&Above(own,upperCanvas)&&Above(popup,own),"SWP_NOZORDER ignores even a valid anchor and cannot fix an unsafe menu placement");

    // Once preservation is no longer required, changing only our own Z order
    // restores the safe band without raising over the existing popup.
    Position(own,nearestCanvas,0);
    Check(ProtectedOrder(own,boundary,nearestCanvas,upperCanvas)&&Above(popup,own),"Normal mode can restore below-canvas placement by moving only its own HWND");

    Position(own,upperCanvas,0);
    Check(Above(own,nearestCanvas)&&!ProtectedOrder(own,boundary,nearestCanvas,upperCanvas),"Choosing the highest canvas would incorrectly leave own above the nearer canvas");
    Position(own,nearestCanvas,0);
    Check(ProtectedOrder(own,boundary,nearestCanvas,upperCanvas),"The canvas nearest the taskbar is the required insertion anchor");
    var noCanvas=TaskbarNative.PlacementAnchor(sample,footprint,own,false,delegate{return false;});
        Check(noCanvas.HasValue&&noCanvas.Value==Top,"ordinary placement retains the original topmost anchor");

    var staleAnchor=TaskbarNative.PlacementAnchor(sample,footprint,own,false,eligible);
    Check(staleAnchor.HasValue&&staleAnchor.Value==nearestCanvas,"Taskbar raise race begins with a verified nearest-canvas anchor");
    Position(boundary,Top,0);
    Position(own,staleAnchor.Value,0);
    Check(Above(boundary,own),"Fixture taskbar can rise above both canvases before the old anchor is applied");
    try {
      Check(!TaskbarNative.PlacementAnchor(sample,footprint,own,true,eligible).HasValue,"production rejects post-placement below-taskbar order even when no passive canvas remains above the taskbar");
    } finally {
      Position(boundary,own,0);
    }
    Check(ProtectedOrder(own,boundary,nearestCanvas,upperCanvas),"Fixture restores the taskbar below the widget after the ordering race");
    Check(!TaskbarNative.PlacementAnchor(null,footprint,own,false,eligible).HasValue,"missing taskbar evidence fails closed");
    Check(before==GetForegroundWindow(),"Every fixture creation, show and positioning step preserves the real foreground HWND");
  }
  static void ClearWindows(){foreach(IntPtr window in windows)DestroyWindow(window);windows.Clear();}
  static void PassiveLeaseMembership(){
    IntPtr before=GetForegroundWindow();
    IntPtr boundary=Window(true,false),own=Window(true,false),canvas=Window(true,true);
    Position(boundary,Top,Show);Position(own,Top,Show);
    var sample=new TaskbarSnapshot{Handle=boundary,Bounds=FixtureArea(boundary),Visible=true,OccupancyKnown=true};
    bool nativeSafe=true;int safetyCalls=0;
    Func<TaskbarSnapshot,IntPtr,bool> safety=delegate(TaskbarSnapshot value,IntPtr widget){safetyCalls++;return nativeSafe&&Object.ReferenceEquals(value,sample)&&widget==own;};
    Func<IntPtr,bool> passive=delegate(IntPtr window){return window==canvas;};
    Func<string> fingerprint=delegate{return TaskbarNative.LeaseFingerprintCore(sample,sample.Bounds,own,safety,passive,0);};
    string baseline=fingerprint();Check(baseline!=null&&safetyCalls==2,"complete native lease collection checks the injected shell boundary twice");
    Check(TaskbarNative.LeaseFingerprint(sample,sample.Bounds,own)==null,"production wrapper refuses a fake taskbar instead of borrowing the fixture guard");
    Position(canvas,Top,Show);
    Check(ProtectedOrder(own,boundary,canvas),"a newly visible passive canvas is above the widget while the widget remains above the fake bar");
    string added=fingerprint();Check(added!=null&&TaskbarNative.LastFingerprintReason=="ok","new passive canvas with protected order keeps a complete native proof");
    Check(ShowWindow(canvas,0),"hide only the fixture canvas after its delayed appearance");
    string removed=fingerprint();Check(removed==baseline,"removing a passive canvas restores the same occupied controls evidence");
    Position(canvas,boundary,Show);
    Check(Above(boundary,canvas)&&Above(own,boundary),"a passive fixture may remain visible behind the fake taskbar");
    Check(fingerprint()==baseline,"a passive canvas below the taskbar does not change the occupied controls evidence");
    Position(canvas,Top,Show);Position(own,Top,0);
    Check(fingerprint()==null&&TaskbarNative.LastFingerprintReason=="above-taskbar-overlap","a canvas below the widget cannot claim the passive lease exemption");
    Position(own,boundary,0);
    Check(fingerprint()==null&&TaskbarNative.LastFingerprintReason=="above-taskbar-overlap","a widget below its taskbar cannot claim safe passive underlay order");
    Position(own,canvas,0);
    IntPtr blocker=Window(true,false);Position(blocker,Top,Show);
    Check(fingerprint()==null&&TaskbarNative.LastFingerprintReason=="above-taskbar-overlap","an ordinary foreign overlap cannot borrow the passive canvas exemption");
    ShowWindow(blocker,0);nativeSafe=false;
    Check(fingerprint()==null&&TaskbarNative.LastFingerprintReason=="initial-native-unsafe","missing live shell-order proof rejects even an otherwise protected passive canvas");
    nativeSafe=true;
    Check(ProtectedOrder(own,boundary,canvas),"native control regression starts with the passive canvas safely above the widget");
    // This is a child of the fully transparent offscreen fake bar, never a
    // top-level application/taskbar button and never a real shell control.
    IntPtr control=CreateWindowEx((uint)NoActivateStyle,"STATIC",null,0x50000000U,2,2,16,16,boundary,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
    Check(control!=IntPtr.Zero&&IsWindowVisible(control),"create a visible own native child inside the fake taskbar footprint");windows.Add(control);
    Check(fingerprint()==null&&TaskbarNative.LastFingerprintReason=="protected-child-overlap","a new native taskbar control revokes the lease even beneath a safe passive canvas");
    Check(ShowWindow(control,0),"hide only the fixture's native taskbar child");
    Check(fingerprint()==baseline,"hiding the native taskbar child restores baseline evidence with the passive canvas present");
    Check(DestroyWindow(control),"destroy only the fixture's native taskbar child");windows.Remove(control);
    Check(fingerprint()==baseline,"removing the native taskbar child retains baseline evidence with the passive canvas present");
    Check(before==GetForegroundWindow(),"lease membership fixture preserves the real foreground");
    Check(added==baseline,"a delayed passive canvas above the widget must not change occupied-controls lease evidence");
  }
  static int Main(){try{FirstPlacementAndMenuOrder();ClearWindows();PassiveLeaseMembership();Console.WriteLine("PASS passive canvas native ordering contract ("+assertions+" assertions)");return 0;}catch(Exception error){Console.Error.WriteLine(error.GetType().Name+": "+error.Message);return 1;}finally{ClearWindows();}}
  [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateWindowEx(uint extended,string className,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr parameter);
  [DllImport("user32.dll")]static extern bool DestroyWindow(IntPtr window);
  [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
  [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr window,uint key,byte alpha,uint flags);
  [DllImport("user32.dll")]static extern bool GetLayeredWindowAttributes(IntPtr window,out uint key,out byte alpha,out uint flags);
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr Long(IntPtr window,int index);
  [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr window,uint command);
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
}
