using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Cinemachine;

/// <summary>
/// ChangingRoom - Swaps between two complete Player GameObjects.
/// 
/// SETUP:
/// 1. Create two player objects in scene: Player1 (uniform) and Player1-Lab (labcoat)
/// 2. Player1-Lab should be INACTIVE initially
/// 3. Both players should have their own Animator, CharacterController, etc.
/// 4. Assign references in Inspector
/// </summary>
public class ChangingRoom : Interactable
{
    [Header("Changing Room Settings")]
    public float changeDuration = 2.5f;

    [Header("Player References")]
    [Tooltip("The current active player (Player1 with uniform).")]
    public GameObject player1;

    [Tooltip("The player to switch to (Player1-Lab with labcoat).")]
    public GameObject player1Lab;

    [Header("Camera Settings")]
    [Tooltip("The Cinemachine Virtual Camera that follows the player.")]
    public CinemachineVirtualCamera virtualCamera;

    [Tooltip("Optional: The camera's follow target on Player1-Lab (e.g., PlayerCameraRoot).")]
    public Transform player1LabCameraTarget;

    [Header("Components to Transfer (Optional)")]
    [Tooltip("If true, preserve velocity when swapping.")]
    public bool preserveVelocity = false;

    private bool hasChanged = false;
    private bool isChanging = false;
    private bool hasRevealedChecklist = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Jangan lakukan apapun jika pemain sedang/sudah ganti baju
            if (hasChanged || isChanging) return;

            // Trigger otomatis HANYA JIKA APD sudah lengkap.
            if (InventoryManager.Instance != null && InventoryManager.Instance.HasCompletePPE())
            {
                TryChangeClothes();
            }
        }
    }

    protected override void ExecuteInteraction()
    {
        TryChangeClothes();
    }

    private void TryChangeClothes()
    {
        Debug.Log("ChangingRoom: Interaction Started.");

        if (hasChanged || isChanging)
        {
            if (hasChanged)
            {
                Debug.Log("ChangingRoom: Already changed clothes.");
                HUDManager.Instance.ShowInteraction("Anda sudah siap masuk ke laboratorium.");
            }
            return;
        }

        if (InventoryManager.Instance != null)
        {
            if (InventoryManager.Instance.HasCompletePPE())
            {
                Debug.Log("ChangingRoom: PPE Complete. Starting change sequence.");
                isChanging = true;
                StartCoroutine(ChangeClothesRoutine());
            }
            else
            {
                Debug.Log("ChangingRoom: PPE Incomplete. Access Denied.");
                HUDManager.Instance.ShowInteraction("Dilarang Masuk! Lengkapi APD terlebih dahulu.");
                // Jika player ngeyel klik ruang ganti padahal belum ambil APD, kita bisa bantu memunculkannya juga.
                RevealChecklist();
            }
        }
    }

    private void RevealChecklist()
    {
        if (!hasRevealedChecklist && InventoryManager.Instance != null)
        {
            hasRevealedChecklist = true;
            InventoryManager.Instance.ShowChecklist();
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.UpdateObjective("Temukan dan gunakan semua APD pada daftar!");
            }
        }
    }

    private IEnumerator ChangeClothesRoutine()
    {
        // Validate references
        if (player1 == null || player1Lab == null)
        {
            Debug.LogError("ChangingRoom: Player references not set!");
            yield break;
        }

        // 1. Fade Out to Black
        if (ScreenFader.Instance) ScreenFader.Instance.FadeOut(0.5f);

        // 2. Wait while screen is black (simulating changing time)
        HUDManager.Instance.ShowInteraction("Mengganti Pakaian....");
        yield return new WaitForSeconds(0.5f); // Wait for fade to finish
        yield return new WaitForSeconds(changeDuration);

        // 3. Get Player1's current position and rotation
        Vector3 currentPosition = player1.transform.position;
        Quaternion currentRotation = player1.transform.rotation;

        // 4. Move Player1-Lab to Player1's position BEFORE activating
        player1Lab.transform.position = currentPosition;
        player1Lab.transform.rotation = currentRotation;

        // 5. Activate Player1-Lab
        player1Lab.SetActive(true);

        // Wait one frame for activation to propagate
        yield return null;

        // 6. Update Camera to follow Player1-Lab
        if (virtualCamera != null)
        {
            // Find camera target on Player1-Lab
            Transform newFollowTarget = player1LabCameraTarget;

            if (newFollowTarget == null)
            {
                // Try to find PlayerCameraRoot automatically
                newFollowTarget = player1Lab.transform.Find("PlayerCameraRoot");
            }

            if (newFollowTarget != null)
            {
                virtualCamera.Follow = newFollowTarget;
                // virtualCamera.LookAt = newFollowTarget; // Mencegah rotasi kamera rusak di StarterAssets
                Debug.Log($"ChangingRoom: Camera now following {newFollowTarget.name}");
            }
            else
            {
                // Fallback to player root
                virtualCamera.Follow = player1Lab.transform;
                // virtualCamera.LookAt = player1Lab.transform; // Mencegah rotasi kamera rusak
                Debug.LogWarning("ChangingRoom: PlayerCameraRoot not found, using player root.");
            }
        }

        // 7. Transfer any necessary state (optional)
        TransferPlayerState(player1, player1Lab);

        // 8. Handle PlayerInput - CRITICAL for movement to work!
        TransferPlayerInput(player1, player1Lab);

        // 9. Deactivate Player1
        player1.SetActive(false);

        Debug.Log("ChangingRoom: Player swap complete!");

        hasChanged = true;

        // 9. Fade In to Clear
        if (ScreenFader.Instance) ScreenFader.Instance.FadeIn(0.5f);

        yield return new WaitForSeconds(0.5f);
        HUDManager.Instance.ShowInteraction("APD Terpasang. Akses Lab Dibuka.");

        // Update Objective and Waypoint to Chemistry Lab
        HUDManager.Instance.UpdateObjective("Pergi ke Laboratorium Kimia.");
        if (GameplayManager.Instance != null && GameplayManager.Instance.chemLabWaypoint != null)
        {
            HUDManager.Instance.SetWaypointTarget(GameplayManager.Instance.chemLabWaypoint);
        }

        // Hide Checklist as requested
        if (InventoryManager.Instance) InventoryManager.Instance.HideChecklist();
    }

    /// <summary>
    /// Transfer state from old player to new player.
    /// Extend this method if you need to transfer more data.
    /// </summary>
    private void TransferPlayerState(GameObject oldPlayer, GameObject newPlayer)
    {
        // Transfer CharacterController state if applicable
        CharacterController oldCC = oldPlayer.GetComponent<CharacterController>();
        CharacterController newCC = newPlayer.GetComponent<CharacterController>();

        if (oldCC != null && newCC != null && preserveVelocity)
        {
            // Note: CharacterController doesn't expose velocity directly
            // If using Rigidbody, transfer velocity here
        }

        // Transfer Animator parameters if needed
        Animator oldAnimator = oldPlayer.GetComponent<Animator>();
        Animator newAnimator = newPlayer.GetComponent<Animator>();

        if (oldAnimator != null && newAnimator != null)
        {
            // Copy common parameters
            foreach (AnimatorControllerParameter param in oldAnimator.parameters)
            {
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Float:
                        newAnimator.SetFloat(param.nameHash, oldAnimator.GetFloat(param.nameHash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        newAnimator.SetInteger(param.nameHash, oldAnimator.GetInteger(param.nameHash));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        newAnimator.SetBool(param.nameHash, oldAnimator.GetBool(param.nameHash));
                        break;
                }
            }
        }

        Debug.Log("ChangingRoom: Player state transferred.");
    }

    /// <summary>
    /// Transfer PlayerInput from old player to new player.
    /// This is CRITICAL for the new player to receive input!
    /// </summary>
    private void TransferPlayerInput(GameObject oldPlayer, GameObject newPlayer)
    {
        // Get PlayerInput components
        PlayerInput oldInput = oldPlayer.GetComponent<PlayerInput>();
        PlayerInput newInput = newPlayer.GetComponent<PlayerInput>();

        if (oldInput != null && newInput != null)
        {
            // CRITICAL: Destroy the old PlayerInput component to avoid dual-player conflict
            // Just disabling it still keeps it in the Input System's player list
            Destroy(oldInput);

            // Make sure new input is enabled
            newInput.enabled = true;

            // Switch to KeyboardMouse control scheme explicitly
            newInput.SwitchCurrentControlScheme("KeyboardMouse", UnityEngine.InputSystem.Keyboard.current, UnityEngine.InputSystem.Mouse.current);

            // Activate the new player input
            newInput.ActivateInput();

            Debug.Log($"ChangingRoom: PlayerInput transferred. Control Scheme: {newInput.currentControlScheme}");
        }
        else
        {
            Debug.LogWarning("ChangingRoom: PlayerInput component not found on one or both players!");
        }

        // Also handle StarterAssetsInputs if present
        var oldStarterInput = oldPlayer.GetComponent<StarterAssets.StarterAssetsInputs>();
        var newStarterInput = newPlayer.GetComponent<StarterAssets.StarterAssetsInputs>();

        if (oldStarterInput != null && newStarterInput != null)
        {
            // Reset the new input to default state
            newStarterInput.move = Vector2.zero;
            newStarterInput.look = Vector2.zero;
            newStarterInput.jump = false;
            newStarterInput.sprint = false;

            Debug.Log("ChangingRoom: StarterAssetsInputs reset on new player.");
        }
    }
}
