using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIVirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [System.Serializable]
    public class Event : UnityEvent<Vector2> { }
    
    [Header("Rect References")]
    public RectTransform containerRect;
    public RectTransform handleRect;

    [Header("Touch Area")]
    [Tooltip("If assigned, touches within this RectTransform will spawn and control the joystick.")]
    public RectTransform joystickTouchArea;

    [Header("Settings")]
    public float joystickRange = 50f;
    public float magnitudeMultiplier = 1f;
    public bool invertXOutputValue;
    public bool invertYOutputValue;

    [Header("Advanced Settings (Mobile)")]
    public bool isFloatingJoystick = false;
    public bool enableDragToSprint = false;
    [Tooltip("Tarik handle melebihi rentang wajar (misal 1.0 - 1.5) ke arah manapun untuk memicu Sprint")]
    public float sprintThreshold = 1.0f;

    [Header("Output")]
    public Event joystickOutputEvent;
    public UnityEvent<bool> sprintOutputEvent;

    [Header("Swipe Camera Mode")]
    public bool isSwipeCamera = false;
    [Tooltip("Sensitivitas sapuan kamera. Disarankan nilai kecil seperti 0.015 hingga 0.05. Ubah ini jika kamera terlalu liar.")]
    public float swipeSensitivity = 0.015f;
    private Vector2 _lastTouchPosition;

    // Internal
    private Vector2 _defaultContainerAnchoredPos;
    private Vector2 _defaultHandleAnchoredPos;
    private CanvasGroup _containerCanvasGroup;
    private CanvasGroup _handleCanvasGroup;
    private bool _isPointerDown = false;
    private int _dragFrame = -1;

    void Start()
    {
        // === Auto-link referensi yang hilang akibat Prefab issue ===
        if (containerRect == null)
        {
            Transform bg = transform.Find("Joystick_Background");
            if (bg != null) containerRect = bg.GetComponent<RectTransform>();
        }
        if (handleRect == null)
        {
            Transform h = transform.Find("Image_Handle");
            if (h != null) handleRect = h.GetComponent<RectTransform>();
        }

        // === Paksa pengaturan untuk joystick pergerakan (Kiri Layar) ===
        if (gameObject.name == "UI_Virtual_Joystick_Move")
        {
            isFloatingJoystick = true;
            enableDragToSprint = true;

            var canvasInput = GetComponentInParent<StarterAssets.UICanvasControllerInput>();
            if (canvasInput == null) canvasInput = FindObjectOfType<StarterAssets.UICanvasControllerInput>();
            if (canvasInput != null)
            {
                sprintOutputEvent.AddListener(canvasInput.VirtualSprintInput);
                Debug.Log("[UIVirtualJoystick] Sprint event auto-wired ke UICanvasControllerInput");
            }
        }

        // === Paksa pengaturan untuk Camera Swipe (Kanan Layar) ===
        if (gameObject.name == "UI_Virtual_Joystick_Look")
        {
            isSwipeCamera = true;
            isFloatingJoystick = false; // Swipe murni, tidak butuh mekanik memindah background

            var canvasInput = GetComponentInParent<StarterAssets.UICanvasControllerInput>();
            if (canvasInput == null) canvasInput = FindObjectOfType<StarterAssets.UICanvasControllerInput>();
            if (canvasInput != null)
            {
                joystickOutputEvent.AddListener(canvasInput.VirtualLookInput);
            }
        }

        // === Simpan posisi default ===
        if (containerRect != null) _defaultContainerAnchoredPos = containerRect.anchoredPosition;
        if (handleRect != null) _defaultHandleAnchoredPos = handleRect.anchoredPosition;

        // === Setup CanvasGroup untuk show/hide visual joystick ===
        _containerCanvasGroup = EnsureCanvasGroup(containerRect);
        _handleCanvasGroup = EnsureCanvasGroup(handleRect);

        if (isFloatingJoystick || isSwipeCamera)
        {
            // Sembunyikan joystick visual di awal (swipe camera akan selalu tersembunyi visual joysticknya)
            SetJoystickVisibility(0f);
        }

        // === GARANSI TOUCH AREA AKTIF ===
        // Pastikan parent ini (area sentuh) bisa menerima Raycast walaupun visualnya transparan
        Image touchArea = GetComponent<Image>();
        if (touchArea != null)
        {
            touchArea.raycastTarget = true;
            if (touchArea.color.a == 0f)
            {
                Color c = touchArea.color; c.a = 0.005f; touchArea.color = c;
            }
        }
        CanvasRenderer cr = GetComponent<CanvasRenderer>();
        if (cr != null) cr.cullTransparentMesh = false;

        // Pastikan background & handle joystick tidak memakan raycast dari jari
        if (containerRect != null && containerRect.gameObject != this.gameObject)
        {
            Image bg = containerRect.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = false;
        }
        if (handleRect != null && handleRect.gameObject != this.gameObject)
        {
            Image hd = handleRect.GetComponent<Image>();
            if (hd != null) hd.raycastTarget = false;
        }

        SetupHandle();

        // === Konfigurasi Area Sentuh Kustom ===
        if (joystickTouchArea != null && joystickTouchArea.gameObject != this.gameObject)
        {
            // Pastikan area sentuh memiliki Image untuk menangkap raycast
            Image areaImage = joystickTouchArea.GetComponent<Image>();
            if (areaImage == null)
            {
                areaImage = joystickTouchArea.gameObject.AddComponent<Image>();
                areaImage.color = new Color(0, 0, 0, 0); // Transparan
            }
            areaImage.raycastTarget = true;

            // Pastikan event trigger ada
            EventTrigger trigger = joystickTouchArea.GetComponent<EventTrigger>();
            if (trigger == null) trigger = joystickTouchArea.gameObject.AddComponent<EventTrigger>();

            // Teruskan PointerDown
            EventTrigger.Entry pdEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pdEntry.callback.AddListener((data) => { OnPointerDown((PointerEventData)data); });
            trigger.triggers.Add(pdEntry);

            // Teruskan Drag
            EventTrigger.Entry dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            dragEntry.callback.AddListener((data) => { OnDrag((PointerEventData)data); });
            trigger.triggers.Add(dragEntry);

            // Teruskan PointerUp
            EventTrigger.Entry puEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            puEntry.callback.AddListener((data) => { OnPointerUp((PointerEventData)data); });
            trigger.triggers.Add(puEntry);

            // Matikan raycast pada joystick object ini sendiri agar tidak double (hanya jika areanya berbeda)
            if (touchArea != null) touchArea.raycastTarget = false;
        }
    }

    void Update()
    {
        if (isSwipeCamera && _isPointerDown)
        {
            // Jika jari ditekan namun tidak bergerak, event OnDrag tidak dipanggil oleh Unity.
            // Hal ini memicu nilai delta dari frame sebelumnya terus berlanjut tanpa batas (berputar terus seperti joystick).
            // Solusi: Jika tidak ada drag pada frame ini, set paksa delta ke 0, yang merupakan karakter asli "Swipe".
            if (Time.frameCount != _dragFrame)
            {
                OutputPointerEventValue(Vector2.zero);
            }
        }
    }

    private CanvasGroup EnsureCanvasGroup(RectTransform rt)
    {
        if (rt == null) return null;
        var cg = rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
        
        // Hanya nonaktifkan raycast jika rt ini bukan area sentuh utama
        if (rt.gameObject != this.gameObject)
        {
            cg.blocksRaycasts = false;
        }
        
        return cg;
    }

    private void SetJoystickVisibility(float alpha)
    {
        if (_containerCanvasGroup != null) _containerCanvasGroup.alpha = alpha;
        if (_handleCanvasGroup != null) _handleCanvasGroup.alpha = alpha;
    }

    private void SetupHandle()
    {
        if (handleRect) UpdateHandleRectPosition(Vector2.zero);
    }

    // ========== POINTER EVENTS ==========

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPointerDown = true;

        if (isSwipeCamera && containerRect != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                containerRect, eventData.position, eventData.pressEventCamera, out _lastTouchPosition);
            return;
        }

        if (isFloatingJoystick && containerRect != null)
        {
            RectTransform parentRect = containerRect.parent as RectTransform;
            if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, eventData.position, eventData.pressEventCamera, out Vector2 localTouchPos))
            {
                // Gunakan localPosition karena localTouchPos dihitung relatif terhadap pivot parent.
                // Menggunakan anchoredPosition akan menyebabkan offset bila pivot parent berbeda.
                containerRect.localPosition = new Vector3(localTouchPos.x, localTouchPos.y, containerRect.localPosition.z);
                UpdateHandleRectPosition(Vector2.zero);
            }
            SetJoystickVisibility(1f);
        }

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (containerRect == null) return;

        if (isSwipeCamera)
        {
            _dragFrame = Time.frameCount; // Tandai bahwa frame ini jari bergerak
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                containerRect, eventData.position, eventData.pressEventCamera, out Vector2 currentPos))
            {
                Vector2 delta = currentPos - _lastTouchPosition;
                _lastTouchPosition = currentPos;
                
                // Solusinya: Kita menyesuaikan pembagi berdasarkan apakah menggunakan Mouse (Editor testing) atau Touch (Mobile).
                // Di ThirdPersonController, Mouse tidak dikali Time.deltaTime, namun Touch dikali.
                Vector2 scaledDelta = delta;
#if ENABLE_INPUT_SYSTEM
                bool isMouse = false;
                var pInput = FindObjectOfType<UnityEngine.InputSystem.PlayerInput>();
                if (pInput != null) isMouse = pInput.currentControlScheme == "KeyboardMouse";
                
                if (!isMouse) scaledDelta = delta / Time.deltaTime;
#else
                scaledDelta = delta / Time.deltaTime;
#endif
                
                // Gunakan swipeSensitivity khusus untuk mengatur kecepatan
                Vector2 output = ApplyInversionFilter(scaledDelta * swipeSensitivity); 
                OutputPointerEventValue(output);
            }
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            containerRect, eventData.position, eventData.pressEventCamera, out Vector2 position);
        
        position = ApplySizeDelta(position);
        Vector2 clampedPosition = ClampValuesToMagnitude(position);
        Vector2 outputPosition = ApplyInversionFilter(clampedPosition);

        OutputPointerEventValue(outputPosition * magnitudeMultiplier);

        if (enableDragToSprint && sprintOutputEvent != null)
        {
            bool isSprinting = position.magnitude >= sprintThreshold;
            sprintOutputEvent.Invoke(isSprinting);
        }

        if (handleRect) UpdateHandleRectPosition(clampedPosition * joystickRange);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPointerDown = false;
        OutputPointerEventValue(Vector2.zero);

        if (isSwipeCamera) return; // Mode swipe camera tidak perlu mereset posisi

        if (enableDragToSprint && sprintOutputEvent != null) sprintOutputEvent.Invoke(false);
        if (handleRect) UpdateHandleRectPosition(Vector2.zero);

        if (isFloatingJoystick && containerRect != null)
        {
            containerRect.anchoredPosition = _defaultContainerAnchoredPos;
            SetJoystickVisibility(0f);
        }
    }

    // ========== HELPERS ==========

    private void OutputPointerEventValue(Vector2 pointerPosition)
    {
        joystickOutputEvent.Invoke(pointerPosition);
    }

    private void UpdateHandleRectPosition(Vector2 newPosition)
    {
        if (handleRect == null) return;

        // Jika handle dan container bukan parent-child (sibling dalam Prefab),
        // offset posisinya agar seolah-olah parented.
        if (containerRect != null && handleRect.parent != containerRect && handleRect.parent == containerRect.parent)
        {
            handleRect.anchoredPosition = newPosition + containerRect.anchoredPosition;
        }
        else
        {
            handleRect.anchoredPosition = newPosition;
        }
    }

    Vector2 ApplySizeDelta(Vector2 position)
    {
        float x = (position.x / containerRect.sizeDelta.x) * 2.5f;
        float y = (position.y / containerRect.sizeDelta.y) * 2.5f;
        return new Vector2(x, y);
    }

    Vector2 ClampValuesToMagnitude(Vector2 position)
    {
        return Vector2.ClampMagnitude(position, 1);
    }

    Vector2 ApplyInversionFilter(Vector2 position)
    {
        if (invertXOutputValue) position.x = -position.x;
        if (invertYOutputValue) position.y = -position.y;
        return position;
    }
}