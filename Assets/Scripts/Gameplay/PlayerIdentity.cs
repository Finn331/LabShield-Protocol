using UnityEngine;

public enum PlayerGender
{
    Male,
    Female,
    Unknown
}

/// <summary>
/// Pasang script ini di objek karakter Anda (misal: Player1, Player1-Lab, Player2-Cewe, dll)
/// Berguna untuk mengecek identitas pemain saat akan menggunakan fasilitas tertentu seperti Toilet.
/// </summary>
public class PlayerIdentity : MonoBehaviour
{
    [Header("Identitas Karakter")]
    [Tooltip("Pilih gender dari karakter ini.")]
    public PlayerGender gender = PlayerGender.Male;
}
