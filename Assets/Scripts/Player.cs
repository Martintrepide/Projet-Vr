using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] float health = 100f;   // 100 PV au départ
    [SerializeField] Transform head;        // la Main Camera du XR Origin

    public void TakeDamage(float damage)
    {
        health -= damage;
        Debug.LogError(string.Format("Player health: {0}", health));
    }

    public Vector3 GetHeadPosition()
    {
        return head.position;
    }
}