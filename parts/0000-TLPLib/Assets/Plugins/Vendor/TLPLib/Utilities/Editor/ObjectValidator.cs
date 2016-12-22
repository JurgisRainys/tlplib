﻿using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using AdvancedInspector;
using com.tinylabproductions.TLPLib.Extensions;
using UnityEngine.Events;
using JetBrains.Annotations;
using com.tinylabproductions.TLPLib.Filesystem;
using com.tinylabproductions.TLPLib.Functional;
using com.tinylabproductions.TLPLib.Logger;
using com.tinylabproductions.TLPLib.validations;
using Object = UnityEngine.Object;

namespace com.tinylabproductions.TLPLib.Utilities.Editor {
  public class ObjectValidator {
    public struct Error {
      public enum Type {
        MissingComponent = 0,
        MissingReference = 1,
        NullReference = 2,
        EmptyCollection = 3,
        UnityEventInvalidMethod = 4,
        UnityEventInvalid = 5,
        TextFieldBadTag = 6
      }

      public readonly Type type;
      public readonly string message;
      public readonly Object context;

      public override string ToString() => 
        $"{nameof(Error)}[{type}, {nameof(context)}: {context}; {message}]";

      #region Constructors

      public Error(Type type, string message, Object context) {
        this.type = type;
        this.message = message;
        this.context = context;
      }

      public static Error missingComponent(Object o) => new Error(
        Type.MissingComponent,
        $"Missing Component in GO or children: {o}",
        o
      );

      public static Error emptyCollection(
        Object o, string component, string property, string context
      ) => new Error(
        Type.EmptyCollection,
        $"Collection is empty in: [{context}]{fullPath(o)}. Component: {component}, Property: {property}",
        o
      );

      public static Error missingReference(
        Object o, string component, string property, string context
      ) => new Error(
        Type.MissingReference,
        $"Missing Ref in: [{context}]{fullPath(o)}. Component: {component}, Property: {property}",
        o
      );

      public static Error nullReference(
        Object o, string component, string property, string context
      ) => new Error(
        Type.NullReference,
        $"Null Ref in: [{context}]{fullPath(o)}. Component: {component}, Property: {property}",
        o
      );

      public static Error unityEventInvalidMethod(
        Object o, string property, int number, string context
      ) => new Error(
        Type.UnityEventInvalidMethod,
        $"UnityEvent {property} callback number {number} has invalid method in [{context}]{fullPath(o)}.",
        o
      );

      public static Error unityEventInvalid(
        Object o, string property, int number, string context
      ) => new Error(
        Type.UnityEventInvalid,
        $"UnityEvent {property} callback number {number} is not valid in [{context}]{fullPath(o)}.",
        o
      );

      public static Error textFieldBadTag(
        Object o, string component, string property, string context
      ) => new Error(
        Type.TextFieldBadTag,
        $"Bad tag in: [{context}]{fullPath(o)}. Component: {component}, Property: {property}",
        o
      );

      #endregion
    }

    [MenuItem(
      "Tools/Validate Objects in Current Scene", 
      isValidateFunction: false, priority: 55
    )]
    static void checkCurrentSceneMenuItem() {
      if (EditorApplication.isPlayingOrWillChangePlaymode) {
        EditorUtility.DisplayDialog(
          "In Play Mode!",
          "This action cannot be run in play mode. Aborting!",
          "OK"
        );
        return;
      }

      var scene = SceneManager.GetActiveScene();
      var t = checkScene(
        scene,
        progress => EditorUtility.DisplayProgressBar(
          "Checking Missing References", "Please wait...", progress
        ),
        EditorUtility.ClearProgressBar
      );
      showErrors(t._1);
      if (Log.isInfo) Log.info(
        $"{scene.name} {nameof(checkCurrentSceneMenuItem)} finished in {t._2}"
      );
    }

    public static Tpl<ImmutableList<Error>, TimeSpan> checkScene(
      Scene scene, Act<float> onProgress = null, Action onFinish = null
    ) {
      var stopwatch = new Stopwatch().tap(_ => _.Start());
      var objects = getSceneObjects(scene);
      var errors = check(scene.name, objects, onProgress, onFinish);
      return F.t(errors, stopwatch.Elapsed);
    }

    public static ImmutableList<Error> checkAssetsAndDependencies(
      IEnumerable<PathStr> assets, Act<float> onProgress = null, Action onFinish = null
    ) {
      var loadedAssets = 
        assets.Select(s => AssetDatabase.LoadMainAssetAtPath(s)).ToArray();
      var dependencies = 
        EditorUtility.CollectDependencies(loadedAssets)
        .Where(x => x is GameObject || x is ScriptableObject)
        .ToImmutableList();
      return check(
        nameof(checkAssetsAndDependencies), dependencies, onProgress, onFinish
      );
    }

    public static ImmutableList<Error> check(
      string context, ICollection<Object> objects, 
      Act<float> onProgress = null, Action onFinish = null
    ) {
      var errors = ImmutableList<Error>.Empty;
      var scanned = 0;
      foreach (var o in objects) {
        var progress = (float) scanned++ / objects.Count;
        onProgress?.Invoke(progress);

        var goOpt = F.opt(o as GameObject);
        if (goOpt.isDefined) {
          var components = goOpt.get.GetComponentsInChildren<Component>();
          foreach (var c in components) {
            errors = 
              c 
              ? errors.AddRange(checkComponent(context, c))
              : errors.Add(Error.missingComponent(c));
          }
        }
        else {
          errors = errors.AddRange(checkComponent(context, o));
        }
      }

      onFinish?.Invoke();

      return errors;
    }

    public static ImmutableList<Error> checkComponent(string context, Object component) {
      var errors = ImmutableList<Error>.Empty;

      var serObj = new SerializedObject(component);
      var sp = serObj.GetIterator();

      while (sp.NextVisible(enterChildren: true)) {
        if (
          sp.propertyType == SerializedPropertyType.ObjectReference
          && sp.objectReferenceValue == null
          && sp.objectReferenceInstanceIDValue != 0
        ) errors = errors.Add(Error.missingReference(
          component, component.GetType().Name, 
          ObjectNames.NicifyVariableName(sp.name), ""
        ));

        if (sp.type == nameof(UnityEvent)) {
          foreach (var evt in getUnityEvent(component, sp.propertyPath)) {
            foreach (var err in checkUnityEvent(evt, component, sp.name, context)) {
              errors = errors.Add(err);
            }
          }
        }
      }

      var fieldErrors = validateFieldsWithAttributes(
        component,
        (field, err) => {
          var componentName = component.GetType().Name;
          return 
            err == FieldAttributeError.NullField
            ? Error.nullReference(component, componentName, field.Name, context)
            : err == FieldAttributeError.EmptyCollection
              ? Error.emptyCollection(component, componentName, field.Name, context)
              : Error.textFieldBadTag(component, componentName, field.Name, context);
        }
      );
      errors = errors.AddRange(fieldErrors);

      return errors;
    }

    static Option<Error> checkUnityEvent(
      UnityEventBase evt, Object component, string propertyName, string context
    ) {
      UnityEventReflector.rebuildPersistentCallsIfNeeded(evt);

      var persistentCalls = evt.__persistentCalls();
      var listPersistentCallOpt = persistentCalls.calls;
      if (listPersistentCallOpt.isEmpty) return Option<Error>.None;
      var listPersistentCall = listPersistentCallOpt.get;

      var index = 0;
      foreach (var persistentCall in listPersistentCall) {
        index++;

        if (persistentCall.isValid) {
          if (evt.__findMethod(persistentCall).isEmpty)
            return Error.unityEventInvalidMethod(component, propertyName, index, context).some();
        }
        else
          return Error.unityEventInvalid(component, propertyName, index, context).some();
      }

      return Option<Error>.None;
    }

    static Option<UnityEvent> getUnityEvent(object obj, string fieldName) {
      if (obj == null) return Option<UnityEvent>.None;

      var fiOpt = F.opt(obj.GetType().GetField(fieldName));
      return 
        fiOpt.isDefined 
        ? F.opt(fiOpt.get.GetValue(obj) as UnityEvent) 
        : Option<UnityEvent>.None;
    }

    enum FieldAttributeError { NullField, EmptyCollection, TextFieldBadTag }

    static IEnumerable<Error> validateFieldsWithAttributes(
      object o, Fn<FieldInfo, FieldAttributeError, Error> createError
    ) {
      var type = o.GetType();
      var fields = type.GetFields(
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
      );
      foreach (var fi in fields) {
        if (fi.hasAttributeWithProperty<TextFieldAttribute>(typeof(TextFieldType), TextFieldType.Tag)) {
          if (fi.FieldType == typeof(string)) {
            var fieldValue = (string)fi.GetValue(o);
            if (!UnityEditorInternal.InternalEditorUtility.tags.Contains(fieldValue)) {
              yield return createError(fi, FieldAttributeError.TextFieldBadTag);
            }
          }
        }
        if (
          (fi.IsPublic && !fi.hasAttribute<NonSerializedAttribute>())
          || (fi.IsPrivate && fi.hasAttribute<SerializeField>())
        ) {
          var fieldValue = fi.GetValue(o);
          var hasNotNull = fi.hasAttribute<NotNullAttribute>();
          if (fieldValue == null) {
            if (hasNotNull) yield return createError(fi, FieldAttributeError.NullField);
          }
          else {
            var listOpt = F.opt(fieldValue as IList);
            if (listOpt.isDefined) {
              var list = listOpt.get;
              if (list.Count == 0 && fi.hasAttribute<NonEmptyAttribute>()) {
                yield return createError(fi, FieldAttributeError.EmptyCollection);
              }
              foreach (
                var _err in validateFieldsWithAttributes(list, fi, hasNotNull, createError)
              ) yield return _err;
            }
            else {
              var fieldType = fi.FieldType;
              // Check non-primitive serialized fields.
              if (
                !fieldType.IsPrimitive
                && fieldType.hasAttribute<SerializableAttribute>()
              ) {
                foreach (var _err in validateFieldsWithAttributes(fieldValue, createError))
                  yield return _err;
              }
            }
          }
        }
      }
    }

    static readonly Type unityObjectType = typeof(Object);

    static IEnumerable<Error> validateFieldsWithAttributes(
      IList list, FieldInfo listFieldInfo, bool hasNotNull, Fn<FieldInfo, FieldAttributeError, Error> createError
    ) {
      var listItemType = listFieldInfo.FieldType.GetElementType();
      var listItemIsUnityObject = unityObjectType.IsAssignableFrom(listItemType);

      if (listItemIsUnityObject) {
        if (hasNotNull && list.Contains(null)) yield return createError(listFieldInfo, FieldAttributeError.NullField);
      }
      else {
        foreach (var listItem in list)
          foreach (var _err in validateFieldsWithAttributes(listItem, createError))
            yield return _err;
      }
    }

    static ImmutableList<Object> getSceneObjects(Scene scene) => 
      scene.GetRootGameObjects()
      .Where(go => go.hideFlags == HideFlags.None)
      .Cast<Object>()
      .ToImmutableList();

    static void showErrors(IEnumerable<Error> errors) {
      foreach (var error in errors)
        if (Log.isError) Log.error(error.message, error.context);
    }

    static string fullPath(Object o) {
      var go = o as GameObject;
      if (go)  
        return go.transform.parent == null
          ? go.name
          : fullPath(go.transform.parent.gameObject) + "/" + go.name;

      return o.name;
    }
  }
}