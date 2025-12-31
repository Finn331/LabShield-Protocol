using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class SetupGameplay
{
    [MenuItem("Tools/Setup Gameplay Scene")]
    public static void Setup()
    {
        // 1. Setup Canvas
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("Canvas");
            canvasObj.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
        }

        // 2. Setup HUD Manager
        HUDManager hud = Object.FindAnyObjectByType<HUDManager>();
        if (hud == null)
        {
            GameObject hudObj = new GameObject("HUDManager");
            hud = hudObj.AddComponent<HUDManager>();
            Undo.RegisterCreatedObjectUndo(hudObj, "Create HUDManager");
        }

        // 3. Setup Inventory Manager
        InventoryManager inv = Object.FindAnyObjectByType<InventoryManager>();
        if (inv == null)
        {
            GameObject invObj = new GameObject("InventoryManager");
            inv = invObj.AddComponent<InventoryManager>();
            Undo.RegisterCreatedObjectUndo(invObj, "Create InventoryManager");
        }

        // 4. Create UI Structure
        // Objective Panel (Top Center)
        if (hud.objectivePanel == null)
        {
            GameObject panel = CreatePanel(canvasObj.transform, "Objective Panel", new Vector2(0, -50), new Vector2(800, 100));
            var img = panel.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.7f);
            hud.objectivePanel = panel;

            GameObject textObj = CreateText(panel.transform, "Objective Text", "Mission Objective...");
            hud.objectiveText = textObj.GetComponent<TextMeshProUGUI>();
        }

        // Checklist Container (Top Left)
        // Note: Field 'checklistContainer' was removed in favor of manual 'checklistLinks'. 
        // We will just ensure the UI structure exists for manual reference.
        GameObject container = GameObject.Find("Checklist Container");
        if (container == null)
        {
            container = CreatePanel(canvasObj.transform, "Checklist Container", new Vector2(250, -200), new Vector2(400, 300));
            // Top Left Anchor
            RectTransform rt = container.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(20, -20);

            var vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(10, 10, 10, 10);

            // Create 4 Template Texts for the user to link
            for (int i = 0; i < 4; i++)
            {
                GameObject template = CreateText(container.transform, $"Objective Text {i + 1}", "Item Name Placeholder");
                template.GetComponent<TextMeshProUGUI>().fontSize = 20;
            }

            Debug.Log("Created Checklist Container and 4 Template Texts. Please assign them to 'Checklist Links' in InventoryManager.");
        }

        // Mobile Button (Bottom Right)
        if (hud.mobileInteractButton == null)
        {
            GameObject btnObj = new GameObject("InteractButton");
            btnObj.transform.SetParent(canvasObj.transform, false);
            Image img = btnObj.AddComponent<Image>();
            img.color = Color.cyan;
            Button btn = btnObj.AddComponent<Button>();

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-50, 50);
            rt.sizeDelta = new Vector2(150, 150);

            GameObject txtObj = CreateText(btnObj.transform, "Text", "INTERACT");
            hud.mobileButtonText = txtObj.GetComponent<TextMeshProUGUI>();
            hud.mobileInteractButton = btn;

            // Add Listener logic 
            // We can't easily add runtime events in Editor script without using UnityEventTools which might be verbose
            // User will have to link it manually or we trust they do it.
        }

        // 5. Setup Camera
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            PlayerInteraction playerInt = mainCam.GetComponent<PlayerInteraction>();
            if (playerInt == null)
            {
                playerInt = mainCam.gameObject.AddComponent<PlayerInteraction>();
                playerInt.interactionLayer = LayerMask.GetMask("Default"); // Default
                Undo.RegisterCreatedObjectUndo(playerInt, "Add PlayerInteraction");
            }
        }

        Debug.Log("Gameplay Scene Setup Complete! Please manually link the 'InteractButton' OnClick event to PlayerInteraction.InteractByUI if not already done.");
    }

    static GameObject CreatePanel(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return obj;
    }

    static GameObject CreateText(Transform parent, string name, string content)
    {
        GameObject obj = new GameObject(name);
        if (parent) obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return obj;
    }
}
