using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Arme de base : saisie, munitions, rechargement, recul, effets.
// Rechargement : bouton A ou X pendant que l'arme est tenue (automatique si le chargeur est vide).
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class Weapon : MonoBehaviour
{
    [SerializeField] protected float shootingForce;
    [SerializeField] protected Transform bulletSpawn;
    [SerializeField] private float recoilForce;
    [SerializeField] private float damage;

    [Header("Identité")]
    [SerializeField] protected string weaponName = "Pistolet";

    [Header("Munitions")]
    [SerializeField] protected int magazineSize = 12;
    [SerializeField] protected float reloadTime = 1.2f;
    [SerializeField] protected bool autoReloadWhenEmpty = true;
    [SerializeField] protected float fireCooldown = 0.15f;

    [Header("Effets")]
    [SerializeField] protected GameObject muzzleFlashPrefab;      // optionnel : s'ajoute au flash généré
    [SerializeField] protected float muzzleFlashSize = 0.12f;
    [SerializeField] protected AudioClip shotSound;
    [SerializeField] protected AudioClip emptySound;
    [SerializeField] protected AudioClip reloadSound;
    [SerializeField, Range(0f, 1f)] protected float hapticAmplitude = 0.6f;
    [SerializeField] protected float hapticDuration = 0.08f;

    public static readonly List<Weapon> HeldWeapons = new List<Weapon>();

    public string WeaponName => weaponName;
    public int CurrentAmmo { get; protected set; }
    public int MagazineSize => magazineSize;
    public bool IsReloading { get; private set; }
    public bool IsHeld => interactor != null;
    public Hand HeldHand { get; private set; }
    protected bool TriggerHeld { get; private set; }

    private Rigidbody rigidBody;
    private XRGrabInteractable interactableWeapon;
    private IXRSelectInteractor interactor;
    private InputAction reloadAction;
    private float lastShotTime = -999f;

    protected virtual void Awake()
    {
        interactableWeapon = GetComponent<XRGrabInteractable>();
        rigidBody = GetComponent<Rigidbody>();
        CurrentAmmo = Mathf.Max(1, magazineSize);
        if (bulletSpawn == null)
            bulletSpawn = transform;
        SetupInteractableWeaponEvents();

        reloadAction = new InputAction("Reload", InputActionType.Button);
        reloadAction.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
        reloadAction.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
        reloadAction.performed += OnReloadPressed;
    }

    protected virtual void OnDestroy()
    {
        HeldWeapons.Remove(this);
        if (reloadAction != null)
        {
            reloadAction.performed -= OnReloadPressed;
            reloadAction.Dispose();
        }
    }

    private void SetupInteractableWeaponEvents()
    {
        interactableWeapon.selectEntered.AddListener(PickUpWeapon);
        interactableWeapon.selectExited.AddListener(DropWeapon);
        interactableWeapon.activated.AddListener(StartShooting);
        interactableWeapon.deactivated.AddListener(StopShooting);
    }

    private void PickUpWeapon(SelectEnterEventArgs args)
    {
        interactor = args.interactorObject;
        HeldHand = Haptics.HandFromTransform(args.interactorObject.transform);
        MeshHidder hidder = args.interactorObject.transform.GetComponentInParent<MeshHidder>();
        if (hidder != null) hidder.Hide();
        if (!HeldWeapons.Contains(this)) HeldWeapons.Add(this);
        reloadAction.Enable();
    }

    private void DropWeapon(SelectExitEventArgs args)
    {
        MeshHidder hidder = args.interactorObject.transform.GetComponentInParent<MeshHidder>();
        if (hidder != null) hidder.Show();
        interactor = null;
        HeldHand = Hand.Unknown;
        TriggerHeld = false;
        HeldWeapons.Remove(this);
        reloadAction.Disable();
        OnTriggerReleased();
    }

    protected virtual void StartShooting(ActivateEventArgs args)
    {
        TriggerHeld = true;
        OnTriggerPulled();
    }

    protected virtual void StopShooting(DeactivateEventArgs args)
    {
        TriggerHeld = false;
        OnTriggerReleased();
    }

    // Appelé quand la gâchette est enfoncée / relâchée. Les armes concrètes surchargent ces méthodes.
    protected virtual void OnTriggerPulled() { }
    protected virtual void OnTriggerReleased() { }

    // Vérifie cadence, rechargement et munitions ; consomme une balle si le tir est possible.
    protected bool TryConsumeAmmo()
    {
        if (IsReloading || Time.time - lastShotTime < fireCooldown)
            return false;
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
            return false;

        if (CurrentAmmo <= 0)
        {
            GameSfx.Play(emptySound != null ? emptySound : GameSfx.Click, bulletSpawn.position, 0.6f, 0f);
            Haptics.Pulse(HeldHand, 0.15f, 0.03f);
            if (autoReloadWhenEmpty)
                Reload();
            return false;
        }

        CurrentAmmo--;
        lastShotTime = Time.time;
        return true;
    }

    protected virtual void Shoot()
    {
        PlayShotEffects();
    }

    protected void PlayShotEffects()
    {
        ApplyRecoil();
        ProceduralFx.MuzzleFlash(bulletSpawn.position, bulletSpawn.forward, muzzleFlashSize);
        if (muzzleFlashPrefab != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, bulletSpawn.position, bulletSpawn.rotation, bulletSpawn);
            Destroy(flash, 1f);
        }
        GameSfx.Play(shotSound != null ? shotSound : DefaultShotSound(), bulletSpawn.position, 0.9f);
        Haptics.Pulse(HeldHand, hapticAmplitude, hapticDuration);
    }

    protected virtual AudioClip DefaultShotSound()
    {
        return GameSfx.Gunshot;
    }

    private void OnReloadPressed(InputAction.CallbackContext ctx)
    {
        Reload();
    }

    public void Reload()
    {
        if (IsReloading || CurrentAmmo >= magazineSize || !isActiveAndEnabled)
            return;
        StartCoroutine(ReloadCO());
    }

    private IEnumerator ReloadCO()
    {
        IsReloading = true;
        GameSfx.Play(reloadSound != null ? reloadSound : GameSfx.Reload, transform.position, 0.7f, 0f);
        Haptics.Pulse(HeldHand, 0.25f, 0.05f);
        yield return new WaitForSeconds(reloadTime);
        CurrentAmmo = magazineSize;
        IsReloading = false;
        Haptics.Pulse(HeldHand, 0.35f, 0.05f);
    }

    private void ApplyRecoil()
    {
        rigidBody.AddRelativeForce(Vector3.back * recoilForce, ForceMode.Impulse);
    }

    public float GetShootingForce()
    {
        return shootingForce;
    }

    public float GetDamage()
    {
        return damage;
    }
}
