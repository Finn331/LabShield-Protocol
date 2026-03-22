using UnityEngine;
using System.Collections;

public enum AllowedGender
{
    BebasSemua,
    HanyaLakiLaki,
    HanyaPerempuan
}

public class DoorInteraction : Interactable
{
    [Header("Door Access (Akses Gender)")]
    [Tooltip("Siapa saja yang boleh membuka pintu ini?")]
    public AllowedGender allowedGender = AllowedGender.BebasSemua;

    [Tooltip("Pesan penolakan jika gender pemain tidak sesuai.")]
    public string accessDeniedMessage = "Akses Ditolak: Bukan Toilet Anda!";

    [Header("Door Access (Akses APD)")]
    [Tooltip("Centang jika pintu ini hanya bisa dibuka setelah pemain menggunakan APD lengkap.")]
    public bool requiresPPE = false;

    [Tooltip("Pesan penolakan jika APD belum lengkap.")]
    public string ppeDeniedMessage = "Akses Ditolak: Anda belum menggunakan APD lengkap!";

    [Header("Door Settings")]
    [Tooltip("Pivot/Engsel pintu yang akan diputar. Biarkan kosong jika pivot ada di objek ini sendiri.")]
    public Transform doorPivot;

    [Tooltip("Sudut rotasi saat pintu TERTUTUP (X, Y, Z)")]
    public Vector3 closedRotation = Vector3.zero;

    [Tooltip("Sudut rotasi saat pintu TERBUKA (X, Y, Z)")]
    public Vector3 openRotation = new Vector3(0, 90, 0);

    [Tooltip("Lama waktu animasi buka/tutup (detik)")]
    public float animationDuration = 1f;

    [Header("Current State")]
    public bool isOpen = false;
    private bool isAnimating = false;

    [Tooltip("Pilih apakah teks interaksi 'Buka/Tutup Pintu' akan ditampilkan di layar atau tidak.")]
    public bool showPrompt = true;

    [Header("Audio Settings")]
    [Tooltip("Suara saat pintu mulai dibuka")]
    public AudioClip openDoorSound;
    [Tooltip("Suara saat pintu mulai ditutup")]
    public AudioClip closeDoorSound;
    private AudioSource audioSource;

    private string openPrompt = "Buka Pintu";
    private string closePrompt = "Tutup Pintu";

    void Start()
    {
        if (doorPivot == null)
        {
            doorPivot = transform;
        }

        // Setup AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            // Otomatis tambahkan jika belum ada, buat sebagai suara 3D
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D Sound
            audioSource.playOnAwake = false;
        }

        // Set prompt awal berdasarkan status pintu
        UpdatePromptMessage();
    }

    public override void OnFocus()
    {
        base.OnFocus();
        if (showPrompt && HUDManager.Instance != null && promptMessage != "")
        {
            HUDManager.Instance.ToggleInteractionButton(true, promptMessage);
        }
    }

    public override void OnLoseFocus()
    {
        base.OnLoseFocus();
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ToggleInteractionButton(false);
        }
    }

    protected override void ExecuteInteraction()
    {
        if (isAnimating) return; // Jangan izinkan spam interaksi saat pintu masih bergerak

        // CEK AKSES GENDER & APD HANYA SAAT PINTU AKAN DIBUKA
        if (!isOpen)
        {
            // CEK AKSES APD
            if (requiresPPE)
            {
                if (InventoryManager.Instance != null && !InventoryManager.Instance.HasCompletePPE())
                {
                    if (HUDManager.Instance != null) HUDManager.Instance.ShowInteraction(ppeDeniedMessage);
                    return; // Batal buka
                }
            }

            // CEK AKSES GENDER
            if (allowedGender != AllowedGender.BebasSemua)
            {
                // Temukan player yang sedang aktif
                GameObject activePlayer = GetActivePlayer();
                if (activePlayer != null)
                {
                    PlayerIdentity identity = activePlayer.GetComponent<PlayerIdentity>();
                    if (identity != null)
                    {
                        // Apakah player ini Laki-Laki mencoba masuk pintu Perempuan?
                        if (allowedGender == AllowedGender.HanyaPerempuan && identity.gender != PlayerGender.Female)
                        {
                            if (HUDManager.Instance != null) HUDManager.Instance.ShowInteraction(accessDeniedMessage);
                            return; // Batal buka
                        }
                        // Apakah player ini Perempuan mencoba masuk pintu Laki-Laki?
                        else if (allowedGender == AllowedGender.HanyaLakiLaki && identity.gender != PlayerGender.Male)
                        {
                            if (HUDManager.Instance != null) HUDManager.Instance.ShowInteraction(accessDeniedMessage);
                            return; // Batal buka
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Pemain tidak memiliki komponen PlayerIdentity. Mengizinkan masuk karena tidak ada data gender.");
                    }
                }
            }
        } // Penutup if (!isOpen)

        StartCoroutine(ToggleDoorRoutine());
    }

    private GameObject GetActivePlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach(GameObject p in players)
        {
            if(p.activeInHierarchy)
            {
                return p;
            }
        }
        return null; // Fallback
    }

    private IEnumerator ToggleDoorRoutine()
    {
        isAnimating = true;

        // Memutar Suara Buka / Tutup
        if (!isOpen && openDoorSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(openDoorSound);
        }
        else if (isOpen && closeDoorSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(closeDoorSound);
        }

        Vector3 startRot = doorPivot.localEulerAngles;
        Vector3 endRot = isOpen ? closedRotation : openRotation;

        float elapsedTime = 0f;

        while (elapsedTime < animationDuration)
        {
            float t = elapsedTime / animationDuration;
            // SmoothStep agar pergerakan punya efek ease-in ease-out (tidak kaku)
            t = t * t * (3f - 2f * t);

            doorPivot.localEulerAngles = new Vector3(
                Mathf.LerpAngle(startRot.x, endRot.x, t),
                Mathf.LerpAngle(startRot.y, endRot.y, t),
                Mathf.LerpAngle(startRot.z, endRot.z, t)
            );

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Pastikan ujung frame posisi sudah akurat
        doorPivot.localEulerAngles = endRot;

        // Balikkan state
        isOpen = !isOpen;
        UpdatePromptMessage();
        
        // Refresh UI jika pemain masih menyorot pintu
        if (showPrompt && HUDManager.Instance != null && promptMessage != "")
        {
            // Namun, jika kita sedang TDK menatap pintu lagi (misal saat berjalan sambil pintu kebuka), 
            // Kita sebaiknya tidak memunculkan paksa tombolnya.
            // Memeriksa Interactable akan di-handle oleh PlayerInteraction, di sini kita hanya paksa ganti teks kalau tombolnya SEDANG aktif.
            HUDManager.Instance.ToggleInteractionButton(true, promptMessage);
        }

        isAnimating = false;
    }

    private void UpdatePromptMessage()
    {
        promptMessage = showPrompt ? (isOpen ? closePrompt : openPrompt) : "";
    }
}
