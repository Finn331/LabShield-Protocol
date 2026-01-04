using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ChangingRoom : Interactable
{
    [Header("Changing Room Settings")]
    public List<GameObject> prePPEObjects;  // Objects to hide (e.g., Casual Clothes)
    public List<GameObject> postPPEObjects; // Objects to show (e.g., Lab Coat model)
    public float changeDuration = 2.5f;

    private bool hasChanged = false;

    protected override void ExecuteInteraction()
    {
        Debug.Log("ChangingRoom: Interaction Started.");

        if (hasChanged)
        {
            Debug.Log("ChangingRoom: Already changed clothes.");
            HUDManager.Instance.ShowInteraction("Anda sudah siap masuk ke laboratorium.");
            return;
        }

        if (InventoryManager.Instance != null)
        {
            if (InventoryManager.Instance.HasCompletePPE())
            {
                Debug.Log("ChangingRoom: PPE Complete. Starting change sequence.");
                StartCoroutine(ChangeClothesRoutine());
            }
            else
            {
                Debug.Log("ChangingRoom: PPE Incomplete. Access Denied.");
                HUDManager.Instance.ShowInteraction("Dilarang Masuk! Lengkapi APD terlebih dahulu.");
            }
        }
        else
        {
            Debug.LogError("ChangingRoom: InventoryManager Instance is NULL!");
        }
    }

    private IEnumerator ChangeClothesRoutine()
    {
        // 1. Fade Out to Black
        if (ScreenFader.Instance) ScreenFader.Instance.FadeOut(0.5f);

        // 2. Wait while screen is black (simulating changing time)
        HUDManager.Instance.ShowInteraction("Mengganti Pakaian....");
        yield return new WaitForSeconds(0.5f); // Wait for fade to finish
        yield return new WaitForSeconds(changeDuration);

        // 3. Swap Models
        foreach (GameObject obj in prePPEObjects) if (obj) obj.SetActive(false);
        foreach (GameObject obj in postPPEObjects) if (obj) obj.SetActive(true);

        hasChanged = true;

        // 4. Fade In to Clear
        if (ScreenFader.Instance) ScreenFader.Instance.FadeIn(0.5f);

        yield return new WaitForSeconds(0.5f);
        HUDManager.Instance.ShowInteraction("APD Terpasang. Akses Lab Dibuka.");

        // Hide Objectives and Checklist as requested
        HUDManager.Instance.HideObjectivePanel();
        if (InventoryManager.Instance) InventoryManager.Instance.HideChecklist();
    }
}
