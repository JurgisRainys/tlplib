﻿using UnityEngine;

namespace com.tinylabproductions.TLPLib.Tween.fun_tween.serialization.tweeners {
  [AddComponentMenu("")]
  public class Transform_LocalScale : SerializedTweener<Vector3, Transform> {
    public Transform_LocalScale() : base(
      TweenOps.vector3, SerializedTweenerOps.Add.vector3, SerializedTweenerOps.Extract.localScale,
      TweenMutators.localScale, Defaults.vector3
    ) { }
  }
}