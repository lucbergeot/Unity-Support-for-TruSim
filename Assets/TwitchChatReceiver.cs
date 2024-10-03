using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class TwitchChatReader : MonoBehaviour
{
    private string url = "http://localhost:10000/projects/NPC-memory-storage/applications/TOPIC/read_twitch_chat";
    private Coroutine fetchChatCoroutine;
    private SmallvilleIntegration smallvilleIntegration;

 void Start()
    {
        smallvilleIntegration = GetComponent<SmallvilleIntegration>();
        if (smallvilleIntegration == null)
        {
            Debug.LogError("SmallvilleIntegration component not found on the same GameObject");
        }
    }

public void StartFetchingChat()
    {
        if (fetchChatCoroutine == null)
        {
            fetchChatCoroutine = StartCoroutine(ReadTwitchChat());
            Debug.Log("Started fetching Twitch chat.");
        }
    }

    // Method to stop fetching Twitch chat
    public void StopFetchingChat()
    {
        if (fetchChatCoroutine != null)
        {
            StopCoroutine(fetchChatCoroutine);
            fetchChatCoroutine = null;
            Debug.Log("Stopped fetching Twitch chat.");
        }
    }

    IEnumerator ReadTwitchChat()
{
    while (true) 
    {
        Debug.Log("Fetching new Twitch chat message...");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error: " + request.error);
            }
            else
            {
                string responseText = request.downloadHandler.text;
                if (!string.IsNullOrEmpty(responseText))
                {
                    ProcessChatMessage(responseText);
                }
                else
                {
                    Debug.LogWarning("Received empty response from server");
                }
            }
        }
        yield return new WaitForSeconds(1f); 
    }
}

    // Process the Twitch chat message
    void ProcessChatMessage(string jsonResponse)
{
    Debug.Log($"Received JSON response: {jsonResponse}");
    
    ChatMessage chatMessage = JsonUtility.FromJson<ChatMessage>(jsonResponse);

    if (chatMessage != null)
    {
        Debug.Log($"Parsed ChatMessage - Type: {chatMessage.type}, Nickname: {chatMessage.nickname}, Comment: {chatMessage.comment}");
    }
    else
    {
        Debug.LogError("Failed to parse JSON response into ChatMessage object");
    }

    if (chatMessage != null && !string.IsNullOrEmpty(chatMessage.comment))
    {
        string message = chatMessage.nickname + ": " + chatMessage.comment;
        Debug.Log("Twitch chat message: " + message);
        
        if (smallvilleIntegration != null)
        {
            smallvilleIntegration.EnqueueTwitchChatMessage(message);
        }
        else
        {
            Debug.LogError("SmallvilleIntegration is null. Cannot enqueue Twitch chat message.");
        }
    }
    else
    {
        Debug.LogWarning($"No new chat message or invalid format. ChatMessage object: {(chatMessage == null ? "null" : "not null")}");
    }
}
    
    // Define the structure of the chat message from the API
    [System.Serializable]
    public class ChatMessage
    {
        public string type;
        public string nickname;
        public string comment;
    }
}

