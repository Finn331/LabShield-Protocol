using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

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

    [Header("Auto Collect (Recommended)")]
    [Tooltip("Auto ambil semua model karakter dari scene supaya karakter baru langsung masuk selection.")]
    public bool autoCollectCharacterModels = false;
    [Tooltip("Root model karakter. Jika kosong, pakai object ini (Selection Character).")]
    public Transform characterModelsRoot;
    [Tooltip("Ikut scan object inactive.")]
    public bool includeInactiveModels = true;
    [Tooltip("Sembunyikan varian labcoat di menu selection (umumnya cukup tampilkan uniform).")]
    public bool hideLabVariantsInSelection = true;

    private int currentIndex = 0;
    private readonly List<Quaternion> initialModelRotations = new List<Quaternion>();

    private void Awake()
    {
        RefreshCharacterModelsIfNeeded();
    }

    void Start()
    {
        RefreshCharacterModelsIfNeeded();

        if (characterModels.Count <= 1)
        {
            Debug.LogWarning($"[CharacterSelection] Character models terdeteksi {characterModels.Count}. Next/Previous akan terlihat tidak berubah jika hanya 1 model.");
        }

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
        if (nextButton)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(NextCharacter);
        }
        if (prevButton)
        {
            prevButton.onClick.RemoveAllListeners();
            prevButton.onClick.AddListener(PreviousCharacter);
        }
        if (selectButton)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(StartGame);
        }
        if (backButton)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(BackToMainMenu);
        }
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
        if (mainMenuController != null) mainMenuController.PlayMenuClickSfx();

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
        if (mainMenuController != null) mainMenuController.PlayMenuClickSfx();

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

                if (i < initialModelRotations.Count)
                {
                    characterModels[i].transform.localRotation = initialModelRotations[i];
                }
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

    private void RefreshCharacterModelsIfNeeded()
    {
        if (!autoCollectCharacterModels) return;

        Transform root = characterModelsRoot != null ? characterModelsRoot : transform;
        if (root == null) return;

        Transform[] allChildren = root.GetComponentsInChildren<Transform>(includeInactiveModels);
        List<GameObject> foundModels = new List<GameObject>();

        for (int i = 0; i < allChildren.Length; i++)
        {
            Transform child = allChildren[i];
            if (child == null || child == root) continue;
            if (!IsCharacterCandidate(child)) continue;

            foundModels.Add(child.gameObject);
        }

        foundModels.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

        if (foundModels.Count == 0)
        {
            CacheInitialRotations();
            return;
        }

        // Jika user sudah isi manual list di Inspector (>=2), jangan ditimpa oleh hasil scan yang lebih sedikit.
        if (characterModels != null && characterModels.Count >= 2 && foundModels.Count < characterModels.Count)
        {
            CacheInitialRotations();
            return;
        }

        characterModels = foundModels;
        RebuildCharacterNamesFromModels();
        CacheInitialRotations();
    }

    private bool IsCharacterCandidate(Transform t)
    {
        if (t.GetComponent<RectTransform>() != null) return false;

        if (hideLabVariantsInSelection && IsLikelyLabModel(t))
            return false;

        if (t.GetComponent<PlayerIdentity>() != null) return true;
        if (t.GetComponent<CharacterController>() != null) return true;
        if (t.GetComponent<Animator>() != null) return true;

        return false;
    }

    private static bool IsLikelyLabModel(Transform t)
    {
        if (t == null) return false;
        if (ContainsAnyKeyword(t.name, "labcoat", " lab", "_lab")) return true;

        Transform[] all = t.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform child = all[i];
            if (child == null) continue;
            if (ContainsAnyKeyword(child.name, "labcoat", " lab", "_lab")) return true;
        }

        return false;
    }

    private static bool ContainsAnyKeyword(string text, params string[] keywords)
    {
        if (string.IsNullOrEmpty(text) || keywords == null) return false;

        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];
            if (string.IsNullOrEmpty(keyword)) continue;
            if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    private void RebuildCharacterNamesFromModels()
    {
        characterNames.Clear();
        for (int i = 0; i < characterModels.Count; i++)
        {
            GameObject model = characterModels[i];
            if (model == null)
            {
                characterNames.Add($"Character {i + 1}");
            }
            else
            {
                characterNames.Add(model.name.Replace("_", " "));
            }
        }
    }

    private void CacheInitialRotations()
    {
        initialModelRotations.Clear();
        for (int i = 0; i < characterModels.Count; i++)
        {
            if (characterModels[i] == null)
            {
                initialModelRotations.Add(Quaternion.identity);
            }
            else
            {
                initialModelRotations.Add(characterModels[i].transform.localRotation);
            }
        }
    }

    public void StartGame()
    {
        if (mainMenuController != null) mainMenuController.PlayStartClickSfx();

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
        if (mainMenuController != null) mainMenuController.PlayMenuClickSfx();

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
