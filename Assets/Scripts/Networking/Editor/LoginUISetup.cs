using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro; // TMP Namespace

public class LoginUISetup : EditorWindow
{
    [MenuItem("LabShield/Setup Login UI (TMP)")]
    public static void Setup()
    {
        // 1. Find or Create Component Holder (Manager)
        GameObject networkManagerGO = GameObject.Find("NetworkManager");
        if (networkManagerGO == null)
        {
            networkManagerGO = new GameObject("NetworkManager");
            Undo.RegisterCreatedObjectUndo(networkManagerGO, "Create NetworkManager");
        }

        // 2. Ensure AuthManager exists (Singleton Logic handles setup)
        if (!networkManagerGO.GetComponent<AuthManager>()) networkManagerGO.AddComponent<AuthManager>();

        // 3. Find or Create Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
        }

        // 4. Create Login Panel
        GameObject loginPanel = CreatePanel("LoginPanel", canvas.transform);
        loginPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.85f); // Dark BG
        // Add CanvasGroup for LeanTween Fading
        if (!loginPanel.GetComponent<CanvasGroup>()) loginPanel.AddComponent<CanvasGroup>();

        // Link LoginUI
        LoginUI loginUI = loginPanel.GetComponent<LoginUI>();
        if (!loginUI) loginUI = loginPanel.AddComponent<LoginUI>();

        // 5. Create UI Elements (TMPro)
        GameObject titleText = CreateTextTMP("Title", "LABSHIELD PROTOCOL", loginPanel.transform, new Vector2(0, 150), 36);
        
        GameObject userField = CreateInputFieldTMP("UsernameInput", "Username...", loginPanel.transform, new Vector2(0, 50));
        GameObject passField = CreateInputFieldTMP("PasswordInput", "Password...", loginPanel.transform, new Vector2(0, -10));
        passField.GetComponent<TMP_InputField>().contentType = TMP_InputField.ContentType.Password;

        GameObject loginBtn = CreateButtonTMP("LoginButton", "LOGIN", loginPanel.transform, new Vector2(0, -80));
        GameObject regBtn = CreateButtonTMP("RegisterButton", "Register Account", loginPanel.transform, new Vector2(0, -140));
        
        GameObject statusText = CreateTextTMP("StatusText", "Ready", loginPanel.transform, new Vector2(0, -200), 18);
        statusText.GetComponent<TextMeshProUGUI>().color = Color.white;

        // 6. Link References
        loginUI.loginPanel = loginPanel;
        loginUI.usernameInput = userField.GetComponent<TMP_InputField>();
        loginUI.passwordInput = passField.GetComponent<TMP_InputField>();
        loginUI.loginButton = loginBtn.GetComponent<Button>();
        loginUI.registerButton = regBtn.GetComponent<Button>();
        loginUI.statusText = statusText.GetComponent<TextMeshProUGUI>();

        Selection.activeGameObject = loginPanel;
        Debug.Log("<color=cyan>LabShield: Login UI (TMPro + LeanTween) Generated!</color>");
    }

    private static GameObject CreatePanel(string name, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image img = panel.AddComponent<Image>();
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return panel;
    }

    private static GameObject CreateInputFieldTMP(string name, string placeholder, Transform parent, Vector2 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image bg = go.AddComponent<Image>();
        bg.color = new Color(1, 1, 1, 0.1f); // Semi Transparent White

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(350, 50);
        rt.anchoredPosition = pos;

        TMP_InputField input = go.AddComponent<TMP_InputField>();
        
        // Text Area
        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(go.transform, false);
        RectTransform taRT = textArea.AddComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(10, 6); taRT.offsetMax = new Vector2(-10, -6);
        RectMask2D mask = textArea.AddComponent<RectMask2D>();

        // Placeholder
        GameObject phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI phText = phGO.AddComponent<TextMeshProUGUI>();
        phText.text = placeholder;
        phText.fontSize = 24;
        phText.color = new Color(1, 1, 1, 0.5f);
        phText.alignment = TextAlignmentOptions.Left;
        phText.enableWordWrapping = false;
        RectTransform phRT = phGO.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;

        // Text
        GameObject tGO = new GameObject("Text");
        tGO.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI tText = tGO.AddComponent<TextMeshProUGUI>();
        tText.text = "";
        tText.fontSize = 24;
        tText.color = Color.white;
        tText.alignment = TextAlignmentOptions.Left;
        tText.enableWordWrapping = false;
        RectTransform tRT = tGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = Vector2.zero; tRT.offsetMax = Vector2.zero;

        input.textViewport = taRT;
        input.textComponent = tText;
        input.placeholder = phText;
        input.targetGraphic = bg;
        
        // Setup Colors for Input
        ColorBlock cb = input.colors;
        cb.highlightedColor = new Color(0, 1, 1, 0.2f); // Cyan Highlight
        input.colors = cb;

        return go;
    }

    private static GameObject CreateButtonTMP(string name, string label, Transform parent, Vector2 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = new Color(0, 1, 1, 0.8f); // Cyan
        
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 50);
        rt.anchoredPosition = pos;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 24;
        text.color = Color.black;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        
        return go;
    }

    private static GameObject CreateTextTMP(string name, string content, Transform parent, Vector2 pos, float size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.color = new Color(0, 1, 1); // Cyan
        text.alignment = TextAlignmentOptions.Center;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(500, 60);
        rt.anchoredPosition = pos;
        return go;
    }
}
