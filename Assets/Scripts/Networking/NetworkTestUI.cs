using UnityEngine;

public class NetworkTestUI : MonoBehaviour
{
    public NetworkManager networkManager;
    public AuthManager authManager;

    private string username = "";
    private string password = "";
    private string statusMessage = "Ready";

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        
        GUILayout.Label("Authentication Test");
        username = GUILayout.TextField(username, 20);
        password = GUILayout.TextField(password, 20);

        if (GUILayout.Button("Register Student"))
        {
            authManager.Register(username, password, (msg) => statusMessage = msg);
        }

        if (GUILayout.Button("Login"))
        {
            authManager.Login(username, password, (msg) => statusMessage = msg);
        }

        GUILayout.Space(20);
        GUILayout.Label($"Status: {statusMessage}");
        GUILayout.Label($"Logged In: {AuthManager.IsLoggedIn} ({AuthManager.CurrentUsername})");

        GUILayout.Space(20);
        if (AuthManager.IsLoggedIn)
        {
            if (GUILayout.Button("Submit Score (10 Q, 90 Score)"))
            {
                networkManager.SubmitScore(10, 90f);
                statusMessage = "Submitting Score...";
            }
        }
        else
        {
            GUILayout.Label("Login to submit score.");
        }

        GUILayout.EndArea();
    }
}
