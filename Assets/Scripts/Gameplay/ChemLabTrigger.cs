using UnityEngine;

/// <summary>
/// Script ini dipasang pada pintu/area masuk Laboratorium Kimia.
/// Berfungsi untuk menghilangkan Waypoint secara permanen saat pemain berhasil masuk lab, 
/// serta menyembunyikan objektif di UI.
/// </summary>
public class ChemLabTrigger : MonoBehaviour
{
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasTriggered)
        {
            hasTriggered = true;
            
            if (HUDManager.Instance != null)
            {
                // Arahkan Waypoint ke Kursi Kuis
                if (GameplayManager.Instance != null && GameplayManager.Instance.quizChairWaypoint != null)
                {
                    HUDManager.Instance.SetWaypointTarget(GameplayManager.Instance.quizChairWaypoint);
                }
                else 
                {
                    HUDManager.Instance.SetWaypointTarget(null);
                }
                
                HUDManager.Instance.UpdateObjective("Silakan duduk di bangku untuk memulai tes tertulis.");
            }
            
            Debug.Log("ChemLabTrigger: Player memasuki Laboratorium Kimia, Waypoint diarahkan ke bangku.");
        }
    }
}
