using System;using System.Collections.Generic;using System.Diagnostics;using System.Threading;using QuotaOverlay;
class TaskbarDiagnosticSpoolTest {
  static int assertions;
  static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
  static Dictionary<string,object> State(int value){return new Dictionary<string,object>{{"value",value}};}
  static void BoundedQueue(){
    var jobs=new List<Action>();var keys=new List<string>();var times=new List<long>();var states=new List<Dictionary<string,object>>();long now=0;
    var spool=new TaskbarDiagnosticSpool(delegate(string key,IntPtr widget,Dictionary<string,object> state){keys.Add(key);times.Add(now);states.Add(state);},delegate(Action action){jobs.Add(action);},delegate{return now;},delegate(int milliseconds){now+=milliseconds;});
    var original=State(0);spool.Enqueue("k0",new IntPtr(123),original);original["value"]=99;
    for(int i=1;i<100;i++)spool.Enqueue("k"+i,IntPtr.Zero,State(i));
    Check(jobs.Count==1&&keys.Count==0,"enqueue schedules one background drain without performing metadata or IO work inline");jobs[0]();
    Check(keys.Count==8&&keys[0]=="k92"&&keys[7]=="k99","overflow drops oldest records and keeps at most eight pending states");
    for(int i=1;i<times.Count;i++)Check(times[i]-times[i-1]>=200,"worker starts must be spaced by at least 200 milliseconds");
    Check(states[0].ContainsKey("diagnosticQueuedAt")&&states[0].ContainsKey("diagnosticWorkerAt")&&states[0].ContainsKey("diagnosticQueue"),"queued event time, deferred metadata time and loss counters remain distinguishable");
    Check(!original.ContainsKey("diagnosticQueuedAt"),"the caller's state dictionary is never mutated");
    spool.Enqueue("later",IntPtr.Zero,State(100));Check(jobs.Count==2,"a later state can restart a completed worker");jobs[1]();Check(keys[keys.Count-1]=="later","later records are not stranded after a drain exits");
  }
  static void CoalescingAndErrors(){
    var jobs=new List<Action>();var values=new List<int>();int attempts=0;long now=0;
    var spool=new TaskbarDiagnosticSpool(delegate(string key,IntPtr widget,Dictionary<string,object> state){attempts++;if(key=="bad")throw new InvalidOperationException("expected fixture failure");values.Add((int)state["value"]);},delegate(Action action){jobs.Add(action);},delegate{return now;},delegate(int milliseconds){now+=milliseconds;});
    spool.Enqueue("same",IntPtr.Zero,State(1));spool.Enqueue("same",IntPtr.Zero,State(2));spool.Enqueue("bad",IntPtr.Zero,State(3));spool.Enqueue("after",IntPtr.Zero,State(4));
    Check(jobs.Count==1,"duplicate and distinct events share one drain");jobs[0]();
    Check(attempts==3&&values.Count==2&&values[0]==2&&values[1]==4,"adjacent duplicate keys coalesce to the newest state and a writer error does not kill later records");
    spool.Enqueue("again",IntPtr.Zero,State(5));jobs[1]();Check(values[2]==5,"writer failures do not prevent future worker restart");
    var before=jobs.Count;spool.Enqueue(null,IntPtr.Zero,State(6));spool.Enqueue(" ",IntPtr.Zero,State(7));Check(jobs.Count==before,"empty diagnostic keys do not schedule work");
  }
  static void RealBackgroundWorker(){
    int caller=Thread.CurrentThread.ManagedThreadId,writerThread=caller,active=0,maximum=0,written=0;
    using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false))using(var done=new ManualResetEvent(false)) {
      var guard=new object();
      var spool=new TaskbarDiagnosticSpool(delegate(string key,IntPtr widget,Dictionary<string,object> state){
        int current=Interlocked.Increment(ref active);lock(guard){maximum=Math.Max(maximum,current);writerThread=Thread.CurrentThread.ManagedThreadId;}
        try{if(key=="first"){entered.Set();release.WaitOne();}Interlocked.Increment(ref written);if(key=="final")done.Set();}finally{Interlocked.Decrement(ref active);}
      });
      try {
        spool.Enqueue("first",IntPtr.Zero,State(0));Check(entered.WaitOne(2000),"the production scheduler starts its background worker");
        var watch=Stopwatch.StartNew();for(int i=0;i<24;i++)spool.Enqueue("queued"+i,IntPtr.Zero,State(i));spool.Enqueue("final",IntPtr.Zero,State(25));
        Check(watch.ElapsedMilliseconds<500,"enqueue never waits for a blocked metadata writer");
        release.Set();Check(done.WaitOne(6000),"bounded pending states complete after the blocked writer resumes");
        Check(writerThread!=caller&&maximum==1,"metadata collection and IO stay on one background worker");
        Check(written<=9,"only one in-flight record plus eight pending states survive overflow");
      }finally{release.Set();}
    }
  }
  static int Main(){try{BoundedQueue();CoalescingAndErrors();RealBackgroundWorker();Console.WriteLine("PASS bounded background diagnostics spool ("+assertions+" assertions)");return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
