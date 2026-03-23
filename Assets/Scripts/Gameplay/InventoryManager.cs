using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [System.Serializable]
    public struct ChecklistLink
    {
        public string itemName;
        public TextMeshProUGUI uiText;
        [Tooltip("Opsional: Tarik objek ikon panah biru (atau parent barisnya) ke sini agar bisa disembunyikan total.")]
        public GameObject rowContainer;
    }

    [Header("Checklist UI")]
    public List<ChecklistLink> checklistLinks; // Manually assigned in Inspector

    // ==========================================
    // BAGIAN: PENGATURAN AWAL (GAME START)
    // ==========================================
    [Space(10)]
    [Header("--- AWAL PERMAINAN: PENGATURAN TEKS ---")]
    [Tooltip("Teks yang muncul pertama kali di pojok kiri atas (sebelum daftar asli muncul).")]
    public string initialChecklistHint = "Pergi ke Ruang Ganti";

    [Tooltip("Warna teks petunjuk awal di atas.")]
    public Color initialHintColor = Color.black; // Sama seperti warna default lainnya.

    // ==========================================
    // BAGIAN: SOAL 1 (PEMILIHAN APD)
    // ==========================================
    [Space(10)]
    [Header("--- PENGATURAN MISI (SETELAH KE RUANG GANTI) ---")]
    [Tooltip("Instruksi soal 1 untuk pemain saat sampai di ruang ganti.")]
    [TextArea] public string missionPrompt = "Praktikum hanya dapat dimulai setelah siswa mengenakan alat pelindung diri. Silahkan pilih dan gunakan APD yang benar untuk mengurangi risiko cedera dan kecelakaan di dalam laboratorium kimia";

    // The exact 4 items required
    [Tooltip("Daftar APD yang benar untuk Soal 1")]
    [SerializeField] private List<string> correctPPE = new List<string> { "Jas lab", "Masker medis", "Sepatu tertutup", "Chemical Resistant Gloves" };

    [Header("--- SOAL 1: PENILAIAN ---")]
    [Tooltip("Jumlah kesalahan pemain saat memilih APD (Soal 1)")]
    public int wrongItemScore = 0; // Tracks number of mistakes

    // ==========================================
    // BAGIAN: SISTEM INVENTORY UMUM
    // ==========================================
    [Space(10)]
    [Header("--- INVENTORY UMUM: DEBUG & DATA ---")]
    public List<string> requiredItems = new List<string>();
    private List<string> collectedItems = new List<string>();
    private Dictionary<string, TextMeshProUGUI> checklistUIEntries = new Dictionary<string, TextMeshProUGUI>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Set specific requirements based on User Input
        requiredItems = new List<string>(correctPPE);

        InitializeChecklist();

        // (We remove the HUD UpdateObjective here because GameplayManager handles the initial objective now)
    }

    // Initialize UI based on manual links
    public void InitializeChecklist()
    {
        checklistUIEntries.Clear();

        for (int i = 0; i < checklistLinks.Count; i++)
        {
            var link = checklistLinks[i];
            if (link.uiText != null && !string.IsNullOrEmpty(link.itemName))
            {
                checklistUIEntries[link.itemName] = link.uiText;

                if (i == 0)
                {
                    // Item pertama dijadikan petunjuk awal
                    link.uiText.text = initialChecklistHint;
                    link.uiText.color = initialHintColor;

                    // Nyalakan text & row containernya
                    link.uiText.gameObject.SetActive(true);
                    if (link.rowContainer != null) link.rowContainer.SetActive(true);
                }
                else
                {
                    // Sisanya disembunyikan total 
                    link.uiText.gameObject.SetActive(false);
                    if (link.rowContainer != null) link.rowContainer.SetActive(false);
                }
            }
        }
    }

    // ==========================================
    // LOGIKA SOAL 1 & PENGUMPULAN ITEM INVENTORY
    // ==========================================
    public void AddItem(string itemName)
    {
        // Validate Item (Pengecekan Soal 1)
        if (requiredItems.Contains(itemName))
        {
            // Correct Item
            if (!collectedItems.Contains(itemName))
            {
                collectedItems.Add(itemName);
                Debug.Log($"Collected Correct Item: {itemName}");

                // Update UI
                if (checklistUIEntries.ContainsKey(itemName))
                {
                    checklistUIEntries[itemName].text = $"<s>{itemName}</s>";
                    checklistUIEntries[itemName].color = Color.green;
                }

                // Show generic feedback
                HUDManager.Instance.ShowInteraction($"Menggunakan {itemName}");
                Invoke("ClearFeedback", 2f);

                if (ScoreSystemManager.Instance != null)
                {
                    ScoreSystemManager.Instance.UpdateAPDScore(collectedItems.Count, requiredItems.Count, wrongItemScore);
                }

                CheckCompletion();
            }
        }
        else
        {
            // Wrong Item!
            wrongItemScore++;
            Debug.Log($"Wrong Item Collected: {itemName}. total Wrong: {wrongItemScore}");

            // Show Feedback (Red Warning)
            HUDManager.Instance.ShowInteraction("Salah! Item ini tidak sesuai standar APD.");
            // Ideally play a buzz sound here

            // IMPORTANT: We do NOT add it to 'collectedItems' so it doesn't count towards progress, 
            // but we might want to record it for the final score report.
            
            if (ScoreSystemManager.Instance != null)
            {
                ScoreSystemManager.Instance.UpdateAPDScore(collectedItems.Count, requiredItems.Count, wrongItemScore);
            }
        }
    }

    void ClearFeedback() { HUDManager.Instance.HideInteraction(); }

    public bool HasItem(string itemName)
    {
        return collectedItems.Contains(itemName);
    }

    public bool HasCompletePPE()
    {
        return collectedItems.Count >= requiredItems.Count;
    }

    public void HideChecklist()
    {
        foreach (var link in checklistLinks)
        {
            if (link.uiText != null)
            {
                // Animated Fade Out using LeanTween.value for TMP
                LeanTween.value(link.uiText.gameObject, link.uiText.alpha, 0f, 0.5f)
                    .setOnUpdate((float val) => { link.uiText.alpha = val; })
                    .setOnComplete(() =>
                    {
                        link.uiText.gameObject.SetActive(false);
                        if (link.rowContainer != null) link.rowContainer.SetActive(false);
                        // Reset alpha for next time
                        link.uiText.alpha = 1f;
                    });
            }
        }
    }

    public void ShowChecklist()
    {
        foreach (var link in checklistLinks)
        {
            if (link.uiText != null)
            {
                // Kembalikan nama dan warna ke semula
                link.uiText.text = link.itemName;
                link.uiText.color = Color.black;

                // Tampilkan text dan containernya
                link.uiText.gameObject.SetActive(true);
                if (link.rowContainer != null) link.rowContainer.SetActive(true);

                // Animated Fade In using LeanTween.value for TMP
                LeanTween.value(link.uiText.gameObject, 0f, 1f, 0.5f)
                    .setOnUpdate((float val) => { link.uiText.alpha = val; });
            }
        }
    }

    void CheckCompletion()
    {
        if (collectedItems.Count >= requiredItems.Count)
        {
            Debug.Log("All PPE Collected!");
            HUDManager.Instance.UpdateObjective("APD Lengkap! Silahkan menuju Ruang Ganti untuk mengganti pakaian.");
            
            if (ScoreSystemManager.Instance != null)
            {
                ScoreSystemManager.Instance.HideScore();
            }

            // Hentikan Timer APD dan Simpan Skor APD ke JSON
            if (QuizSessionManager.Instance != null)
            {
                QuizSessionManager.Instance.StopAPDTimer();
                QuizSessionManager.Instance.RecordAPDResult(collectedItems.Count, wrongItemScore);
            }

            // Menonaktifkan semua APD sisa di scene agar tidak bisa di-interact lagi
            PickupItem.DisableAllPickups();

            // Tampilkan kembali Waypoint, kali ini menunjuk ke Ruang Ganti
            if (GameplayManager.Instance != null && GameplayManager.Instance.changingRoomWaypoint != null)
            {
                HUDManager.Instance.SetWaypointTarget(GameplayManager.Instance.changingRoomWaypoint);
            }
        }
    }
}
