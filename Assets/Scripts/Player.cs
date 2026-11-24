using System;
using UnityEngine;

// Joueur : points de vie, régénération, mort.
// Placé sur la racine du XR Origin. "head" pointe vers la Main Camera.
public class Player : MonoBehaviour
{
    [SerializeField] float health;
    [SerializeField] float maxHealth = 100f;
    [SerializeField] Transform head;

    [Header("Régénération")]
    [SerializeField] float regenDelay = 4f;
    [SerializeField] float regenPerSecond = 8f;

    public event Action<float> Damaged;
    public event Action Died;

    public float Health => health;
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    float lastDamageTime = -999f;

    private void Awake()
    {
        if (head == null && Camera.main != null)
            head = Camera.main.transform;
        if (maxHealth <= 0f)
            maxHealth = health > 0f ? health : 100f;
        health = maxHealth;
    }

    private void Update()
    {
        if (IsDead || health >= maxHealth)
            return;
        if (Time.time - lastDamageTime >= regenDelay)
            health = Mathf.Min(maxHealth, health + regenPerSecond * Time.deltaTime);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f)
            return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            return;

        health = Mathf.Max(0f, health - damage);
        lastDamageTime = Time.time;
        Damaged?.Invoke(damage);

        if (health <= 0f)
        {
            IsDead = true;
            Died?.Invoke();
        }
    }

    public void ResetHealth()
    {
        IsDead = false;
        health = maxHealth;
    }

    public Vector3 GetHeadPosition()
    {
        return head != null ? head.position : transform.position + Vector3.up * 1.6f;
    }

    public Transform Head => head != null ? head : transform;
}
