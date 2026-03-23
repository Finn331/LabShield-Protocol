using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterSelectionManager : MonoBehaviour
{
    [Header("UI References")]
    public Button nextButton;
    public Button prevButton;
    public Button selectButton;
    public Button backButton;
    public TextMeshProUGUI characterNameText;
    
    [Header("Rotasi Animasi (360 derajat)")]
    public float rotationSpeed = 50f;

    [Header("Menu references")]
    public MainMenuController mainMenuController;
    public GameObject characterSelectionPanel;

    [Header("Character Models")]
    public List<GameObject> characterModels = new List<GameObject>();
    public List<string> characterNames = new List<string>();

    private int currentIndex = 0;

    void Start()
    {
        // Load index dari PlayerPrefs kalau ada (supaya tidak reset ke awal jika sebelumnya sudah pilih)
        currentIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);

        // Jika data listnya tidak pas dengan indeks, reset ke 0
        if (currentIndex < 0 || currentIndex >= characterModels.Count)
        {
            if (characterModels.Count > 0) currentIndex = 0;
        }

        SetupButtons();
        UpdateCharacterDisplay();
    }

    void SetupButtons()
    {
        if (nextButton) nextButton.onClick.AddListener(NextCharacter);
        if (prevButton) prevButton.onClick.AddListener(PreviousCharacter);
        if (selectButton) selectButton.onClick.AddListener(StartGame);
        if (backButton) backButton.onClick.AddListener(BackToMainMenu);
    }

    void Update()
    {
        // Animasi putar model 3D (360 derajat non stop)
        if (characterModels.Count > 0 && currentIndex >= 0 && currentIndex < characterModels.Count)
        {
            GameObject activeModel = characterModels[currentIndex];
            if (activeModel != null && activeModel.activeInHierarchy)
            {
                // Putar model pada sumbu Y
                activeModel.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }
        }
    }

    public void NextCharacter()
    {
        if (characterModels.Count == 0) return;

        currentIndex++;
        if (currentIndex >= characterModels.Count)
        {
            currentIndex = 0; // Wrap around
        }
        UpdateCharacterDisplay();
    }

    public void PreviousCharacter()
    {
        if (characterModels.Count == 0) return;

        currentIndex--;
        if (currentIndex < 0)
        {
            currentIndex = characterModels.Count - 1; // Wrap around
        }
        UpdateCharacterDisplay();
    }

    void UpdateCharacterDisplay()
    {
        if (characterModels.Count == 0) return;

        // Tampilkan hanya model yang sedang dipilih
        for (int i = 0; i < characterModels.Count; i++)
        {
            if (characterModels[i] != null)
            {
                characterModels[i].SetActive(i == currentIndex);
                // Reset rotasi ke default tiap ganti model agar selalu menghadap depan saat pertama mucul?
                // Optional: characterModels[i].transform.localRotation = Quaternion.identity;
            }
        }

        // Update nama teks karakter jika tersedia
        if (characterNameText != null)
        {
            if (currentIndex < characterNames.Count)
            {
                characterNameText.text = characterNames[currentIndex];
            }
            else
            {
                characterNameText.text = "Character " + (currentIndex + 1);
            }
        }

        // Simpan index sesekali ke playerprefs agar selalu up to date
        PlayerPrefs.SetInt("SelectedCharacter", currentIndex);
        PlayerPrefs.Save();
    }

    public void StartGame()
    {
        // Sembunyikan panel pemilihan karakter sebelum pindah scene
        if (characterSelectionPanel) characterSelectionPanel.SetActive(false);

        // Minta MainMenuController untuk meluncurkan game (menampilkan layar loading)
        // Karena LoadingScreen dan Coroutine StartGame ada di sana.
        if (mainMenuController != null)
        {
            Debug.Log($"Memulai game dengan karakter: {currentIndex}");
            mainMenuController.StartGameFromSelection(); 
            // Kita sudah modifikasi MainMenuController untuk meload scene langsung melalui StartGameFromSelection
            // Tapi karena UI ini terpisah, kita akan buat fungsi load scene langsung
        }
    }

    public void BackToMainMenu()
    {
        // Berikan sinyal ke MainMenuController bahwa kita menekan tombol back
        // 1. Hide diri sendiri (dengan animasi keluar)
        // 2. Munculkan kembali MainMenu Panel dengan animasi
        
        LeanTween.scale(characterSelectionPanel, Vector3.zero, 0.4f)
            .setEase(LeanTweenType.easeInBack)
            .setOnComplete(() =>
            {
                characterSelectionPanel.SetActive(false);

                // Kembalikan ke MainMenu
                if (mainMenuController != null && mainMenuController.worldSpaceMenuCanvas != null)
                {
                    Transform mmFound = mainMenuController.worldSpaceMenuCanvas.transform.Find("Canvas - Worldspace/Mainmenu Panel");
                    if (mmFound == null) mmFound = mainMenuController.worldSpaceMenuCanvas.transform.Find("Mainmenu Panel");
                    
                    if (mmFound != null)
                    {
                        mmFound.gameObject.SetActive(true);
                        LeanTween.scale(mmFound.gameObject, mainMenuController.menuTargetScale, 0.6f)
                            .setEase(LeanTweenType.easeOutBack);
                    }
                }
            });
    }
}
