using UnityEngine;

// Fait évoluer la musique (asset Dynamic Music) avec la partie :
// partie 0 au menu, puis une partie plus intense à chaque vague.
[RequireComponent(typeof(DynamicPlayer))]
public class MusicDirector : MonoBehaviour
{
    [SerializeField] private int menuPart = 0;
    [SerializeField] private int firstWavePart = 1;

    private DynamicPlayer music;
    private GameManager gm;
    private int partCount;

    private void Start()
    {
        music = GetComponent<DynamicPlayer>();
        partCount = music.musicSet != null && music.musicSet.audioClips != null ? music.musicSet.audioClips.Length : 0;
        gm = GameManager.Instance;
        if (gm == null || partCount == 0 || !music.musicSet.partSet)
            return;

        gm.StateChanged += OnStateChanged;
        if (gm.Spawner != null) gm.Spawner.WaveStarted += OnWaveStarted;
        OnStateChanged(gm.State);
    }

    private void OnDestroy()
    {
        if (gm == null) return;
        gm.StateChanged -= OnStateChanged;
        if (gm.Spawner != null) gm.Spawner.WaveStarted -= OnWaveStarted;
    }

    private void OnStateChanged(GameState s)
    {
        if (s != GameState.Playing) music.SwitchParts(Mathf.Clamp(menuPart, 0, partCount - 1));
    }

    private void OnWaveStarted(int wave)
    {
        music.SwitchParts(Mathf.Clamp(firstWavePart + wave - 1, 0, partCount - 1));
    }
}
