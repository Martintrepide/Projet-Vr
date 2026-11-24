using UnityEngine;
using UnityEngine.UI;

// Retour visuel et haptique des dégâts : flash rouge, vignette quand la vie est basse, vibrations.
public class DamageFeedback : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private Color hurtColor = new Color(0.8f, 0f, 0f, 1f);
    [SerializeField] private float flashAlpha = 0.6f;
    [SerializeField] private float flashFadeSpeed = 2.5f;
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.4f;

    private Image vignette;
    private float flash;

    private void Start()
    {
        if (player == null) player = FindAnyObjectByType<Player>();
        Camera cam = Camera.main;
        if (player == null || cam == null)
        {
            enabled = false;
            return;
        }

        GameObject go = new GameObject("Damage Overlay", typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = cam.nearClipPlane + 0.05f;
        canvas.sortingOrder = 100;

        GameObject imgGo = new GameObject("Vignette", typeof(RectTransform));
        imgGo.transform.SetParent(go.transform, false);
        UiFactory.Stretch((RectTransform)imgGo.transform);
        vignette = imgGo.AddComponent<Image>();
        vignette.sprite = UiFactory.VignetteSprite();
        vignette.raycastTarget = false;
        vignette.color = Color.clear;

        player.Damaged += OnDamaged;
        player.Died += OnDied;
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.Damaged -= OnDamaged;
            player.Died -= OnDied;
        }
    }

    private void OnDamaged(float amount)
    {
        flash = flashAlpha;
        Haptics.Pulse(Hand.Unknown, 0.7f, 0.12f);
        GameSfx.Play(GameSfx.Hurt, player.GetHeadPosition(), 0.8f, 0.05f, 0f);
    }

    private void OnDied()
    {
        flash = 1f;
        Haptics.Pulse(Hand.Unknown, 1f, 0.4f);
    }

    private void Update()
    {
        if (vignette == null) return;
        float ratio = player.MaxHealth > 0f ? player.Health / player.MaxHealth : 1f;
        float low = ratio < lowHealthThreshold ? (1f - ratio / lowHealthThreshold) * 0.45f : 0f;
        if (player.IsDead) low = 0.85f;
        flash = Mathf.MoveTowards(flash, 0f, flashFadeSpeed * Time.deltaTime);
        Color c = hurtColor;
        c.a = Mathf.Max(flash, low);
        vignette.color = c;
    }
}
