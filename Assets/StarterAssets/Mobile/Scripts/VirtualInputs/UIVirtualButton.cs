using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class UIVirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [System.Serializable]
    public class BoolEvent : UnityEvent<bool> { }
    [System.Serializable]
    public class Event : UnityEvent { }

    [Header("Output")]
    public BoolEvent buttonStateOutputEvent;
    public Event buttonClickOutputEvent;

    private void Start()
    {
        AutoWireToCanvasInputIfNeeded();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OutputButtonStateValue(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        OutputButtonStateValue(false);
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        OutputButtonClickEvent();
    }

    void OutputButtonStateValue(bool buttonState)
    {
        buttonStateOutputEvent.Invoke(buttonState);
    }

    void OutputButtonClickEvent()
    {
        buttonClickOutputEvent.Invoke();
    }

    private void AutoWireToCanvasInputIfNeeded()
    {
        if (buttonStateOutputEvent == null) return;
        if (buttonStateOutputEvent.GetPersistentEventCount() > 0) return;

        var canvasInput = GetComponentInParent<StarterAssets.UICanvasControllerInput>();
        if (canvasInput == null)
        {
            canvasInput = FindFirstObjectByType<StarterAssets.UICanvasControllerInput>(FindObjectsInactive.Include);
        }

        if (canvasInput == null) return;

        if (name == "UI_Virtual_Button_Jump")
        {
            buttonStateOutputEvent.AddListener(canvasInput.VirtualJumpInput);
            Debug.Log("[UIVirtualButton] Jump event auto-wired ke UICanvasControllerInput");
            return;
        }

        if (name == "UI_Virtual_Button_Sprint")
        {
            buttonStateOutputEvent.AddListener(canvasInput.VirtualSprintInput);
            Debug.Log("[UIVirtualButton] Sprint event auto-wired ke UICanvasControllerInput");
        }
    }

}
