using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Construit les interfaces par code (pas de prefab à maintenir).
public static class UiFactory
{
    // Canvas en espace monde : sizeMeters = taille physique, pixelsPerMeter = résolution.
    public static Canvas WorldCanvas(string name, Transform parent, Vector2 sizeMeters, float pixelsPerMeter = 1000f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = sizeMeters * pixelsPerMeter;
        rt.localScale = Vector3.one / pixelsPerMeter;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        return canvas;
    }

    public static Image Panel(Transform parent, Color color)
    {
        GameObject go = new GameObject("Background", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, float fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        Stretch(rt);
        rt.offsetMin = new Vector2(12f, 8f);
        rt.offsetMax = new Vector2(-12f, -8f);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Texture de vignette : transparente au centre, opaque sur les bords.
    public static Sprite VignetteSprite(int size = 256)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1.05f, d));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
