using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Pastikan script ini ditaruh di GameObject yang sama dengan Animator (Player)
public class PlayerCustomAnim : MonoBehaviour
{
    private Animator _animator;
    
    // Siapkan wadah (Hash ID) untuk parameter String agar lebih ringan
    private int _animIDStandToSit;
    private int _animIDSitToStand;

    // Status apakah player sedang duduk atau tidak
    public bool isSitting = false;

    void Start()
    {
        _animator = GetComponent<Animator>();

        // Sesuaikan dengan nama parameter Trigger persis di Animator Window-mu
        _animIDStandToSit = Animator.StringToHash("StandToSit"); 
        _animIDSitToStand = Animator.StringToHash("SitToStand");
    }

    void Update()
    {
        // Pemicu Keyboard kini dipindah ke ChairInteraction.cs
        // Agar player hanya bisa duduk JIKA DEKAT dengan kursi.
    }

    // Fungsi ini bisa dipanggil dari mana saja (Script lain, Tombol UI, atau Keyboard)
    public void ToggleSit()
    {
        if (_animator == null) return;

        if (!isSitting)
        {
            // Player sedang berdiri -> Suruh duduk
            _animator.SetTrigger(_animIDStandToSit);
            isSitting = true;
            Debug.Log("Player mulai duduk...");
            
            // Opsional: Matikan pergerakan ThirdPersonController agar tidak bisa jalan sambil duduk
            // GetComponent<StarterAssets.ThirdPersonController>().enabled = false;
        }
        else
        {
            // Player sedang duduk -> Suruh berdiri
            _animator.SetTrigger(_animIDSitToStand);
            isSitting = false;
            Debug.Log("Player berdiri...");
            
            // Opsional: Nyalakan kembali pergerakan
            // GetComponent<StarterAssets.ThirdPersonController>().enabled = true;
        }
    }
}
