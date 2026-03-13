using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using TMPro;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject titleText; // "LabShield Protocol" Object
    public GameObject pressStartText; // "Press Anywhere To Start" Object

    [Header("Camera Settings")]
    public Camera mainCamera;
    public Animator cameraAnimator;
    public PlayableDirector cameraDirector;

    [Header("Target Transform")]
    public Vector3 targetPosition = new Vector3(2.0f, 1.74f, -3.0f);
    public Quaternion targetRotation = new Quaternion(0.0f, -0.7933523f, 0.0f, 0.6087629f);

    private bool hasStarted = false;
    private bool isLoggedIn = false; // New flag

    [Header("Menu references")]
    public GameObject worldSpaceMenuCanvas; // Ref User: "Canvas Worldspace"

    [Header("World Menu Transform")]
    public Vector3 menuTargetPos = new Vector3(0.1000356f, 0.7953247f, -3.670000f);
    public Quaternion menuTargetRot = new Quaternion(0.0f, -0.8660245f, 0.0f, 0.5000017f);
    public Vector3 menuTargetScale = new Vector3(0.1332075f, 0.1332075f, 0.1332075f);

    private GameObject menuObjectToAnimate; // The actual object to move/scale

    [Header("Menu Logic")]
    public SettingsMenuController settingsController;
    public UnityEngine.UI.Button playButton;
    public UnityEngine.UI.Button settingsButton;
    public UnityEngine.UI.Button exitButton;

    // Optional: Reference to Gameplay components to activate on Play
    [Header("Gameplay References (Optional if loading new scene)")]
    public GameObject hudCanvas;
    public GameObject playerController; // Or FirstPersonController script reference

    [Header("Settings Camera Transform")]
    public Vector3 settingsCameraPos = new Vector3(4.006f, 1.773f, -3.553f);
    public Quaternion settingsCameraRot = new Quaternion(0.0f, 0.6087628f, 0.0f, 0.7933524f);

    [Header("Scene Configuration & Loading")]
    public string gameplaySceneName = "Gameplay"; // Set in Inspector
    
    [Tooltip("Opsional: Masukkan panel/Gameobject Loading Screen di sini")]
    public GameObject loadingScreenUI;
    [Tooltip("Opsional: Masukkan Slider untuk progress bar loading")]
    public UnityEngine.UI.Slider loadingSlider;
    [Tooltip("Opsional: Masukkan TextMeshProUGUI untuk animasi teks (Loading...)")]
    public TextMeshProUGUI loadingText;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // Hide Loading Screen if it exists
        if (loadingScreenUI != null) loadingScreenUI.SetActive(false);

        // Start State: Hide "Press Start" until Login is done
        // if (titleText) titleText.SetActive(false); // CHANGED: Keep Title Visible!


        if (pressStartText) pressStartText.SetActive(false);

        // Ensure Worldspace Menu is hidden initially and reset scale for pop-up effect
        if (worldSpaceMenuCanvas)
        {
            // 1. Try Specific Search for "Mainmenu Panel" (Deep Search)
            Transform found = worldSpaceMenuCanvas.transform.Find("Canvas - Worldspace/Mainmenu Panel");

            // 2. Fallback: Search just by name in all children
            if (found == null)
            {
                foreach (Transform t in worldSpaceMenuCanvas.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Mainmenu Panel")
                    {
                        found = t;
                        break;
                    }
                }
            }

            if (found != null)
            {
                menuObjectToAnimate = found.gameObject;
            }
            else
            {
                // Last Resort: Fallback to the assigned object 
                menuObjectToAnimate = worldSpaceMenuCanvas;
            }

            menuObjectToAnimate.SetActive(false);
            Debug.Log($"[MainMenu] Worldspace Menu ({menuObjectToAnimate.name}) set to INACTIVE (Waiting for Press).");

            // Prepare Transform for Animation
            menuObjectToAnimate.transform.position = menuTargetPos;
            menuObjectToAnimate.transform.rotation = menuTargetRot;
            menuObjectToAnimate.transform.localScale = Vector3.zero; // Start from zero scale
        }

        // Start Pulsing Animation for "Press Start" (will be visible later)
        if (pressStartText != null)
        {
            LeanTween.scale(pressStartText, Vector3.one * 1.1f, 0.8f)
                .setEase(LeanTweenType.easeInOutSine)
                .setLoopPingPong();
        }
    }

    // Called by LoginUI when login is successful
    public void OnLoginSuccess()
    {
        isLoggedIn = true;

        // Show Title Screen Sequence
        if (titleText)
        {
            titleText.SetActive(true);
            CanvasGroup cg = EnsureCanvasGroup(titleText);
            LeanTween.alphaCanvas(cg, 1f, 1f).setFrom(0f);
        }

        if (pressStartText)
        {
            pressStartText.SetActive(true);
            CanvasGroup cg = EnsureCanvasGroup(pressStartText);
            LeanTween.alphaCanvas(cg, 1f, 1f).setFrom(0f).setDelay(0.5f);
        }
    }

    private CanvasGroup EnsureCanvasGroup(GameObject obj)
    {
        CanvasGroup cg = obj.GetComponent<CanvasGroup>();
        if (cg == null) cg = obj.AddComponent<CanvasGroup>();
        return cg;
    }

    void Update()
    {
        // Only allow Start if Logged In AND Not Started Yet
        if (!isLoggedIn || hasStarted) return;

        // Ignore clicks if over UI
        if (IsPointerOverUI()) return;

        // Check Input (New Input System)
        bool pressed = false;

        // Check Mouse/Touch
        if (UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame)
            pressed = true;

        // Check Keyboard (Any Key)
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.anyKey.wasPressedThisFrame)
            pressed = true;

        if (pressed)
        {
            StartGameSequence();
        }
    }

    public void StartGameSequence()
    {
        hasStarted = true;

        // 1. Hide UI
        if (titleText) titleText.SetActive(false);
        if (pressStartText) pressStartText.SetActive(false);

        // 2. Hide WorldSpace Menu initially (Wait for camera)
        if (menuObjectToAnimate)
        {
            menuObjectToAnimate.SetActive(false);
            menuObjectToAnimate.transform.localScale = Vector3.zero; // Reset scale
        }

        // 3. Handle Director (Reset before disable)
        if (cameraDirector != null)
        {
            cameraDirector.Stop();
            cameraDirector.time = 0;
            cameraDirector.Evaluate(); // Force reset to frame 0
            cameraDirector.enabled = false;
        }

        // 4. Disable Animator
        if (cameraAnimator != null)
        {
            cameraAnimator.enabled = false;
        }

        // 5. Move Camera
        if (mainCamera != null)
        {
            // Move
            LeanTween.move(mainCamera.gameObject, targetPosition, 1.5f)
                .setEase(LeanTweenType.easeInOutCubic);

            // Rotate
            LeanTween.rotate(mainCamera.gameObject, targetRotation.eulerAngles, 1.5f)
                .setEase(LeanTweenType.easeInOutCubic)
                .setOnComplete(() =>
                {
                    Debug.Log("Camera Transition Complete. Opening Main Menu.");

                    // Animate Worldspace Menu Opening
                    if (menuObjectToAnimate)
                    {
                        Debug.Log($"[MainMenu] Worldspace Menu ({menuObjectToAnimate.name}) set to ACTIVE (User Pressed).");
                        menuObjectToAnimate.SetActive(true);
                        // Scale Up Animation (Open)
                        LeanTween.scale(menuObjectToAnimate, menuTargetScale, 0.6f)
                            .setEase(LeanTweenType.easeOutBack); // Bouncy open effect

                        // Optional: Ensure Alpha is 1 just in case
                        CanvasGroup cg = menuObjectToAnimate.GetComponent<CanvasGroup>();
                        if (cg != null) cg.alpha = 1f;

                        // Setup Buttons dynamically if not assigned, or just ensure listeners
                        SetupMenuButtons();
                    }
                });
        }
    }

    private void SetupMenuButtons()
    {
        if (playButton)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClicked);
        }
        if (settingsButton)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }
        if (exitButton)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(OnExitClicked);
        }
    }

    public void OnPlayClicked()
    {
        Debug.Log("[MainMenu] Play Clicked. Loading Scene Asynchronously...");
        
        // Hide Main Menu
        if (menuObjectToAnimate) menuObjectToAnimate.SetActive(false);

        // Show Loading Screen if Assigned
        if (loadingScreenUI != null)
        {
            loadingScreenUI.SetActive(true);
        }

        StartCoroutine(LoadGameplayScene());
    }

    private IEnumerator LoadGameplayScene()
    {
        // Add a slight delay just to let the Loading Screen UI pop up smoothly
        yield return new WaitForSeconds(0.2f);

        // Start Loading the Scene 
        AsyncOperation operation = SceneManager.LoadSceneAsync(gameplaySceneName);
        
        // Prevent activation immediately if you want to ensure the min loading time (Optional)
        // operation.allowSceneActivation = false; 

        float dotTimer = 0f;
        int dotCount = 0;

        while (!operation.isDone)
        {
            // Calculate progress (Unity's progress stops at 0.9f before activation)
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            // Update Slider if Assigned
            if (loadingSlider != null)
            {
                loadingSlider.value = progress;
            }

            // Optional Typing Animation for Text
            if (loadingText != null)
            {
                dotTimer += Time.deltaTime;
                if (dotTimer >= 0.3f) // Speed of adding dots
                {
                    dotTimer = 0f;
                    dotCount++;
                    if (dotCount > 3) dotCount = 0;

                    string dots = new string('.', dotCount);
                    loadingText.text = "Loading" + dots;
                }
            }

            yield return null;
        }

        // If you used operation.allowSceneActivation = false, you would check 
        // if progress >= 0.9f here and wait for user input or just set it to true.
    }

    public void OnSettingsClicked()
    {
        if (settingsController == null || menuObjectToAnimate == null) return;

        // 1. Disable Main Menu Interaction
        SetMenuButtonsInteractive(false);

        // 2. Animate Main Menu OUT
        LeanTween.scale(menuObjectToAnimate, Vector3.zero, 0.4f)
            .setEase(LeanTweenType.easeInBack)
            .setOnComplete(() =>
            {
                menuObjectToAnimate.SetActive(false);

                // 3. Move Camera to Settings Position
                MoveCameraToSettings();
            });
    }

    private void MoveCameraToSettings()
    {
        if (mainCamera == null) return;

        LeanTween.move(mainCamera.gameObject, settingsCameraPos, 1.0f).setEase(LeanTweenType.easeInOutCubic);
        LeanTween.rotate(mainCamera.gameObject, settingsCameraRot.eulerAngles, 1.0f)
            .setEase(LeanTweenType.easeInOutCubic)
            .setOnComplete(() =>
            {
                // 4. Open Settings Panel
                if (settingsController)
                {
                    settingsController.OpenSettings();
                    // Setup return callback if needed, or rely on public access
                    settingsController.onBack.RemoveAllListeners();
                    settingsController.onBack.AddListener(OnReturnFromSettings);
                }
            });
    }

    // Callback passed to SettingsController
    private void OnReturnFromSettings()
    {
        // 1. Settings Panel assumes it has already animated OUT by itself before calling this?
        // Or we call Close() here? Better: Settings Controller calls this EVENT when it closes.

        // 2. Move Camera Back to Main Menu
        MoveCameraToMain();
    }

    private void MoveCameraToMain()
    {
        if (mainCamera == null) return;

        LeanTween.move(mainCamera.gameObject, targetPosition, 1.0f).setEase(LeanTweenType.easeInOutCubic);
        LeanTween.rotate(mainCamera.gameObject, targetRotation.eulerAngles, 1.0f)
            .setEase(LeanTweenType.easeInOutCubic)
            .setOnComplete(() =>
            {
                // 3. Animate Main Menu IN
                if (menuObjectToAnimate)
                {
                    menuObjectToAnimate.SetActive(true);
                    LeanTween.scale(menuObjectToAnimate, menuTargetScale, 0.6f)
                        .setEase(LeanTweenType.easeOutBack)
                        .setOnComplete(() => SetMenuButtonsInteractive(true));
                }
            });
    }

    private void SetMenuButtonsInteractive(bool state)
    {
        if (playButton) playButton.interactable = state;
        if (settingsButton) settingsButton.interactable = state;
        if (exitButton) exitButton.interactable = state;
    }

    public void OnExitClicked()
    {
        Debug.Log("[MainMenu] Exit Clicked. Quitting...");
        Application.Quit();
    }

    private bool IsPointerOverUI()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }
        return false;
    }
}
