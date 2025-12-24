using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Assertions.Must;

public class SaveManager : MonoBehaviour
{
    public static SaveManager instance { get; private set; }

    //What we want to save
    [Header("Level Unlocked")]
    public bool level2Unlocked;
    public bool level3Unlocked;

    [Header("Level Score")]
    public int level1Score;
    public int level2Score;
    public int level3Score;


    private void Awake()
    {
        if (instance != null && instance != this)
            Destroy(gameObject);
        else
            instance = this;

        DontDestroyOnLoad(gameObject);
        Load();
    }

    public void Load()
    {
        if (File.Exists(Application.persistentDataPath + "/playerInfo.dat"))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(Application.persistentDataPath + "/playerInfo.dat", FileMode.Open);
            PlayerData_Storage data = (PlayerData_Storage)bf.Deserialize(file);

            // Score Save
            level1Score = data.level1Score;
            level2Score = data.level2Score;
            level3Score = data.level3Score;

            // Boolean Save
            level2Unlocked = data.level2Unlocked;
            level3Unlocked = data.level3Unlocked;

            file.Close();
        }
    }

    public void Save()
    {
        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(Application.persistentDataPath + "/playerInfo.dat");
        PlayerData_Storage data = new PlayerData_Storage();


        // Score Save
        data.level1Score = level1Score;
        data.level2Score = level2Score;
        data.level3Score = level3Score;


        // Boolean Save
        data.level2Unlocked = level2Unlocked;
        data.level3Unlocked = level3Unlocked;

        bf.Serialize(file, data);
        file.Close();
    }
}

[Serializable]
class PlayerData_Storage
{
    // Score Save
    public int level1Score;
    public int level2Score;
    public int level3Score;


    // Boolean Save
    public bool level2Unlocked;
    public bool level3Unlocked;
}