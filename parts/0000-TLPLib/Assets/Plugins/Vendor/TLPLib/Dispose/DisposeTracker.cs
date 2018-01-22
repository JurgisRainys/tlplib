﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using com.tinylabproductions.TLPLib.Data.typeclasses;
using com.tinylabproductions.TLPLib.Reactive;
using GenerationAttributes;
using JetBrains.Annotations;

namespace com.tinylabproductions.TLPLib.dispose {
  [Record]
  public partial struct TrackedDisposable : IStr {
    public readonly IDisposable disposable;
    public readonly string callerMemberName, callerFilePath;
    public readonly int callerLineNumber;

    public string asString() => $"{callerMemberName} @ {callerFilePath}:{callerLineNumber}";
  }
  
  public interface IDisposableTracker : IDisposable {
    void track(
      IDisposable a,
      [CallerMemberName] string callerMemberName = "", 
      [CallerFilePath] string callerFilePath = "", 
      [CallerLineNumber] int callerLineNumber = 0
    );
    
    int trackedCount { get; }
    IEnumerable<TrackedDisposable> trackedDisposables { get; }
  }

  public class DisposableTracker : IDisposableTracker {
    readonly List<TrackedDisposable> list = new List<TrackedDisposable>();
    public int trackedCount => list.Count;
    public IEnumerable<TrackedDisposable> trackedDisposables => list;

    public void track(
      IDisposable a,
      [CallerMemberName] string callerMemberName = "", 
      [CallerFilePath] string callerFilePath = "", 
      [CallerLineNumber] int callerLineNumber = 0
    ) => list.Add(new TrackedDisposable(
      a, callerMemberName, callerFilePath, callerLineNumber
    ));

    public int count => list.Count;

    public void Dispose() {
      foreach (var a in list) a.disposable.Dispose();
      list.Clear();
      list.Capacity = 0;
    }
  }

  /// <summary>
  /// Used when we are sure that we never want to clean the subscription automatically
  /// (for example in Observable operations). 
  /// </summary>
  public class NoOpDisposableTracker : IDisposableTracker {
    public static readonly IDisposableTracker instance = new NoOpDisposableTracker();
    NoOpDisposableTracker() {}
    
    public void track(
      IDisposable a,
      string callerMemberName, 
      string callerFilePath, 
      int callerLineNumber
    ) {}

    public int trackedCount => 0;
    public IEnumerable<TrackedDisposable> trackedDisposables => Enumerable.Empty<TrackedDisposable>();
    public void Dispose() {}
  }
}