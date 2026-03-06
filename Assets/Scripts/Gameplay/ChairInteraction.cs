using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ChairInteraction : MonoBehaviour
{
    [Header("Titik Duduk (Snap Point)")]
    [Tooltip("UBAH! Taruh titik ini tepat di LANTAI tempat PIJAKAN KAKI, bukan di bantalan sofa!")]
    public Transform sitPoint;

    [Tooltip("Koreksi posisi tinggi karakter jika animasi masih mengambang / mendem ke lantai (misal isi: -0.5 untuk turunkan).")]
    public float yOffset = 0f;

    [Header("Interaksi")]
    public float interactionDistance = 1.5f;

    private GameObject player;
    private PlayerCustomAnim playerAnimScript;
    private CharacterController charController;
    
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

    void Update()
    {
        if (player == null || playerAnimScript == null) return;

        // Cek Jarak antara player dan kursi
        float distance = Vector3.Distance(player.transform.position, transform.position);

        // Jika player dekat dengan kursi
        if (distance <= interactionDistance)
        {
            // Tampilkan ikon/teks "Tekan F untuk duduk" (Bisa diimplementasikan nanti)

            // Deteksi input F (Input System Baru)
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                OnInteract();
            }
#else
            // Input System Lama
            if (Input.GetKeyDown(KeyCode.F))
            {
                OnInteract();
            }
#endif
        }
    }

    private void OnInteract()
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
                mainCamera.transform.position = sittingCameraPosition.position;
                mainCamera.transform.rotation = sittingCameraPosition.rotation;
            }

            // Membuka Kursor agar bisa di-klik di HP/PC
            if (starterInputs != null)
            {
                starterInputs.cursorLocked = false;
                starterInputs.cursorInputForLook = false;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        // Jika sedang DUDUK di KURSI INI -> BERDIRI
        else 
        {
            playerAnimScript.ToggleSit(); // Panggil animasi berdiri
            
            // Kembalikan Kontrol Jalan
            if (thirdPersonScript != null) thirdPersonScript.enabled = true;
            if (charController != null) charController.enabled = true;

            // Kembalikan Kamera Semula
            if (playerFollowCamera != null) playerFollowCamera.SetActive(true);

            // Kunci kembali Kursor (Mode Standar StarterAssets)
            if (starterInputs != null)
            {
                starterInputs.cursorLocked = true;
                starterInputs.cursorInputForLook = true;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void ClearInteractionUI()
    {
        if (HUDManager.Instance != null) 
            HUDManager.Instance.HideInteraction();
    }
}
