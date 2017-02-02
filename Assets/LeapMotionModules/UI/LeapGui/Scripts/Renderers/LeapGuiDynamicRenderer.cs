﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Leap.Unity.Query;
using Leap.Unity.Attributes;

public class LeapGuiDynamicRenderer : LeapGuiRenderer,
  ISupportsFeature<LeapGuiMeshFeature> {

  [SerializeField]
  private MotionType _motionType = MotionType.Translation;

  [SerializeField]
  private Shader _shader;

  [MinValue(0)]
  [SerializeField]
  private int _borderAmount;


  private List<Mesh> _elementMeshes = new List<Mesh>();
  private Material _material;

  //Feature lists
  private List<LeapGuiMeshFeature> _meshFeatures = new List<LeapGuiMeshFeature>();
  private List<LeapGuiTextureFeature> _textureFeatures = new List<LeapGuiTextureFeature>();

  //Textures
  private Dictionary<UVChannelFlags, Rect[]> _atlasedRects;

  public void GetSupportInfo(List<LeapGuiMeshFeature> features, List<FeatureSupportInfo> info) { }


  public override void OnEnableRenderer() {
    throw new NotImplementedException();
  }

  public override void OnDisableRenderer() {
    throw new NotImplementedException();
  }

  public override void OnUpdateRenderer() {
    if (gui.space is LeapGuiCylindricalSpace) {
      var cylindricalSpace = gui.space as LeapGuiCylindricalSpace;

      for (int i = 0; i < _elementMeshes.Count; i++) {
        var element = gui.elements[i];
        var parameters = cylindricalSpace.GetElementParameters(element.anchor, element.transform.position);
        var pos = cylindricalSpace.TransformPoint(element, gui.transform.InverseTransformPoint(element.transform.position));

        Quaternion rot = Quaternion.Euler(0, parameters.angleOffset * Mathf.Rad2Deg, 0);

        Graphics.DrawMesh(_elementMeshes[i], pos, rot, _material, 0);
      }
    }
  }

  public override void OnEnableRendererEditor() {
    throw new NotImplementedException();
  }

  public override void OnDisableRendererEditor() {
    throw new NotImplementedException();
  }

  public override void OnUpdateRendererEditor() {
    ensureObjectsAreValid();

    if (gui.GetSupportedFeatures(_meshFeatures)) {
      Profiler.BeginSample("Bake Verts");
      bakeVerts();
      Profiler.EndSample();

      Profiler.BeginSample("Bake Colors");
      bakeColors();
      Profiler.EndSample();
    }

    if (gui.GetSupportedFeatures(_textureFeatures)) {
      Profiler.BeginSample("Atlas Textures");
      atlasTextures();
      Profiler.EndSample();
    }

    OnUpdateRenderer();
  }

  private void ensureObjectsAreValid() {
    //Destroy meshes that will no longer be used
    while (_elementMeshes.Count > gui.elements.Count) {
      DestroyImmediate(_elementMeshes.RemoveLast());
    }

    //Clear out the meshes that remain
    foreach (var mesh in _elementMeshes) {
      mesh.Clear();
    }

    //Create new meshes that need to exist
    while (_elementMeshes.Count < gui.elements.Count) {
      Mesh elementMesh = new Mesh();
      elementMesh.MarkDynamic();
      elementMesh.name = "Generated Element Mesh";
      _elementMeshes.Add(elementMesh);
    }

    if (_shader == null) {
      _shader = Shader.Find("LeapGui/Defaults/Dynamic");
    }

    if (_material != null) {
      DestroyImmediate(_material);
    }

    _material = new Material(_shader);
    _material.name = "Dynamic Gui Material";
  }

  private List<Vector3> _tempVertList = new List<Vector3>();
  private List<int> _tempTriList = new List<int>();
  private void bakeVerts() {
    for (int i = 0; i < gui.elements.Count; i++) {
      _tempVertList.Clear();
      _tempTriList.Clear();

      foreach (var meshFeature in _meshFeatures) {
        var elementData = meshFeature.data[i];
        var mesh = elementData.mesh;
        if (mesh == null) continue;

        var topology = MeshCache.GetTopology(elementData.mesh);

        int vertOffset = _tempVertList.Count;
        for (int j = 0; j < topology.tris.Length; j++) {
          _tempTriList.Add(topology.tris[j] + vertOffset);
        }

        _tempVertList.AddRange(topology.verts);
      }

      int min = int.MaxValue, max = int.MinValue;
      foreach (var index in _tempTriList) {
        min = Mathf.Min(index, min);
        max = Mathf.Max(index, max);
      }
      Debug.Log(":::: " + min + " : " + max);

      _elementMeshes[i].SetVertices(_tempVertList);
      _elementMeshes[i].SetTriangles(_tempTriList, 0);

      Debug.Log(_elementMeshes[i].vertexCount);
    }
  }

  private List<Color> _tempColorList = new List<Color>();
  private void bakeColors() {
    //If no mesh feature wants colors, don't bake them!
    if (!_meshFeatures.Query().Any(f => f.color)) {
      return;
    }

    for (int i = 0; i < gui.elements.Count; i++) {
      _tempColorList.Clear();

      foreach (var meshFeature in _meshFeatures) {
        var elementData = meshFeature.data[i];
        var mesh = elementData.mesh;
        if (mesh == null) continue;

        Color totalTint = elementData.color * meshFeature.tint;

        var colors = MeshCache.GetColors(mesh);
        if (colors != null) {
          colors.Query().Select(c => c * totalTint).AppendList(_tempColorList);
        } else {
          _tempColorList.Append(mesh.vertexCount, totalTint);
        }
      }

      _elementMeshes[i].SetColors(_tempColorList);
    }

    _material.EnableKeyword(LeapGuiMeshFeature.COLORS_FEATURE);
  }

  private void atlasTextures() {
    Texture2D[] atlasTextures;
    AtlasUtil.DoAtlas(_textureFeatures, _borderAmount,
                  out atlasTextures,
                  out _atlasedRects);

    for (int i = 0; i < _textureFeatures.Count; i++) {
      _material.SetTexture(_textureFeatures[i].propertyName, atlasTextures[i]);
    }
  }

  public enum MotionType {
    Translation,
    Full
  }




}
