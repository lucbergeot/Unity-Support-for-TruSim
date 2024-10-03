using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Convai.Scripts.Runtime.Core;
using System.Text.RegularExpressions;

public class SmallvilleIntegration : MonoBehaviour
{
    private const string SERVER_URL = "http://localhost:10000/projects/NPC-memory-storage/applications/TOPIC";

    [System.Serializable]
    public class GenerateTopicRequest
    {
        public string npc1_id;
    }

    [System.Serializable]
    public class ScriptResponse
    {
        public List<string> script;
    }

    private bool isFetchingScript = false;
    public ConvaiNPC convaiNPC;
    private Queue<string> commandQueue = new Queue<string>();
    private Queue<string> twitchChatQueue = new Queue<string>();
    private string npc1Id;
    private float scriptedCommandDelay = 5f; 
    private float twitchChatDelay = 1f; 

    private bool cooldownActive = false; 
    private float apiCooldown = 15f;

    private bool isAtTwitchPodium = false; 
    private float podiumWaitTime = 300f; 
    private TwitchChatReader twitchChatReader; 
    private Coroutine twitchProcessingCoroutine;

    void Start()
    {
        npc1Id = convaiNPC.characterID;
        StartCoroutine(ProcessCommands());

        twitchChatReader = GetComponent<TwitchChatReader>();
        if (twitchChatReader == null)
        {
            Debug.LogError("TwitchChatReader component is missing!");
        }
    }
public void EnqueueTwitchChatMessage(string message)
    {
        twitchChatQueue.Enqueue(message);
        Debug.Log("Enqueued Twitch chat message: " + message);
    }

    public IEnumerator GetScript(string npc1Id, System.Action<List<string>> onScriptReceived)
{
    string url = $"{SERVER_URL}/generate_script";
    GenerateTopicRequest requestData = new GenerateTopicRequest { npc1_id = npc1Id};
    string jsonData = JsonUtility.ToJson(requestData);

    using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
    {
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(jsonToSend);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        Debug.Log("Sending request to the server...");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"Server response: {request.downloadHandler.text}");
            ScriptResponse response = JsonUtility.FromJson<ScriptResponse>(request.downloadHandler.text);
            onScriptReceived?.Invoke(response.script);
        }
        else
        {
            Debug.LogError($"Error fetching script: {request.error}");
            onScriptReceived?.Invoke(null);
        }
        isFetchingScript = false;
    }
}

void OnScriptReceived(List<string> script)
{
    if (script != null)
    {
        foreach (string command in script)
        {
            commandQueue.Enqueue(command);
        }
        isFetchingScript = false; // Reset the flag
    }
    else
    {
        Debug.LogError("Failed to receive script.");
        isFetchingScript = false; // Reset the flag even on failure
    }
}


private IEnumerator ProcessCommands()
{
    while (true)
    {
            if (commandQueue.Count == 0)
            {
                if (!isFetchingScript && !cooldownActive && !isAtTwitchPodium)
                {
                    isFetchingScript = true;
                    StartCoroutine(GetScript(npc1Id, OnScriptReceived));

                    cooldownActive = true;
                    yield return new WaitForSeconds(apiCooldown);
                    cooldownActive = false;
                }
                yield return null;
        }
        else
        {
            string command = commandQueue.Dequeue();
            yield return StartCoroutine(WaitForNPCToFinish());
            yield return StartCoroutine(ProcessCommand(command));
            yield return new WaitForSeconds(scriptedCommandDelay);

        
            if (command.Contains("move to **Twitch Podium**"))
                {
                    isAtTwitchPodium = true;
                    Debug.Log("NPC has reached Twitch Podium. Starting Twitch chat fetching...");
                    StartTwitchChatFetching();
                    StartProcessingTwitchMessages();
                    yield return StartCoroutine(StartPodiumTimer());
            }
        }
    }
}
  private IEnumerator StartPodiumTimer()
    {
        Debug.Log("NPC has reached Twitch Podium. Starting the 5-minute timer...");
        yield return new WaitForSeconds(podiumWaitTime); // Wait for 5 minutes

        Debug.Log("Podium timer finished. Generating new script...");
        isAtTwitchPodium = false; 
        StopTwitchChatFetching();
        StartCoroutine(GetScript(npc1Id, OnScriptReceived));
    }

public void StartProcessingTwitchMessages()
{
    if (twitchProcessingCoroutine == null)
    {
        twitchProcessingCoroutine = StartCoroutine(TwitchMessageProcessing());
    }
}

private IEnumerator TwitchMessageProcessing()
{
    while (isAtTwitchPodium) 
    {
        if (twitchChatQueue.Count > 0)
        {
            string chatMessage = twitchChatQueue.Dequeue();
            Debug.Log("Processing Twitch chat message: " + chatMessage);

            convaiNPC.SendTextDataAsync(chatMessage);
            yield return StartCoroutine(WaitForNPCToFinish());
            yield return new WaitForSeconds(twitchChatDelay);
        }
        else
        {
            yield return null;  
        }
    }
    twitchProcessingCoroutine = null;
}
    private void StartTwitchChatFetching()
    {
        if (twitchChatReader != null && isAtTwitchPodium)
        {
            twitchChatReader.StartFetchingChat(); // Start reading Twitch chat when at the podium
        }
    }

    private void StopTwitchChatFetching()
    {
        if (twitchChatReader != null)
        {
            twitchChatReader.StopFetchingChat(); // Stop reading Twitch chat when leaving the podium
        }
    }

private IEnumerator WaitForNPCToFinish()
{
    // Wait until the NPC is no longer talking
    while (convaiNPC.IsCharacterTalking)
    {
        yield return null;
    }

    // If you have a way to check if the NPC is moving or performing an action, wait for that as well
    while (convaiNPC.IsPerformingAction()) 
    {
        yield return null;
    }
    Debug.Log("NPC has finished speaking and performing actions.");
}

private IEnumerator ProcessCommand(string command)
{
    command = command.Trim();

    if (command.StartsWith("$"))
    {
        command = command.Substring(1).Trim('\"');
        string parsedCommand = ParseCommand(command);
        Debug.Log($"Parsed Command: {parsedCommand}");
        yield return StartCoroutine(WaitForNPCToFinish());
        convaiNPC.SendTextDataAsync(parsedCommand);
        yield return StartCoroutine(WaitForNPCToFinish());
        Debug.Log($"Command sent to NPC: {parsedCommand}. NPC is talking: {convaiNPC.IsCharacterTalking}, NPC performing action: {convaiNPC.IsPerformingAction()}");
    }
    else
    {
    }

    yield return null;
}

private string ParseCommand(string command)
{
    // Remove any surrounding quotes
    command = command.Trim('\"');

    // Handle "move to" commands with optional actions and emotions
    if (command.StartsWith("move to"))
    {
        // Remove the initial "move to" part
        command = command.Substring("move to".Length).Trim();

        // Regex for "move to **location** and talk #topic (emotion)"
        Regex regexMoveWithActionAndEmotion = new Regex(@"\*\*(.*?)\*\*\s+and\s+talk\s+#(\w+)\s+\((.*?)\)");
        // Regex for "move to **location** (emotion)"
        Regex regexMoveWithEmotion = new Regex(@"\*\*(.*?)\*\*\s*\((.*?)\)");
        // Regex for "move to **location**" (without emotion)
        Regex regexMoveOnly = new Regex(@"\*\*(.*?)\*\*");

        Match matchMoveWithActionAndEmotion = regexMoveWithActionAndEmotion.Match(command);
        Match matchMoveWithEmotion = regexMoveWithEmotion.Match(command);
        Match matchMoveOnly = regexMoveOnly.Match(command);

        if (matchMoveWithActionAndEmotion.Success)
        {
            string location = matchMoveWithActionAndEmotion.Groups[1].Value;
            string topic = matchMoveWithActionAndEmotion.Groups[2].Value;
            string emotion = matchMoveWithActionAndEmotion.Groups[3].Value;

            // Return command formatted as "$Move to {location} #{topic} ({emotion})"
            return $"$Move to {location} #{topic} ({emotion})";
        }
        else if (matchMoveWithEmotion.Success)
        {
            string location = matchMoveWithEmotion.Groups[1].Value;
            string emotion = matchMoveWithEmotion.Groups[2].Value;

            // Return command formatted as "$move to {location} ({emotion})"
            return $"$move to {location} ({emotion})";
        }
        else if (matchMoveOnly.Success)
        {
            string location = matchMoveOnly.Groups[1].Value;

            // Return command formatted as "$move to {location}"
            return $"$move to {location}";
        }
        else
        {
            Debug.LogError($"Failed to parse move command: {command}");
            return command;
        }
    }
    else if (command.StartsWith("say"))
    {
        // Handle "say" commands, extracting the text inside braces { }
        int messageStart = command.IndexOf('{') + 1;
        int messageEnd = command.LastIndexOf('}');
        if (messageStart >= 1 && messageEnd > messageStart)
        {
            string message = command.Substring(messageStart, messageEnd - messageStart);

            // Return command formatted as "Say"{message}"
            return $"Say\"{message}\"";
        }
        else
        {
            Debug.LogError($"Failed to parse say command: {command}");
            return command;
        }
    }
    else if (command.StartsWith("$"))
    {
        // Handle any other actions that start with a $ (e.g., $dance)
        return command;
    }
    else
    {
        Debug.LogError($"Unknown command format: {command}");
        return command;
    }
}


}
