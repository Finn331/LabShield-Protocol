using UnityEngine;

[RequireComponent(typeof(Animator))]
public class TeacherController : MonoBehaviour
{
    private Animator animator;

    [Header("Animation Trigger Names")]
    [SerializeField] private string praiseTrigger = "Praise"; // Senyum & Angkat Jempol
    [SerializeField] private string warningTrigger = "Warning"; // Simbol Peringatan (Menggeleng/Tegas)
    [SerializeField] private string explainTrigger = "Explain"; // Sedang Menjelaskan Soal / Evaluasi

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void PlayPraiseAnimation()
    {
        if (animator != null && !string.IsNullOrEmpty(praiseTrigger))
            animator.SetTrigger(praiseTrigger);
        
        Debug.Log("Guru: (Tersenyum & Mengangkat Jempol)");
    }

    public void PlayWarningAnimation()
    {
        if (animator != null && !string.IsNullOrEmpty(warningTrigger))
            animator.SetTrigger(warningTrigger);
        
        Debug.Log("Guru: (Menampilkan Simbol Peringatan \u26A0\uFE0F)");
    }

    public void PlayExplainAnimation()
    {
        if (animator != null && !string.IsNullOrEmpty(explainTrigger))
            animator.SetTrigger(explainTrigger);
    }
}
