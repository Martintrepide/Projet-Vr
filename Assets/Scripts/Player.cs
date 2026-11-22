using UnityEngine;

// =====================================================================
//  PARTIE 2 – Le joueur (points de vie + position de la tête)
//  D'après GameDevChef « VRShooter », adapté à Unity 6000.3.
//
//  À placer sur la racine « XR Origin (XR Rig) ».
//  Head = la « Main Camera » (Camera Offset > Main Camera).
// =====================================================================
public class Player : MonoBehaviour
{
    [SerializeField] private float health = 100f;
    [Tooltip("La Main Camera du XR Origin (= la tête du joueur)")]
    [SerializeField] private Transform head;

    public float Health => health;

    private void Awake()
    {
        // Ajout : si on a oublié de remplir « Head », on prend la caméra principale
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
        // La vidéo utilise Debug.LogError ; Debug.Log suffit (et ne met pas le jeu
        // en pause si l'option « Error Pause » de la Console est activée)
        Debug.Log($"Player health: {health}");

        if (health <= 0)
        {
            Debug.Log("Le joueur est mort ! (à vous d'imaginer la suite : écran Game Over, restart…)");
        }
    }

    public Vector3 GetHeadPosition()
    {
        return head.position;
    }
}