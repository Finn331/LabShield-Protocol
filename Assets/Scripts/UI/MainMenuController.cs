using UnityEngine;
using UnityEngine.Playables;
using TMPro;

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

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // Start State: Hide "Press Start" until Login is done
        // if (titleText) titleText.SetActive(false); // CHANGED: Keep Title Visible!

        if (pressStartText) pressStartText.SetActive(false);

        // Ensure Worldspace Menu is hidden initially
        if (worldSpaceMenuCanvas) worldSpaceMenuCanvas.SetActive(false);

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

        // 2. Activate Worldspace Menu (Tomb Raider Style)
        if (worldSpaceMenuCanvas)
        {
            worldSpaceMenuCanvas.SetActive(true);

            // Optional: Fade in if it has a CanvasGroup
            CanvasGroup cg = worldSpaceMenuCanvas.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0;
                LeanTween.alphaCanvas(cg, 1f, 1.5f).setEase(LeanTweenType.easeInOutCubic);
            }
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
                    Debug.Log("Camera Transition Complete. Gameplay Phase.");
                    // Gameplay starts now. Login is already done.
                });
        }
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
