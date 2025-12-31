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
    }

    [Header("Checklist UI")]
    public List<ChecklistLink> checklistLinks; // Manually assigned in Inspector

    [Header("Mission Settings")]
    [TextArea] public string missionPrompt = "Praktikum hanya dapat dimulai setelah siswa mengenakan alat pelindung diri. Silahkan pilih dan gunakan APD yang benar untuk mengurangi risiko cedera dan kecelakaan di dalam laboratorium kimia";

    // The exact 4 items required
    private List<string> correctPPE = new List<string> { "Jas lab", "Masker medis", "Sepatu tertutup", "Chemical Resistant Gloves" };

    [Header("Scoring")]
    public int wrongItemScore = 0; // Tracks number of mistakes

    [Header("Debug/Data")]
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

        // Show Mission Prompt
        HUDManager.Instance.UpdateObjective(missionPrompt);
    }

    // Initialize UI based on manual links
    public void InitializeChecklist()
    {
        checklistUIEntries.Clear();

        foreach (var link in checklistLinks)
        {
            if (link.uiText != null && !string.IsNullOrEmpty(link.itemName))
            {
                // Ensure required items match the manual links if needed, or just map them
                checklistUIEntries[link.itemName] = link.uiText;

                // Reset text to default state
                link.uiText.text = "- " + link.itemName;
                link.uiText.color = Color.white; // Or default color
            }
        }
    }

    public void AddItem(string itemName)
    {
        // Validate Item
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
                    checklistUIEntries[itemName].text = $"<s>- {itemName}</s>";
                    checklistUIEntries[itemName].color = Color.green;
                }

                // Show generic feedback
                HUDManager.Instance.ShowInteraction($"Menggunakan {itemName}");
                Invoke("ClearFeedback", 2f);

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

    void CheckCompletion()
    {
        if (collectedItems.Count >= requiredItems.Count)
        {
            Debug.Log("All PPE Collected!");
            HUDManager.Instance.UpdateObjective("APD Lengkap! Akses ke instrumen laboratorium terbuka.");
            // Trigger next game phase here
        }
    }
}
