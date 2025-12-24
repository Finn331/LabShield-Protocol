using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

public class NetworkManager : MonoBehaviour
{
    private string serverUrl = "http://31.56.56.8:3000/api/submit-score"; 

    public void SubmitScore(int questionsAnswered, float score)
    {
        string studentName = AuthManager.IsLoggedIn ? AuthManager.CurrentUsername : "Guest";
        StartCoroutine(PostScore(new StudentData(studentName, questionsAnswered, score)));
    }

    // Helper untuk test manual jika ingin override nama
    public void SubmitScoreManual(string name, int count, float score)
    {
        StartCoroutine(PostScore(new StudentData(name, count, score)));
    }

    private IEnumerator PostScore(StudentData data)
    {
        string jsonData = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"Sending score to {serverUrl}: {jsonData}");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error submitting score: {request.error}");
            }
            else
            {
                Debug.Log($"Score submitted successfully! Response: {request.downloadHandler.text}");
            }
        }
    }
}
