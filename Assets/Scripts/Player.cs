using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// =====================================================================
//  Player – vie du joueur, position de la tête, affichage et Game Over
//  À placer sur la racine « XR Origin (XR Rig) ».
// =====================================================================
public class Player : MonoBehaviour
{
    [SerializeField] float health = 100f;
    [Tooltip("La Main Camera du XR Origin (= la tête du joueur)")]
    [SerializeField] Transform head;

    [Header("Affichage (facultatif)")]
    [Tooltip("Texte TextMeshPro qui affiche la vie, par exemple sur la manette gauche")]
    [SerializeField] TMP_Text healthText;

    [Tooltip("Couleur normale du texte de vie")]
    [SerializeField] Color normalColor = Color.white;
    [Tooltip("Couleur du texte quand on est touché")]
    [SerializeField] Color hitColor = Color.red;

    [Header("Mort")]
    [Tooltip("Secondes avant de relancer la partie après la mort")]
    [SerializeField] float restartDelay = 3f;

    private bool isDead;
    public bool IsDead => isDead;

    private void Awake()
    {
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
        UpdateHealthText();
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        health = Mathf.Max(0f, health - damage);
        Debug.Log("Player health: " + health);
        UpdateHealthText();
        FlashHit();

        if (health <= 0f)
            Die();
    }

    public Vector3 GetHeadPosition()
    {
        return head.position;
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("GAME OVER");
        if (healthText != null)
        {
            healthText.text = "GAME OVER";
            healthText.color = hitColor;
        }
        Invoke(nameof(Restart), restartDelay);
    }

    private void Restart()
    {
        // La scène doit être dans File > Build Profiles > Scene List
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Le texte passe en rouge une fraction de seconde à chaque tir reçu
    private void FlashHit()
    {
        if (healthText == null)
            return;
        healthText.color = hitColor;
        CancelInvoke(nameof(ResetColor));
        Invoke(nameof(ResetColor), 0.2f);
    }

    private void ResetColor()
    {
        if (healthText != null && !isDead)
            healthText.color = normalColor;
    }

    private void UpdateHealthText()
    {
        if (healthText != null)
            healthText.text = "Vie : " + Mathf.CeilToInt(health);
    }
}