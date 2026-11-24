using UnityEngine;

// Sons générés par code : aucun fichier audio nécessaire.
// Pour utiliser vos propres sons, renseignez les champs AudioClip des armes.
public static class GameSfx
{
    const int Rate = 44100;
    static AudioClip gunshot, shotgun, enemyShot, click, reload, hurt;

    public static AudioClip Gunshot { get { if (gunshot == null) gunshot = Noise("Gunshot", 0.25f, 18f, 0.9f); return gunshot; } }
    public static AudioClip Shotgun { get { if (shotgun == null) shotgun = Noise("Shotgun", 0.45f, 9f, 1f); return shotgun; } }
    public static AudioClip EnemyShot { get { if (enemyShot == null) enemyShot = Noise("EnemyShot", 0.2f, 22f, 0.6f); return enemyShot; } }
    public static AudioClip Click { get { if (click == null) click = Tone("Click", 0.03f, 2200f, 120f, 0.5f); return click; } }
    public static AudioClip Reload { get { if (reload == null) reload = DoubleClick(); return reload; } }
    public static AudioClip Hurt { get { if (hurt == null) hurt = Sweep("Hurt", 0.22f, 180f, 60f, 0.8f); return hurt; } }

    public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitchJitter = 0.08f, float spatial = 1f)
    {
        if (clip == null) return;
        GameObject go = new GameObject("SFX_" + clip.name);
        go.transform.position = position;
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        src.spatialBlend = spatial;
        src.minDistance = 2f;
        src.maxDistance = 60f;
        src.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.5f, src.pitch) + 0.1f);
    }

    static AudioClip Noise(string name, float length, float decay, float gain)
    {
        int n = (int)(Rate * length);
        float[] data = new float[n];
        float low = 0f;
        System.Random rng = new System.Random(name.GetHashCode());
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += 0.25f * (white - low);
            float env = Mathf.Exp(-decay * t);
            float thump = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-30f * t);
            data[i] = gain * env * (0.6f * low + 0.3f * white * Mathf.Exp(-60f * t) + 0.5f * thump);
        }
        return Make(name, data);
    }

    static AudioClip Tone(string name, float length, float freq, float decay, float gain)
    {
        int n = (int)(Rate * length);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            data[i] = gain * Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-decay * t);
        }
        return Make(name, data);
    }

    static AudioClip Sweep(string name, float length, float f0, float f1, float gain)
    {
        int n = (int)(Rate * length);
        float[] data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float k = (float)i / n;
            phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, k) / Rate;
            data[i] = gain * Mathf.Sin(phase) * (1f - k);
        }
        return Make(name, data);
    }

    static AudioClip DoubleClick()
    {
        AudioClip a = Click;
        float[] one = new float[a.samples];
        a.GetData(one, 0);
        int gap = (int)(Rate * 0.12f);
        float[] data = new float[gap + one.Length];
        for (int i = 0; i < one.Length; i++)
        {
            data[i] += one[i];
            data[gap + i] += one[i] * 0.8f;
        }
        return Make("Reload", data);
    }

    static AudioClip Make(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
