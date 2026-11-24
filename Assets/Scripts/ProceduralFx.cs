using System.Collections.Generic;
using UnityEngine;

// Effets visuels générés par code : flash de bouche, traçantes, impacts et trous de balle.
// Le matériau vient de Resources/VRShooterFX (créé par le menu VR Shooter) pour être inclus dans le build.
public static class ProceduralFx
{
    static Material material;
    static Sprite flashSprite, dotSprite;
    static readonly Queue<GameObject> holes = new Queue<GameObject>();
    const int MaxHoles = 60;

    public static Material Material
    {
        get
        {
            if (material == null)
            {
                material = Resources.Load<Material>("VRShooterFX");
                if (material == null) material = new Material(Shader.Find("Sprites/Default"));
            }
            return material;
        }
    }

    public static void MuzzleFlash(Vector3 position, Vector3 forward, float size)
    {
        GameObject go = new GameObject("Muzzle Flash");
        go.transform.position = position + forward * size * 0.3f;
        Camera cam = Camera.main;
        Vector3 toCam = cam != null ? cam.transform.position - go.transform.position : -forward;
        go.transform.rotation = Quaternion.LookRotation(-toCam) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        go.transform.localScale = Vector3.one * size * Random.Range(0.8f, 1.2f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = FlashSprite;
        sr.sharedMaterial = Material;
        sr.color = new Color(1f, 0.85f, 0.45f, 1f);

        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.75f, 0.4f);
        light.intensity = 3f;
        light.range = 4f;
        light.shadows = LightShadows.None;

        Object.Destroy(go, 0.05f);
    }

    public static void Tracer(Vector3 from, Vector3 to, Color color, float width, float duration)
    {
        GameObject go = new GameObject("Tracer");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = width;
        lr.endWidth = width * 0.4f;
        lr.sharedMaterial = Material;
        lr.startColor = color;
        Color end = color;
        end.a = 0.1f;
        lr.endColor = end;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.Destroy(go, duration);
    }

    public static void AddTrail(GameObject target, Color color, float width, float time)
    {
        TrailRenderer tr = target.AddComponent<TrailRenderer>();
        tr.sharedMaterial = Material;
        tr.time = time;
        tr.startWidth = width;
        tr.endWidth = 0f;
        tr.startColor = color;
        Color end = color;
        end.a = 0f;
        tr.endColor = end;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.minVertexDistance = 0.05f;
    }

    // Gerbe de sang instantanée (s'ajoute à l'effet de particules de l'ennemi).
    public static void Blood(Vector3 point, Vector3 towardShooter)
    {
        for (int i = 0; i < 3; i++)
        {
            GameObject go = new GameObject("Blood Puff");
            go.transform.position = point + towardShooter.normalized * 0.05f + Random.insideUnitSphere * 0.04f;
            Camera cam = Camera.main;
            Vector3 toCam = cam != null ? cam.transform.position - go.transform.position : towardShooter;
            go.transform.rotation = Quaternion.LookRotation(-toCam) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            go.transform.localScale = Vector3.one * Random.Range(0.12f, 0.22f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DotSprite;
            sr.sharedMaterial = Material;
            sr.color = new Color(0.55f, 0f, 0f, 0.9f);
            Object.Destroy(go, 0.12f);
        }
    }

    // Étincelle + trou de balle sur une surface.
    public static void Impact(Vector3 point, Vector3 normal)
    {
        if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;

        GameObject spark = new GameObject("Impact Spark");
        spark.transform.position = point + normal * 0.02f;
        spark.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        spark.transform.localScale = Vector3.one * 0.12f;
        SpriteRenderer s = spark.AddComponent<SpriteRenderer>();
        s.sprite = FlashSprite;
        s.sharedMaterial = Material;
        s.color = new Color(1f, 0.7f, 0.3f, 1f);
        Object.Destroy(spark, 0.06f);

        GameObject hole = new GameObject("Bullet Hole");
        hole.transform.position = point + normal * 0.004f;
        hole.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        hole.transform.localScale = Vector3.one * 0.05f;
        SpriteRenderer h = hole.AddComponent<SpriteRenderer>();
        h.sprite = DotSprite;
        h.sharedMaterial = Material;
        h.color = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        holes.Enqueue(hole);
        while (holes.Count > MaxHoles)
        {
            GameObject old = holes.Dequeue();
            if (old != null) Object.Destroy(old);
        }
        Object.Destroy(hole, 15f);
    }

    static Sprite FlashSprite
    {
        get
        {
            if (flashSprite == null) flashSprite = MakeSprite(64, true);
            return flashSprite;
        }
    }

    static Sprite DotSprite
    {
        get
        {
            if (dotSprite == null) dotSprite = MakeSprite(32, false);
            return dotSprite;
        }
    }

    // Étoile lumineuse (flash) ou disque doux (trou de balle).
    static Sprite MakeSprite(int size, bool star)
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
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a;
                if (star)
                {
                    float core = Mathf.Clamp01(1f - r * 2.2f);
                    float angle = Mathf.Atan2(dy, dx);
                    float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 3f)), 12f) * Mathf.Clamp01(1f - r);
                    a = Mathf.Clamp01(core + rays * 0.9f);
                }
                else
                {
                    a = Mathf.Clamp01((1f - r) * 3f);
                }
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
