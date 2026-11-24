using System.Text;
using TMPro;
using UnityEngine;

// Petit écran au-dessus de la manette gauche : vie, vague, ennemis restants, score, munitions.
public class WristHUD : MonoBehaviour
{
    [SerializeField] private Transform anchor;                      // vide : cherche "Left Controller"
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.09f, -0.02f);
    [SerializeField] private float refreshInterval = 0.1f;

    private Canvas canvas;
    private TextMeshProUGUI text;
    private float nextRefresh;
    private readonly StringBuilder sb = new StringBuilder();

    private void Start()
    {
        if (anchor == null)
        {
            GameObject left = GameObject.Find("Left Controller");
            if (left != null) anchor = left.transform;
        }
        canvas = UiFactory.WorldCanvas("Wrist HUD", anchor, new Vector2(0.16f, 0.1f), 2000f);
        canvas.transform.localPosition = localOffset;
        UiFactory.Panel(canvas.transform, new Color(0f, 0f, 0f, 0.6f));
        text = UiFactory.Text(canvas.transform, "Text", 22f, TextAlignmentOptions.TopLeft);
        text.richText = true;
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        Camera cam = Camera.main;
        if (anchor == null && cam != null)
            canvas.transform.position = cam.transform.position + cam.transform.forward * 0.6f + Vector3.down * 0.3f;
        if (cam != null)
            canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - cam.transform.position);

        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    private void Refresh()
    {
        GameManager gm = GameManager.Instance;
        Player p = gm != null ? gm.Player : null;
        sb.Clear();

        if (p != null)
        {
            int hp = Mathf.CeilToInt(p.Health);
            string color = p.Health / p.MaxHealth < 0.35f ? "#FF5050" : "#7CFF7C";
            sb.Append("VIE <color=").Append(color).Append('>').Append(hp).Append("</color>\n");
        }
        if (gm != null)
        {
            sb.Append("VAGUE ").Append(gm.Wave);
            if (gm.Spawner != null && gm.State == GameState.Playing)
                sb.Append("   ENNEMIS ").Append(gm.Spawner.RemainingInWave);
            sb.Append("\nSCORE ").Append(gm.Score).Append('\n');
        }

        AppendWeapon(Hand.Left, "G");
        AppendWeapon(Hand.Right, "D");
        text.text = sb.ToString();
    }

    private void AppendWeapon(Hand hand, string label)
    {
        foreach (Weapon w in Weapon.HeldWeapons)
        {
            if (w == null || w.HeldHand != hand) continue;
            sb.Append(label).Append(' ').Append(w.WeaponName).Append(' ');
            if (w.IsReloading) sb.Append("<color=#FFD040>RECHARGE</color>");
            else if (w.CurrentAmmo == 0) sb.Append("<color=#FF5050>VIDE</color>");
            else sb.Append(w.CurrentAmmo).Append('/').Append(w.MagazineSize);
            sb.Append('\n');
            return;
        }
    }
}
