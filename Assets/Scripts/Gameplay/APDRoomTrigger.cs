using UnityEngine;

/// <summary>
/// Script ini dipasang pada pintu/area masuk Ruang APD (Bukan Ruang Ganti).
/// Berfungsi untuk memunculkan daftar checklist APD secara otomatis saat pemain memasukinya.
/// </summary>
public class APDRoomTrigger : MonoBehaviour
{
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasTriggered)
        {
            hasTriggered = true;
            
            if (InventoryManager.Instance != null)
            {
                // Setelah masuk ruang APD, munculkan semua daftar APD yang harus dicari
                InventoryManager.Instance.ShowChecklist();
                
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.UpdateObjective("Temukan dan gunakan semua APD pada daftar!");
                    // Hilangkan arah panah Waypoint saat pemain sibuk mencari APD dengan animasi Pop-out
                    HUDManager.Instance.SetWaypointTarget(null); 
                }
                
                Debug.Log("APDRoomTrigger: Player memasuki ruang APD, checklist diaktifkan.");
            }
        }
    }
}
