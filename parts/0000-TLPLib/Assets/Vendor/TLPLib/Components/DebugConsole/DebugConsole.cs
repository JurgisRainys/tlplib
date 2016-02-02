﻿using System;
using System.Collections.Generic;
using com.tinylabproductions.TLPLib.Extensions;
using com.tinylabproductions.TLPLib.Functional;
using com.tinylabproductions.TLPLib.Logger;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.tinylabproductions.TLPLib.Components.DebugConsole {
  public class DConsole {
    public struct Command {
      public readonly string cmdGroup, name;
      public readonly Act run;

      public Command(string cmdGroup, string name, Act run) {
        this.cmdGroup = cmdGroup;
        this.name = name;
        this.run = run;
      }
    }

    struct Instance {
      public readonly DebugConsoleBinding view;

      public Instance(DebugConsoleBinding view) {
        this.view = view;
      }
    }

    public static DConsole instance { get; } = new DConsole();

    DConsole() {
      registrarFor(nameof(DConsole)).register("Self-test", () => "self-test");
    }

    readonly Dictionary<string, List<Command>> commands = new Dictionary<string, List<Command>>();
    public bool enabled { get; private set; }

    Option<Instance> current = F.none<Instance>();

    public static readonly int[] DEFAULT_SEQUENCE = { 0, 1, 3, 2, 0, 2, 3, 1, 0 };

    public static RegionClickObservable registerDebugSequence(
      DebugConsoleBinding binding,
      int[] sequence=null
    ) {
      sequence = sequence ?? DEFAULT_SEQUENCE;

      var go = new GameObject {name = "Debug Console initiator"};
      Object.DontDestroyOnLoad(go);

      var obs = go.AddComponent<RegionClickObservable>();
        obs.init(2, 2).sequenceWithinTimeframe(sequence, 3)
        .subscribe(_ => { instance.show(binding); });

      instance.enabled = true;

      return obs;
    }

    public static Option<RegionClickObservable> registerDebugSequenceIfDebug(
      DebugConsoleBinding binding,
      int[] sequence = null
    ) {
      if (Log.isDebug) {
        Log.info("Registering debug console");
        return F.some(registerDebugSequence(binding, sequence));
      }
      else {
        Log.info("Debug console not registered, turn on debug log level.");
        return F.none<RegionClickObservable>();
      }
    }

    public void register(Command command) {
      var list = commands.get(command.cmdGroup).getOrElse(() => {
        var lst = new List<Command>();
        commands[command.cmdGroup] = lst;
        return lst;
      });
      list.Add(command);
    }

    public DConsoleRegistrar registrarFor(string prefix) {
      return new DConsoleRegistrar(this, prefix);
    }

    public void show(DebugConsoleBinding binding) {
      destroy();

      var view = binding.clone();

      foreach (var commandGroup in commands) {
        var button = addButton(view.buttonPrefab, view.commandGroupsHolder.transform);
        button.text.text = commandGroup.Key;
        button.button.onClick.AddListener(() => showGroup(view, commandGroup.Key, commandGroup.Value));
      }

      Application.logMessageReceived += onLogMessageReceived;
      view.closeButton.onClick.AddListener(destroy);

      current = new Instance(view).some();
    }

    void showGroup(DebugConsoleBinding view, string groupName, IEnumerable<Command> commands) {
      view.commandGroupLabel.text = groupName;
      foreach (var t in view.commandsHolder.transform.children()) Object.Destroy(t.gameObject);
      foreach (var command in commands) {
        var button = addButton(view.buttonPrefab, view.commandsHolder.transform);
        button.text.text = command.name;
        button.button.onClick.AddListener(() => command.run());
      }
    }

    static ButtonBinding addButton(ButtonBinding prefab, Transform target) {
      var button = prefab.clone();
      // Parent of RectTransform is being set with parent property. 
      // Consider using the SetParent method instead, with the worldPositionStays 
      // argument set to false. This will retain local orientation and scale rather 
      // than world orientation and scale, which can prevent common UI scaling issues.
      button.GetComponent<RectTransform>().SetParent(target, worldPositionStays: false);
      return button;
    }

    void onLogMessageReceived(string message, string stackTrace, LogType type) {
      current.each(instance => {
        var entry = instance.view.logEntryPrefab.clone();
        var shortText = $"{DateTime.Now}  {type}  {message}";

        entry.text = shortText;
        entry.GetComponent<RectTransform>().SetParent(
          instance.view.logEntriesHolder.transform, worldPositionStays: false
        );
        entry.transform.SetAsFirstSibling();
      });
    }

    public void destroy() {
      current.each(i => {
        Application.logMessageReceived -= onLogMessageReceived;
        Object.Destroy(i.view.gameObject);
      });
      current = current.none;
    }
  }

  public delegate Option<Obj> HasObjFn<Obj>();

  public struct DConsoleRegistrar {
    public readonly DConsole console;
    public readonly string commandGroup;

    public DConsoleRegistrar(DConsole console, string commandGroup) {
      this.console = console;
      this.commandGroup = commandGroup;
    }

    static readonly HasObjFn<Unit> unitSomeFn = () => F.some(F.unit);

    public void register(string name, Act run) {
      register(name, () => { run(); return F.unit; });
    }
    public void register<A>(string name, Fn<A> run) {
      register(name, unitSomeFn, _ => run());
    }
    public void register<Obj>(string name, HasObjFn<Obj> objOpt, Act<Obj> run) {
      register(name, objOpt, obj => { run(obj); return F.unit; });
    }
    public void register<Obj, A>(string name, HasObjFn<Obj> objOpt, Fn<Obj, A> run) {
      var prefixedName = $"[{commandGroup}] {name}";
      console.register(new DConsole.Command(commandGroup, name, () => {
        var opt = objOpt();
        if (opt.isDefined) {
          var returnValue = run(opt.get);
          Log.rdebug($"{prefixedName} done: {returnValue}");
        }
        else Log.rdebug($"{prefixedName} not running: {typeof(Obj)} is None.");
      }));
    }

    public bool enabled => console.enabled;
  }
}
