using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Script ini dipasang pada objek UI (misalnya Image atau Icon Penanda) di dalam Canvas.
/// Berfungsi untuk mengikuti posisi target 3D di dunia permainan dan menampilkan icon di layar.
/// Jika target berada di belakang layar atau di luar kamera, icon akan menempel di pinggir layar.
/// </summary>
public class WaypointMarker : MonoBehaviour
{
    [Header("Referensi")]
    [Tooltip("Target 3D yang akan diikuti oleh Waypoint ini.")]
    public Transform target;
    [Tooltip("Kamera utama yang digunakan pemain.")]
    public Camera mainCamera;
    
    [Header("Pengaturan Tampilan")]
    [Tooltip("Offset (ketinggian) tambahan dari posisi asli target (misal: ditaruh di atas pintu).")]
    public Vector3 offset = new Vector3(0, 2f, 0);
    [Tooltip("Batas margin dari pinggir layar (agar ikon tidak terlalu mepet).")]
    public float edgePadding = 50f;
    [Tooltip("Apakah ikon ini harus berputar menunjuk ke arah target?")]
    public bool rotateToTarget = true;
    [Tooltip("Koreksi rotasi jika gambar panah bawaan Anda tidak menunjuk lurus ke kanan. (Misal: Panah hadap atas = -90, Panah hadap bawah = 90).")]
    public float angleOffset = -90f; 

    private RectTransform rectTransform;
    private Image markerImage;
    private Vector3 baseScale; // Untuk menyimpan ukuran asli ikon

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        markerImage = GetComponent<Image>();
        baseScale = rectTransform.localScale;
        
        // Posisikan Pivot dan Anchor tepat di tengah objek agar panahnya berputar pada sumbu poros yang pas
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        if (target == null)
        {
            // Sembunyikan jika tidak ada target
            if (markerImage != null) markerImage.enabled = false;
            return;
        }

        if (markerImage != null) markerImage.enabled = true;

        Vector3 targetPos = target.position + offset;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(targetPos);

        bool isBehindCamera = screenPos.z < 0;

        // Jika target ada di belakang kamera, balikkan posisinya
        if (isBehindCamera)
        {
            screenPos *= -1;
        }

        // Tentukan titik tengah layar
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        // Cari posisi relatif dari tengah
        screenPos.x -= screenCenter.x;
        screenPos.y -= screenCenter.y;

        // Batasi (clamp) posisi agar tidak keluar dari layar, dengan sedikit padding
        float maxAllowedX = screenCenter.x - edgePadding;
        float maxAllowedY = screenCenter.y - edgePadding;

        // Jika di luar layar atau di belakang kamera, kita clamp ke pinggir layar
        if (isBehindCamera || Mathf.Abs(screenPos.x) > maxAllowedX || Mathf.Abs(screenPos.y) > maxAllowedY)
        {
            // Ambil sudut tembakan dari tengah ke arah target
            float angleForClamp = Mathf.Atan2(screenPos.y, screenPos.x);

            // Tentukan posisi X dan Y berdasarkan batas layar dan rasio kemiringan
            float m = Mathf.Tan(angleForClamp); // gradient/slope
            
            // Cek apakah nabrak batas Kanan / Kiri dulu atau Atas / Bawah dulu
            if (Mathf.Abs(screenPos.x) / maxAllowedX > Mathf.Abs(screenPos.y) / maxAllowedY)
            {
                // Nabrak Kanan / Kiri
                screenPos.x = Mathf.Sign(screenPos.x) * maxAllowedX;
                screenPos.y = screenPos.x * m;
            }
            else
            {
                // Nabrak Atas / Bawah
                screenPos.y = Mathf.Sign(screenPos.y) * maxAllowedY;
                screenPos.x = screenPos.y / m;
            }
        }

        // Kembalikan ke koordinat layar normal (0,0 di pojok kiri bawah)
        screenPos.x += screenCenter.x;
        screenPos.y += screenCenter.y;

        // Terapkan posisi ke UI
        rectTransform.position = new Vector3(screenPos.x, screenPos.y, 0);

        // --- Logika Rotasi Panah ---
        if (rotateToTarget)
        {
            // Ambil vektor arah dari titik tengah layar ke posisi target di layar
            Vector3 direction = screenPos - new Vector3(screenCenter.x, screenCenter.y, 0);
            
            // Hitung sudut rotasinya dalam derajat
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Terapkan rotasi pada sumbu Z (untuk UI/2D) ditambah offset (agar ujung panahnya pas)
            rectTransform.rotation = Quaternion.Euler(0, 0, angle + angleOffset);
        }
    }

    /// <summary>
    /// Panggil fungsi ini untuk mengubah tujuan waypoint secara dinamis.
    /// Berikan parameter 'null' jika ingin menghilangkan waypoint dengan animasi.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        if (target == newTarget) return; // Mencegah animasi berulang jika targetnya sama

        target = newTarget;

        if (target != null)
        {
            // Pastikan image aktif sebelum dianimasikan
            if (markerImage != null) markerImage.enabled = true;

            // Animasi Pop-In (Membesar dari 0 ke ukuran asli)
            rectTransform.localScale = Vector3.zero;
            LeanTween.cancel(rectTransform.gameObject);
            LeanTween.scale(rectTransform, baseScale, 0.5f).setEaseOutBack();
        }
        else
        {
            // Animasi Pop-Out (Mengecil dari ukuran asli ke 0 lalu disembunyikan)
            LeanTween.cancel(rectTransform.gameObject);
            LeanTween.scale(rectTransform, Vector3.zero, 0.3f).setEaseInBack().setOnComplete(() =>
            {
                if (markerImage != null) markerImage.enabled = false;
            });
        }
    }
}
