using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class AuthManager : MonoBehaviour
{
    private string baseUrl = "http://31.56.56.8:3000/api"; 
    private string registerUrl = "http://31.56.56.8:3000/register.html";

    public static AuthManager Instance { get; private set; }
    public static string CurrentUsername { get; private set; }
    public static bool IsLoggedIn { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [System.Serializable]
    public class AuthData
    {
        public string username;
        public string password;
    }

    [System.Serializable]
    public class AuthResponse
    {
        public bool success;
        public string message;
        public string role;
        public string error;
    }

    // New: Open Register Page in Browser
    public void OpenRegisterPage()
    {
        Application.OpenURL(registerUrl);
        Debug.Log("Opening Register Page: " + registerUrl);
    }

    public void Register(string username, string password, System.Action<string> callback)
    {
        StartCoroutine(AuthRequest("/register", username, password, callback));
    }

    public void Login(string username, string password, System.Action<string> callback)
    {
        StartCoroutine(AuthRequest("/login", username, password, (result) => {
            if (result == "Success")
            {
                CurrentUsername = username;
                IsLoggedIn = true;
                Debug.Log($"Logged in as: {CurrentUsername}");
            }
            callback(result);
        }));
    }

    private IEnumerator AuthRequest(string endpoint, string u, string p, System.Action<string> callback)
    {
        AuthData data = new AuthData { username = u, password = p };
        string json = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(baseUrl + endpoint, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMessage = "Connection Error";
                
                // 1. Try to parse JSON body
                if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    try {
                        AuthResponse errorRes = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                        if (!string.IsNullOrEmpty(errorRes.error)) errorMessage = errorRes.error;
                    } catch {
                        // Parsing failed, ignore
                    }
                }

                // 2. If message is still default or raw HTTP header, use Status Code
                if (errorMessage == "Connection Error" || errorMessage.Contains("HTTP"))
                {
                    if (request.responseCode == 404) errorMessage = "Invalid User";
                    else if (request.responseCode == 401) errorMessage = "Wrong Password";
                    else if (request.responseCode == 429) errorMessage = "Too Many Attempts";
                    else errorMessage = request.error; // Last resort
                }
                
                callback(errorMessage);
            }
            else
            {
                AuthResponse res = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                if (res.success)
                {
                    callback("Success");
                }
                else
                {
                    callback(res.error);
                }
            }
        }
    }
}
