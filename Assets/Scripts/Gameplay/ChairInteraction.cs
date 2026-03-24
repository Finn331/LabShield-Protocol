using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ChairInteraction : Interactable
{
    [Header("Titik Duduk (Snap Point)")]
    [Tooltip("Titik final saat karakter sudah duduk tepat di bantalan/kursi!")]
    public Transform sitPoint;

    [Header("Posisi Animasi Duduk (Opsional)")]
    [Tooltip("Titik pemain memulai animasi duduk (di depan kursi). Jika kosong, langsung ke sitPoint.")]
    public Transform sitAnimPosition;
    
    [Tooltip("Durasi transisi pergerakan karakter dari posisi 'Anim Duduk' ke 'Snap Point Duduk'.")]
    public float sitTransitionTime = 0.5f;

    [Header("Posisi Animasi Berdiri (Opsional)")]
    [Tooltip("Titik awal karakter bergerak untuk bangun dari kursi. Jika kosong, tetap di tempat.")]
    public Transform standAnimPosition;

    [Tooltip("Titik akhir karakter setelah selesai berdiri (di luar kursi) agar bisa jalan tanpa nabrak.")]
    public Transform standFinalPosition;

    [Tooltip("Koreksi posisi tinggi karakter jika animasi masih mengambang / mendem ke lantai (misal isi: -0.5 untuk turunkan).")]
    public float yOffset = 0f;

    [Tooltip("Waktu penundaan (detik) hingga animasi berdiri selesai sebelum player bisa jalan lagi.")]
    public float standUpDelay = 1.5f;

    [Tooltip("Durasi perpindahan kamera (detik) agar halus dari posisi awal ke posisi duduk.")]
    public float cameraTransitionTime = 1f;

    [Header("Fitur Kuis (Opsional)")]
    [Tooltip("Pilih objek komponen QuizTrigger di ruangan ini (jika ada). Kuis akan dimulai saat player sudah selesai duduk.")]
    public QuizTrigger targetQuiz;

    [Header("Posisi Score Panel Saat Duduk")]
    [Tooltip("Jika aktif, Score Panel dipindah ke posisi ini saat player selesai duduk di kursi quiz.")]
    public bool useSittingScorePanelPlacement = true;
    public Vector3 sittingScorePanelPosition = new Vector3(405.9000244f, 1096.0500488f, 0f);
    public Vector3 sittingScorePanelScale = Vector3.one;

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
        // BARU: Selalu perbarui referensi player saat berinteraksi agar mendeteksi player yang aktif
        RefreshPlayerReference();

        // Jika sedang BERDIRI -> DUDUK
        if (!playerAnimScript.isSitting) 
        {
            // 1. Matikan komponen gerak agar posisi tidak kerubah gravitasi/user
            if (thirdPersonScript != null) thirdPersonScript.enabled = false;
            if (charController != null) charController.enabled = false;

            // 2. Tentukan posisi awal animasi duduk (sitAnimPosition jika ada, atau langsung sitPoint)
            Transform targetSitAnim = sitAnimPosition != null ? sitAnimPosition : sitPoint;
            Vector3 startSitPos = targetSitAnim.position;
            startSitPos.y += yOffset;
            player.transform.position = startSitPos;
            
            // 3. Putar badan Player agar menghadap arah target
            player.transform.rotation = targetSitAnim.rotation;

            // 4. Mainkan Animasi Duduk
            playerAnimScript.ToggleSit();

            // 5. Perpindahan Halus (Lerp) ke titik final duduk (sitPoint) agar tidak tembus/ngambang
            if (sitAnimPosition != null)
            {
                Vector3 finalSitPos = sitPoint.position;
                finalSitPos.y += yOffset;
                StartCoroutine(MovePlayerRoutine(startSitPos, finalSitPos, targetSitAnim.rotation, sitPoint.rotation, sitTransitionTime));
            }

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
                starterInputs.SetCursorState(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // -- BARU: Pasang forcedInteractable agar UI Interact Mobile tidak hilang memudar! --
            PlayerInteraction playerInteractionLogic = mainCamera != null ? mainCamera.GetComponent<PlayerInteraction>() : FindFirstObjectByType<PlayerInteraction>();
            if (playerInteractionLogic != null)
            {
                promptMessage = "";
                playerInteractionLogic.forcedInteractable = this;
                
                // Sembunyikan semua UI Gameplay saat mulai Kuis (termasuk crosshair)
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.HideAllHUD();
                }
            }
        }
        // Jika sedang DUDUK di KURSI INI -> BERDIRI
        else 
        {
            if (ScoreSystemManager.Instance != null)
            {
                ScoreSystemManager.Instance.RestoreDefaultScorePanelPlacement();
            }

            // Pindahkan player ke titik stand anim (jika ada) agar animasi berdiri tidak bertabrakan dengan mesh kursi
            if (standAnimPosition != null)
            {
                Vector3 finalStandPos = standAnimPosition.position;
                finalStandPos.y += yOffset;
                player.transform.position = finalStandPos;
                player.transform.rotation = standAnimPosition.rotation;
            }

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
                starterInputs.SetCursorState(true);
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // -- BARU: Lepas forcedInteractable agar pencarian objek dengan Raycast kembali bekerja --
            PlayerInteraction playerInteractionLogic = mainCamera != null ? mainCamera.GetComponent<PlayerInteraction>() : FindFirstObjectByType<PlayerInteraction>();
            if (playerInteractionLogic != null)
            {
                promptMessage = "";
                playerInteractionLogic.forcedInteractable = null;
                
                // Kembalikan semua UI yang tadi disembunyikan
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowAllHUD();
                    HUDManager.Instance.ToggleInteractionButton(false);
                }
            }

            // Jangan langsung kembalikan kontrol jalan! Tunggu durasi berdiri selesai
            StartCoroutine(EnableMovementRoutine());
        }
    }

    private void RefreshPlayerReference()
    {
        // Temukan player yang AKTIF saat ini
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach(GameObject p in players)
        {
            if(p.activeInHierarchy)
            {
                player = p;
                break;
            }
        }

        if (player != null)
        {
            playerAnimScript = player.GetComponent<PlayerCustomAnim>();
            charController = player.GetComponent<CharacterController>();
            thirdPersonScript = player.GetComponent("ThirdPersonController") as MonoBehaviour;
            starterInputs = player.GetComponent<StarterAssets.StarterAssetsInputs>();
        }
    }

    private IEnumerator EnableMovementRoutine()
    {
        // Berikan jeda waktu
        yield return new WaitForSeconds(standUpDelay);

        if (player != null)
        {
            // Pindahkan ke posisi final berdiri (jika ada) setelah animasi usai
            if (standFinalPosition != null)
            {
                Vector3 finalStandPos = standFinalPosition.position;
                finalStandPos.y += yOffset;
                
                // Gunakan CharacterController agar tidak terjadi bentrokan saat pindah posisi drastis
                if (charController != null) charController.enabled = false;
                
                player.transform.position = finalStandPos;
                player.transform.rotation = standFinalPosition.rotation;
            }

            // Kembalikan Kontrol Jalan SETELAH animasi selesai
            if (charController != null) charController.enabled = true;
            if (thirdPersonScript != null) thirdPersonScript.enabled = true;
        }
    }

    // BARU: Coroutine untuk menggeser posisi player secara mulus saat animasi duduk berlangsung
    private IEnumerator MovePlayerRoutine(Vector3 startPos, Vector3 endPos, Quaternion startRot, Quaternion endRot, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            player.transform.position = Vector3.Lerp(startPos, endPos, t);
            player.transform.rotation = Quaternion.Lerp(startRot, endRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        player.transform.position = endPos;
        player.transform.rotation = endRot;
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
        if (useSittingScorePanelPlacement && ScoreSystemManager.Instance != null)
        {
            ScoreSystemManager.Instance.SetScorePanelWorldPlacement(
                sittingScorePanelPosition,
                Quaternion.identity,
                sittingScorePanelScale
            );
        }

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
