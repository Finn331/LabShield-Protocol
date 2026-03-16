using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioSource))]
public class TeacherController : MonoBehaviour
{
    private Animator animator;
    private AudioSource audioSource;

    [Header("Animation Trigger Names")]
    [SerializeField] private string clappingTrigger = "clapping"; // Dimainkan ketika benar
    [SerializeField] private string wrongTrigger = "wrong";       // Dimainkan ketika salah
    [SerializeField] private string explainTrigger = "explain";   // Dimainkan saat evaluasi
    [SerializeField] private string idleTrigger = "idle";         // Dimainkan saat menunggu/baca soal

    private void Awake()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
    }

    private void SetAnimTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName)) return;

        // Reset semua trigger yang tersisa agar tidak memotong animasi baru
        if (!string.IsNullOrEmpty(idleTrigger)) animator.ResetTrigger(idleTrigger);
        if (!string.IsNullOrEmpty(clappingTrigger)) animator.ResetTrigger(clappingTrigger);
        if (!string.IsNullOrEmpty(wrongTrigger)) animator.ResetTrigger(wrongTrigger);
        if (!string.IsNullOrEmpty(explainTrigger)) animator.ResetTrigger(explainTrigger);

        animator.SetTrigger(triggerName);
    }

    public void PlayIdleAnimation()
    {
        SetAnimTrigger(idleTrigger);
    }

    public void PlayClappingAnimation()
    {
        SetAnimTrigger(clappingTrigger);
        Debug.Log("Guru: (Clapping - Benar)");
    }

    public void PlayWrongAnimation()
    {
        SetAnimTrigger(wrongTrigger);
        Debug.Log("Guru: (Wrong - Salah)");
    }

    public void PlayExplainAnimation()
    {
        SetAnimTrigger(explainTrigger);
        Debug.Log("Guru: (Explain)");
    }
    
    public void PlayVoice(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.Stop(); // Hentikan suara sebelumnya (jika ada)
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
    
    public void StopVoice()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
