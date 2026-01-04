using UnityEngine;
using UnityEngine.UI;
using TMPro; // Added TMPro

public class LoginUI : MonoBehaviour
{
    [Header("References")]
    // public AuthManager authManager; // Removed: We use Singleton now
    public GameObject loginPanel;
    public GameObject mainMenuPanel;
    public TMP_InputField usernameInput; // Changed to TMP
    public TMP_InputField passwordInput; // Changed to TMP
    public TMP_Text statusText; // Changed to TMP
    public Button loginButton;
    public Button registerButton;

    [Header("Settings")]
    public bool showOnStart = true; // Default TRUE for "Login First" flow

    void Start()
    {
        // Add Listeners
        if (loginButton) loginButton.onClick.AddListener(OnLoginClicked);
        if (registerButton) registerButton.onClick.AddListener(OnRegisterClicked);

        // Initial State
        // Force show login if not logged in (Override Inspector 'false' if needed for this flow)
        if (showOnStart || !AuthManager.IsLoggedIn)
        {
            ShowLogin();
        }
        else
        {
            SetPanelActive(loginPanel, false, true);
        }

        SetPanelActive(mainMenuPanel, true, true); // Always Active
    }

    public void ShowLogin()
    {
        bool loggedIn = AuthManager.IsLoggedIn;
        SetPanelActive(loginPanel, !loggedIn, false); // Animate In
    }

    void UpdateUIState()
    {
        bool loggedIn = AuthManager.IsLoggedIn;

        // Use LeanTween for smooth transition
        if (!loggedIn)
        {
            // Show Login (Overlay)
            SetPanelActive(loginPanel, true);

            // Focus Username
            if (usernameInput) usernameInput.Select();
        }
        else
        {
            // Hide Login (Overlay)
            SetPanelActive(loginPanel, false);
            // MainMenu stays active
        }
    }

    // Helper for Tweening CanvasGroup (better than SetActive)
    void SetPanelActive(GameObject panel, bool active, bool immediate = false)
    {
        if (!panel) return;

        // Ensure CanvasGroup exists for fading
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (!cg) cg = panel.AddComponent<CanvasGroup>();

        if (immediate)
        {
            panel.SetActive(active);
            cg.alpha = active ? 1 : 0;
            cg.interactable = active;
            cg.blocksRaycasts = active;
            panel.transform.localScale = active ? Vector3.one : Vector3.zero;
        }
        else
        {
            if (active)
            {
                panel.SetActive(true);
                cg.interactable = true;
                cg.blocksRaycasts = true;

                // Reset start pos
                panel.transform.localScale = Vector3.one * 0.8f;
                cg.alpha = 0;

                // Animate In
                LeanTween.alphaCanvas(cg, 1f, 0.4f).setEaseOutCubic();
                LeanTween.scale(panel, Vector3.one, 0.4f).setEaseOutBack();
            }
            else
            {
                cg.interactable = false;
                cg.blocksRaycasts = false;

                // Animate Out
                LeanTween.alphaCanvas(cg, 0f, 0.3f).setEaseInCubic().setOnComplete(() =>
                {
                    panel.SetActive(false);
                });
                LeanTween.scale(panel, Vector3.one * 0.9f, 0.3f).setEaseInCubic();
            }
        }
    }

    void OnLoginClicked()
    {
        // Button Punch Animation
        if (loginButton) LeanTween.scale(loginButton.gameObject, Vector3.one * 0.9f, 0.1f).setLoopPingPong(1);

        if (AuthManager.Instance == null)
        {
            SetStatus("Error: AuthManager not found!", true);
            return;
        }

        string u = usernameInput.text;
        string p = passwordInput.text;

        if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
        {
            SetStatus("Please enter username and password.", true);
            // Shake input fields
            if (usernameInput) LeanTween.moveX(usernameInput.gameObject, usernameInput.transform.position.x + 10, 0.05f).setLoopPingPong(3);
            return;
        }

        SetStatus("Logging in...", false);
        loginButton.interactable = false;

        AuthManager.Instance.Login(u, p, (msg) =>
        {
            if (loginButton) loginButton.interactable = true;
            if (msg == "Success")
            {
                SetStatus("Login Successful!", false);
                UpdateUIState();

                // Trigger Main Menu "Press Start"
                MainMenuController mm = FindFirstObjectByType<MainMenuController>();
                if (mm == null) mm = FindObjectOfType<MainMenuController>();
                if (mm != null) mm.OnLoginSuccess();
            }
            else
            {
                // Clean Error Message (No "Login Failed:" prefix)
                // Maps "Incorrect password" -> "Wrong Password" to match user request exactly if needed, 
                // but usually server message is fine. Checking user request:
                // "akan menampilkan Wrong Password" -> Server sends "Incorrect password". Close enough?
                // Let's replace it to be exact.

                string displayMsg = msg;
                if (msg == "Incorrect password") displayMsg = "Wrong Password";
                if (msg == "User not found") displayMsg = "Invalid User";

                SetStatus(displayMsg, true);
                // Shake Panel on Error
                LeanTween.moveX(loginPanel, loginPanel.transform.position.x + 10, 0.05f).setLoopPingPong(3);
            }
        });
    }

    void OnRegisterClicked()
    {
        if (AuthManager.Instance)
        {
            AuthManager.Instance.OpenRegisterPage();
            SetStatus("Opened Registration Page in Browser.", false);
        }
    }

    void SetStatus(string msg, bool isError)
    {
        if (statusText)
        {
            statusText.text = msg;
            statusText.color = isError ? Color.red : Color.green;

            // Pop text animation
            statusText.transform.localScale = Vector3.one;
            LeanTween.scale(statusText.gameObject, Vector3.one * 1.2f, 0.1f).setLoopPingPong(1);
        }
        Debug.Log(msg);
    }
}
