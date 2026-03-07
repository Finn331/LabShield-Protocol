using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ChairInteraction : Interactable
{
    [Header("Titik Duduk (Snap Point)")]
    [Tooltip("UBAH! Taruh titik ini tepat di LANTAI tempat PIJAKAN KAKI, bukan di bantalan sofa!")]
    public Transform sitPoint;

    [Tooltip("Koreksi posisi tinggi karakter jika animasi masih mengambang / mendem ke lantai (misal isi: -0.5 untuk turunkan).")]
    public float yOffset = 0f;

    [Tooltip("Waktu penundaan (detik) hingga animasi berdiri selesai sebelum player bisa jalan lagi.")]
    public float standUpDelay = 1.5f;

    [Tooltip("Durasi perpindahan kamera (detik) agar halus dari posisi awal ke posisi duduk.")]
    public float cameraTransitionTime = 1f;

    [Header("Fitur Kuis (Opsional)")]
    [Tooltip("Pilih objek komponen QuizTrigger di ruangan ini (jika ada). Kuis akan dimulai saat player sudah selesai duduk.")]
    public QuizTrigger targetQuiz;

    private GameObject player;
    private PlayerCustomAnim playerAnimScript;
    private CharacterController charController;
    private Coroutine cameraCoroutine; // Menyimpan referensi coroutine efek kamera
    
    // Untuk mengunci gerak bawaan ThirdPersonController
    private MonoBehaviour thirdPersonScript;
    // Referensi fitur Kursor
    private StarterAssets.StarterAssetsInputs starterInputs;

    [Header("Kamera & Kursor")]
    [Tooltip("Kamera pantauan (misal: PlayerFollowCamera). Akan dimatikan saat duduk.")]
    public GameObject playerFollowCamera;
    
    [Tooltip("Kamera Utama (MainCamera) pelacak.")]
    public Camera mainCamera;
    
    [Tooltip("Titik referensi kamera saat duduk. MainCamera akan dipindah ke sini saat duduk.")]
    public Transform sittingCameraPosition;

    void Start()
    {
        // Inisialisasi teks UI saat disorot kamera
        promptMessage = "";

        // Cari player di scene menggunakan tag
        player = GameObject.FindGameObjectWithTag("Player");
        
        if (player != null)
        {
            playerAnimScript = player.GetComponent<PlayerCustomAnim>();
            charController = player.GetComponent<CharacterController>();
            
            // StarterAssets bawaan
            thirdPersonScript = player.GetComponent("ThirdPersonController") as MonoBehaviour;
            starterInputs = player.GetComponent<StarterAssets.StarterAssetsInputs>();
        }

        if (sitPoint == null)
        {
            Debug.LogWarning("Sit Point belum diisi pada kursi " + gameObject.name);
            sitPoint = transform; // Default ke titik tengah kursi jika lupa diisi
        }
    }

    protected override void ExecuteInteraction()
    {
        // Jika sedang BERDIRI -> DUDUK
        if (!playerAnimScript.isSitting) 
        {
            // 1. Matikan komponen gerak agar posisi tidak kerubah gravitasi/user
            if (thirdPersonScript != null) thirdPersonScript.enabled = false;
            if (charController != null) charController.enabled = false;

            // 2. Pindahkan Player tepat ke titik "sitPoint" LANTAI ditambah koreksi Y manual
            Vector3 finalSitPos = sitPoint.position;
            finalSitPos.y += yOffset;
            player.transform.position = finalSitPos;
            
            // 3. Putar badan Player agar menghadap arah yang sama dengan kursi
            player.transform.rotation = sitPoint.rotation;

            // 4. Mainkan Animasi Duduk
            playerAnimScript.ToggleSit();

            // 5. Pindahkan Kamera & Lepas Kursor
            if (playerFollowCamera != null) playerFollowCamera.SetActive(false);

            if (mainCamera != null && sittingCameraPosition != null)
            {
                // Hentikan transisi kamera yang mungkin masih berjalan sebelumnya
                if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
                cameraCoroutine = StartCoroutine(SmoothCameraTransition());
            }

            // Membuka Kursor agar bisa di-klik di HP/PC
            if (starterInputs != null)
            {
                starterInputs.cursorLocked = false;
                starterInputs.cursorInputForLook = false;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // -- BARU: Pasang forcedInteractable agar UI Interact Mobile tidak hilang memudar! --
            PlayerInteraction playerInteractionLogic = mainCamera != null ? mainCamera.GetComponent<PlayerInteraction>() : FindObjectOfType<PlayerInteraction>();
            if (playerInteractionLogic != null)
            {
                promptMessage = "";
                playerInteractionLogic.forcedInteractable = this;
                
                // PENTING: Refresh Paksa UI agar terbaca update hilangnya teks
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ToggleInteractionButton(true, promptMessage);
                }
            }
        }
        // Jika sedang DUDUK di KURSI INI -> BERDIRI
        else 
        {
            playerAnimScript.ToggleSit(); // Panggil animasi berdiri
            
            // Hentikan transisi kamera menuju ke kursi (jika belum selesai)
            if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);

            // Kembalikan Kamera Semula (agar user bisa langsung melihat playernya dari belakang lagi)
            if (playerFollowCamera != null) playerFollowCamera.SetActive(true);

            // Kunci kembali Kursor (Mode Standar StarterAssets)
            if (starterInputs != null)
            {
                starterInputs.cursorLocked = true;
                starterInputs.cursorInputForLook = true;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // -- BARU: Lepas forcedInteractable agar pencarian objek dengan Raycast kembali bekerja --
            PlayerInteraction playerInteractionLogic = mainCamera != null ? mainCamera.GetComponent<PlayerInteraction>() : FindObjectOfType<PlayerInteraction>();
            if (playerInteractionLogic != null)
            {
                promptMessage = "";
                playerInteractionLogic.forcedInteractable = null;
                
                // Matikan paksa UI sesaat, jika kursor kena kursi lagi nanti otomatis hidup
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ToggleInteractionButton(false);
                }
            }

            // Jangan langsung kembalikan kontrol jalan! Tunggu durasi berdiri selesai
            StartCoroutine(EnableMovementRoutine());
        }
    }

    private IEnumerator EnableMovementRoutine()
    {
        // Berikan jeda waktu
        yield return new WaitForSeconds(standUpDelay);

        if (player != null)
        {
            // Kembalikan Kontrol Jalan SETELAH animasi selesai
            if (thirdPersonScript != null) thirdPersonScript.enabled = true;
            if (charController != null) charController.enabled = true;
        }
    }

    private IEnumerator SmoothCameraTransition()
    {
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < cameraTransitionTime)
        {
            // Menghitung perpindahan posisi (Lerp) secara halus dari 0% ke 100%
            float t = elapsedTime / cameraTransitionTime;
            // Gunakan SmoothStep agar pergerakan kamera halus di awal dan akhir
            t = t * t * (3f - 2f * t);

            mainCamera.transform.position = Vector3.Lerp(startPos, sittingCameraPosition.position, t);
            mainCamera.transform.rotation = Quaternion.Lerp(startRot, sittingCameraPosition.rotation, t);
            
            elapsedTime += Time.deltaTime;
            yield return null; // Tunggu ke frame berikutnya
        }
        
        // Pastikan posisi tertempel sempurna di akhir durasi
        mainCamera.transform.position = sittingCameraPosition.position;
        mainCamera.transform.rotation = sittingCameraPosition.rotation;

        // --- BARU: Jalankan Event Kuis setelah Kamera Berhenti ---
        if (targetQuiz != null)
        {
            targetQuiz.TriggerQuizManual();
        }
    }

    private void ClearInteractionUI()
    {
        if (HUDManager.Instance != null) 
            HUDManager.Instance.HideInteraction();
    }
}
