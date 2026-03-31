using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System;
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
    private const string PrefSelectedCharacterIndex = "SelectedCharacter";
    private const string PrefSelectedCharacterName = "SelectedCharacterName";
    private const string PrefSelectedCharacterKey = "SelectedCharacterKey";

    [Header("Changing Room Settings")]
    public float changeDuration = 2.5f;

    [Header("Player References")]
    [Tooltip("The current active player (Player1 with uniform).")]
    public GameObject player1;

    [Tooltip("The player to switch to (Player1-Lab with labcoat).")]
    public GameObject player1Lab;

    [Header("Variant Lists (Optional)")]
    [Tooltip("Daftar semua karakter uniform. Jika kosong, script akan auto-cari dari scene.")]
    public List<GameObject> uniformPlayerVariants = new List<GameObject>();

    [Tooltip("Daftar semua karakter labcoat. Jika kosong, script akan auto-cari dari scene.")]
    public List<GameObject> labPlayerVariants = new List<GameObject>();

    [Tooltip("Auto deteksi varian dari scene berdasarkan nama object + PlayerIdentity.")]
    public bool autoResolveVariants = true;

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

    private void Awake()
    {
        ResolveSelectedCharacterVariant();
        BindSceneSystemsToActivePlayer(player1);
    }

    private void Start()
    {
        // Safety pass after all scene objects are initialized.
        BindSceneSystemsToActivePlayer(player1);
    }

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

    private void ResolveSelectedCharacterVariant()
    {
        if (autoResolveVariants)
        {
            AutoCollectVariantListsIfNeeded();
        }

        if (uniformPlayerVariants.Count == 0 && player1 != null)
        {
            uniformPlayerVariants.Add(player1);
        }

        if (labPlayerVariants.Count == 0 && player1Lab != null)
        {
            labPlayerVariants.Add(player1Lab);
        }

        if (uniformPlayerVariants.Count == 0 || labPlayerVariants.Count == 0)
        {
            Debug.LogWarning("ChangingRoom: Variant list uniform/lab masih kosong. Pakai referensi default inspector.");
            return;
        }

        int selectedCharacter = PlayerPrefs.GetInt(PrefSelectedCharacterIndex, 0);
        string selectedCharacterName = PlayerPrefs.GetString(PrefSelectedCharacterName, string.Empty);
        string selectedCharacterKey = PlayerPrefs.GetString(PrefSelectedCharacterKey, string.Empty);

        if (!TryResolveSelectedVariants(selectedCharacter, selectedCharacterName, selectedCharacterKey, out GameObject resolvedUniform, out GameObject resolvedLab))
        {
            int uniformIndex = Mod(selectedCharacter, uniformPlayerVariants.Count);
            int labIndex = Mod(selectedCharacter, labPlayerVariants.Count);
            resolvedUniform = uniformPlayerVariants[uniformIndex];
            resolvedLab = labPlayerVariants[labIndex];
        }

        player1 = resolvedUniform;
        player1Lab = resolvedLab;

        ApplyInitialVariantActivationState();
        ResolveCameraTargetForLabVariant();

        Debug.Log($"ChangingRoom: SelectedCharacter={selectedCharacter}, Uniform={SafeName(player1)}, Lab={SafeName(player1Lab)}");
    }

    private void AutoCollectVariantListsIfNeeded()
    {
        PlayerIdentity[] identities = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        List<GameObject> foundUniform = new List<GameObject>();
        List<GameObject> foundLab = new List<GameObject>();

        for (int i = 0; i < identities.Length; i++)
        {
            PlayerIdentity identity = identities[i];
            if (identity == null || identity.gameObject == null) continue;

            GameObject candidate = identity.gameObject;
            bool isLab = IsLikelyLabVariant(candidate);
            bool isUniform = IsLikelyUniformVariant(candidate);

            if (isLab)
            {
                foundLab.Add(candidate);
            }
            else if (isUniform || !isLab)
            {
                foundUniform.Add(candidate);
            }
        }

        foundUniform.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        foundLab.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

        if (foundUniform.Count > 0) uniformPlayerVariants = foundUniform;
        if (foundLab.Count > 0) labPlayerVariants = foundLab;

        Debug.Log($"ChangingRoom AutoCollect -> Uniform:{uniformPlayerVariants.Count}, Lab:{labPlayerVariants.Count}");
    }

    private void ApplyInitialVariantActivationState()
    {
        for (int i = 0; i < uniformPlayerVariants.Count; i++)
        {
            GameObject variant = uniformPlayerVariants[i];
            if (variant != null)
            {
                bool isSelectedUniform = variant == player1;
                variant.SetActive(isSelectedUniform);
                SetPlayerInputEnabled(variant, isSelectedUniform);
            }
        }

        for (int i = 0; i < labPlayerVariants.Count; i++)
        {
            GameObject variant = labPlayerVariants[i];
            if (variant != null)
            {
                variant.SetActive(false);
                SetPlayerInputEnabled(variant, false);
            }
        }
    }

    private void ResolveCameraTargetForLabVariant()
    {
        if (player1Lab == null)
        {
            player1LabCameraTarget = null;
            return;
        }

        Transform found = player1Lab.transform.Find("PlayerCameraRoot");
        if (found != null)
        {
            player1LabCameraTarget = found;
        }
    }

    private static int Mod(int value, int count)
    {
        if (count <= 0) return 0;
        int result = value % count;
        if (result < 0) result += count;
        return result;
    }

    private static string SafeName(GameObject go)
    {
        return go == null ? "(null)" : go.name;
    }

    private bool TryResolveSelectedVariants(int selectedCharacterIndex, string selectedCharacterName, string selectedCharacterKey, out GameObject resolvedUniform, out GameObject resolvedLab)
    {
        resolvedUniform = null;
        resolvedLab = null;

        if (!string.IsNullOrWhiteSpace(selectedCharacterName))
        {
            resolvedUniform = FindByExactName(uniformPlayerVariants, selectedCharacterName);
        }

        if (resolvedUniform == null && !string.IsNullOrWhiteSpace(selectedCharacterKey))
        {
            resolvedUniform = FindByKey(uniformPlayerVariants, selectedCharacterKey);
        }

        if (resolvedUniform == null && uniformPlayerVariants.Count > 0)
        {
            int uniformIndex = Mod(selectedCharacterIndex, uniformPlayerVariants.Count);
            resolvedUniform = uniformPlayerVariants[uniformIndex];
        }

        string resolvedKey = !string.IsNullOrWhiteSpace(selectedCharacterKey)
            ? selectedCharacterKey
            : BuildCharacterKey(SafeName(resolvedUniform));

        if (!string.IsNullOrWhiteSpace(resolvedKey))
        {
            resolvedLab = FindByKey(labPlayerVariants, resolvedKey);
        }

        if (resolvedLab == null && labPlayerVariants.Count > 0)
        {
            int labIndex = Mod(selectedCharacterIndex, labPlayerVariants.Count);
            resolvedLab = labPlayerVariants[labIndex];
        }

        return resolvedUniform != null && resolvedLab != null;
    }

    private static GameObject FindByExactName(List<GameObject> list, string targetName)
    {
        if (list == null || string.IsNullOrWhiteSpace(targetName)) return null;

        for (int i = 0; i < list.Count; i++)
        {
            GameObject candidate = list[i];
            if (candidate == null) continue;
            if (string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static GameObject FindByKey(List<GameObject> list, string key)
    {
        if (list == null || string.IsNullOrWhiteSpace(key)) return null;

        for (int i = 0; i < list.Count; i++)
        {
            GameObject candidate = list[i];
            if (candidate == null) continue;
            if (BuildCharacterKey(candidate.name) == key)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string BuildCharacterKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        string key = name.Trim().ToLowerInvariant();
        key = key.Replace("labcoat", string.Empty);
        key = key.Replace("_lab", string.Empty);
        key = key.Replace("-lab", string.Empty);
        key = key.Replace(" lab", string.Empty);
        key = key.Replace("uniform", string.Empty);
        key = key.Replace("variant", string.Empty);
        key = key.Replace("_", string.Empty);
        key = key.Replace("-", string.Empty);
        key = key.Replace(" ", string.Empty);
        return key;
    }

    private static bool IsLikelyLabVariant(GameObject go)
    {
        if (go == null) return false;

        string name = go.name ?? string.Empty;
        if (ContainsAnyKeyword(name, "lab variant", "- lab", "_lab", "labcoat")) return true;
        return false;
    }

    private static bool IsLikelyUniformVariant(GameObject go)
    {
        if (go == null) return false;

        string name = go.name ?? string.Empty;
        if (ContainsAnyKeyword(name, "lab variant", "- lab", "_lab", "labcoat")) return false;
        if (ContainsAnyKeyword(name, "uniform", "variant", "player")) return true;
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

        // 6. Transfer any necessary state (optional)
        TransferPlayerState(player1, player1Lab);

        // 7. Handle PlayerInput - CRITICAL for movement to work!
        TransferPlayerInput(player1, player1Lab);

        // 8. Bind camera + mobile UI ke player baru
        BindSceneSystemsToActivePlayer(player1Lab);

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
        SyncPlayerIdentity(oldPlayer, newPlayer);

        Debug.Log("ChangingRoom: Player state transferred.");
    }

    private static void SyncPlayerIdentity(GameObject oldPlayer, GameObject newPlayer)
    {
        if (oldPlayer == null || newPlayer == null) return;

        PlayerIdentity sourceIdentity = oldPlayer.GetComponent<PlayerIdentity>();
        if (sourceIdentity == null)
        {
            sourceIdentity = oldPlayer.GetComponentInChildren<PlayerIdentity>(true);
        }

        if (sourceIdentity == null) return;

        PlayerIdentity targetIdentity = newPlayer.GetComponent<PlayerIdentity>();
        if (targetIdentity == null)
        {
            targetIdentity = newPlayer.GetComponentInChildren<PlayerIdentity>(true);
        }

        if (targetIdentity == null)
        {
            targetIdentity = newPlayer.AddComponent<PlayerIdentity>();
        }

        targetIdentity.gender = sourceIdentity.gender;
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

        if (oldInput != null)
        {
            oldInput.DeactivateInput();
            oldInput.enabled = false;
        }

        if (newInput != null)
        {
            newInput.enabled = true;
            newInput.ActivateInput();

            if (oldInput != null && oldInput.currentActionMap != null)
            {
                newInput.SwitchCurrentActionMap(oldInput.currentActionMap.name);
            }

            Debug.Log($"ChangingRoom: PlayerInput transferred. Control Scheme now: {newInput.currentControlScheme}");
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

    private void BindSceneSystemsToActivePlayer(GameObject activePlayer)
    {
        if (activePlayer == null) return;

        UpdateCameraFollowTarget(activePlayer);
        RebindMobileInputs(activePlayer);
        SetPlayerInputEnabled(activePlayer, true);
    }

    private void UpdateCameraFollowTarget(GameObject activePlayer)
    {
        if (activePlayer == null) return;

        if (virtualCamera == null)
        {
            virtualCamera = FindFirstObjectByType<CinemachineVirtualCamera>(FindObjectsInactive.Include);
        }

        if (virtualCamera == null) return;

        Transform followTarget = GetCameraTarget(activePlayer);
        virtualCamera.Follow = followTarget;
        Debug.Log($"ChangingRoom: Camera follow set to {followTarget.name} ({activePlayer.name})");
    }

    private Transform GetCameraTarget(GameObject player)
    {
        if (player == null) return transform;

        var controller = player.GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null && controller.CinemachineCameraTarget != null)
        {
            return controller.CinemachineCameraTarget.transform;
        }

        Transform root = player.transform.Find("PlayerCameraRoot");
        if (root != null) return root;

        return player.transform;
    }

    private void RebindMobileInputs(GameObject activePlayer)
    {
        var selectedInputs = activePlayer.GetComponent<StarterAssets.StarterAssetsInputs>();
        if (selectedInputs == null)
        {
            Debug.LogWarning($"ChangingRoom: StarterAssetsInputs tidak ditemukan di {activePlayer.name}");
            return;
        }

        selectedInputs.enabled = true;

        StarterAssets.UICanvasControllerInput[] canvasInputs =
            FindObjectsByType<StarterAssets.UICanvasControllerInput>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < canvasInputs.Length; i++)
        {
            if (canvasInputs[i] == null) continue;
            canvasInputs[i].starterAssetsInputs = selectedInputs;
        }
    }

    private static void SetPlayerInputEnabled(GameObject player, bool enabled)
    {
        if (player == null) return;

        PlayerInput input = player.GetComponent<PlayerInput>();
        if (input == null) return;

        if (enabled)
        {
            input.enabled = true;
            input.ActivateInput();
        }
        else
        {
            input.DeactivateInput();
            input.enabled = false;
        }
    }
}
