﻿using AdvancedInspector;
using com.tinylabproductions.TLPLib.Components.Interfaces;
using com.tinylabproductions.TLPLib.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace com.tinylabproductions.TLPLib.Components.gradient {

  [RequireComponent(typeof(Image))]
  public class GradientTexture: MonoBehaviour, IMB_Start {

    [SerializeField] int textureSize = 128;
    [SerializeField] Image textureTarget;
    [SerializeField] Gradient gradient = new Gradient();
    [SerializeField] Direction direction = Direction.Horizontal;

    enum Direction { Vertical, Horizontal }

    [Inspect]
    void generate() {

      var texture = new Texture2D(textureSize, textureSize, TextureFormat.ARGB32, false);
      var pixels = new Color[textureSize * textureSize];

      if (direction == Direction.Horizontal)
        for (int x = 0; x < textureSize; x++) {
          var c = gradient.Evaluate(x / (float) textureSize);
          for (int y = 0; y < textureSize; y++) {
            pixels[x + y * textureSize] = c;
          }
        }
      else if (direction == Direction.Vertical)
        for (int y = 0; y < textureSize; y++) {
          var c = gradient.Evaluate(y / (float) textureSize);
          for (int x = 0; x < textureSize; x++) {
            pixels[x + y * textureSize] = c;
          }
        }

      texture.SetPixels(pixels);
      texture.Apply();
      textureTarget.sprite = texture.toSprite();
    }

    public void Start() => generate();
  }

}