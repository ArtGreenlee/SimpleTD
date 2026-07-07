using System;
using System.Collections.Generic;
using UnityEngine;

public class ShapeHelper : MonoBehaviour
{
    public enum Shape
    {
        Square,
        Circle,
        Hex,
        Triangle,
        NineSliced
    }

    [Serializable]
    public struct ShapeData
    {
        public Shape shape;
        public Sprite sprite;
    }

    public List<ShapeData> shapeData;

    private Dictionary<Shape, Sprite> sprites = new Dictionary<Shape, Sprite>();

    void Awake()
    {
        sprites.Clear();
        if (shapeData == null || shapeData.Count == 0) return;

        for (int i = 0; i < shapeData.Count; i++)
        {
            var data = shapeData[i];
            if (data.sprite == null) continue;
            sprites[data.shape] = data.sprite;
        }
    }

    public Sprite GetSprite(Shape shape)
    {
        if (sprites.TryGetValue(shape, out var sprite))
        {
            return sprite;
        }
        return null;
    }
}
