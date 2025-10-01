using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class GunController : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public Transform firePoint;
    public Transform recoilAnchor;
    public ParticleSystem muzzleFlashPrefab;
    public GameObject bulletImpactPrefab;
    public LineRenderer bulletTrailPrefab;

    [Header("Firing Settings")]
    public bool automatic = true;

    [Tooltip("Rounds Per Minute")]
    public float roundsPerMinute = 600f; // <--- RPM now
    public int magazineSize = 30;
    public float reloadTime = 2f;

    [Header("Damage Settings")]
    public float damage = 20f;
    public float range = 100f;

    [Header("Spread Settings")]
    public float hipfireSpread = 5f;
    public float adsSpread = 1f;
    public bool isAiming = false;

    [Header("Camera Recoil Settings")]
    public float hipfireRecoilVertical = 3f;
    public float hipfireRecoilHorizontal = 1f;
    public float aimRecoilVertical = 1.5f;
    public float aimRecoilHorizontal = 0.5f;
    public float recoilReturnSpeed = 10f;
    public float recoilSnappiness = 15f;

    [Header("Recoil Pattern")]
    public Vector2[] recoilPattern;
    public bool usePattern = false;

    [Header("Camera Shake")]
    public float shakeMagnitude = 0.05f;
    public float maxShakeDuration = 0.1f;

    [Header("Other")]
    public LayerMask hitMask;
    public Animator anim;

    private bool fireHeld;
    private bool reloadPressed;
    private bool adsHeld;

    private int currentAmmo;
    private float nextTimeToFire;
    private bool isReloading;

    // recoil internals
    private Vector2 currentRecoilOffset;
    private Vector2 recoilVelocity;
    private Vector2 recoilTargetOffset;
    private float shakeTimer;
    private int patternIndex;
    private Vector3 baseLocalPos;

    void Start()
    {
        currentAmmo = magazineSize;
        if (playerCamera != null)
            baseLocalPos = playerCamera.transform.localPosition;

        if (recoilAnchor == null && playerCamera != null)
        {
            GameObject anchor = new GameObject("CameraRecoil");
            anchor.transform.SetParent(playerCamera.transform.parent, false);
            recoilAnchor = anchor.transform;
            playerCamera.transform.SetParent(recoilAnchor, true);
        }
    }

    void Update()
    {
        HandleCameraRecoilReturn();

        if (isReloading) return;

        // Fire
        if (automatic)
        {
            if (fireHeld && Time.time >= nextTimeToFire) TryShoot();
        }
        else
        {
            if (fireHeld && Time.time >= nextTimeToFire)
            {
                TryShoot();
                fireHeld = false;
            }
        }

        // Reload
        if (reloadPressed)
        {
            StartCoroutine(Reload());
            reloadPressed = false;
        }

        // ADS
        isAiming = adsHeld;
    }

    void TryShoot()
    {
        if (currentAmmo > 0)
        {
            float timeBetweenShots = 60f / roundsPerMinute; // <-- convert RPM to seconds
            nextTimeToFire = Time.time + timeBetweenShots;
            Shoot();
        }
        else
        {
            StartCoroutine(Reload());
        }
    }

    void Shoot()
    {
        anim.SetBool("IsShooting", true);
        currentAmmo--;

        if (muzzleFlashPrefab != null && firePoint != null)
        {
            ParticleSystem flash = Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation, firePoint);
            flash.Play();
            Destroy(flash.gameObject, flash.main.duration);
        }

        float spread = isAiming ? adsSpread : hipfireSpread;
        Vector3 direction = playerCamera.transform.forward;
        direction = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0) * direction;

        Vector3 hitPoint = playerCamera.transform.position + direction * range;
        if (Physics.Raycast(playerCamera.transform.position, direction, out RaycastHit hit, range, hitMask))
        {
            hitPoint = hit.point;
            var health = hit.collider.GetComponent<Health>();
            if (health != null) health.TakeDamage(damage);

            if (bulletImpactPrefab != null)
            {
                GameObject impact = Instantiate(bulletImpactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impact, 2f);
            }
        }

        if (bulletTrailPrefab != null)
            StartCoroutine(SpawnBulletTrail(hitPoint));

        ApplyCameraRecoil();
    }

    IEnumerator SpawnBulletTrail(Vector3 hitPoint)
    {
        LineRenderer trail = Instantiate(bulletTrailPrefab, firePoint.position, Quaternion.identity);
        trail.SetPosition(0, firePoint.position);
        trail.SetPosition(1, hitPoint);
        yield return new WaitForSeconds(0.05f);
        Destroy(trail.gameObject);
    }

    void ApplyCameraRecoil()
    {
        bool aiming = isAiming;
        Vector2 addRecoil;

        if (usePattern && recoilPattern.Length > 0)
        {
            addRecoil = recoilPattern[patternIndex % recoilPattern.Length];
            patternIndex++;
        }
        else
        {
            float vRecoil = aiming ? aimRecoilVertical : hipfireRecoilVertical;
            float hRecoil = aiming ? aimRecoilHorizontal : hipfireRecoilHorizontal;

            addRecoil = new Vector2(
                Random.Range(vRecoil * 0.9f, vRecoil),
                Random.Range(-hRecoil, hRecoil));
        }

        recoilTargetOffset += addRecoil;
        shakeTimer = maxShakeDuration;
    }

    void HandleCameraRecoilReturn()
    {
        if (recoilAnchor == null) return;

        currentRecoilOffset = Vector2.SmoothDamp(currentRecoilOffset, recoilTargetOffset, ref recoilVelocity, 1f / recoilSnappiness);
        Quaternion recoilRot = Quaternion.Euler(-currentRecoilOffset.x, currentRecoilOffset.y, 0f);
        recoilAnchor.localRotation = Quaternion.Slerp(recoilAnchor.localRotation, recoilRot, Time.deltaTime * recoilSnappiness);
        recoilTargetOffset = Vector2.Lerp(recoilTargetOffset, Vector2.zero, Time.deltaTime * recoilReturnSpeed);

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float shakeAmount = Mathf.Lerp(shakeMagnitude, 0f, 1 - (shakeTimer / maxShakeDuration));
            Vector3 shakeOffset = new Vector3(Random.Range(-shakeAmount, shakeAmount), Random.Range(-shakeAmount, shakeAmount), 0);
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, baseLocalPos + shakeOffset, Time.deltaTime * recoilReturnSpeed);
        }
        else
        {
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, baseLocalPos, Time.deltaTime * recoilReturnSpeed);
        }
    }

    IEnumerator Reload()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = magazineSize;
        isReloading = false;
    }

    public void OnFire(InputAction.CallbackContext ctx) => fireHeld = ctx.ReadValueAsButton();
    public void OnReload(InputAction.CallbackContext ctx) { if (ctx.performed) reloadPressed = true; }
    public void OnAim(InputAction.CallbackContext ctx) => adsHeld = ctx.ReadValueAsButton();

    void StopShooting()
    {
        anim.SetBool("IsShooting", false);
    }
}
