using UnityEngine;
using Convai.Scripts.Runtime.Core; // Ensure this namespace is properly imported for ConvaiHeadTracking

public class CameraSwitcher : MonoBehaviour
{
    public Camera mainCamera; // Main camera for streaming (only used if no other camera is in range)
    public Camera[] proximityCameras; // Array of proximity cameras around the scene
    public Transform npcTransform; // Reference to Convai NPC Dr. Quack Matterson
    public float switchDistance = 10f; // Distance threshold for switching cameras

    private Camera currentCamera;
    private ConvaiHeadTracking headTracking;

    void Start()
    {
        // Ensure all proximity cameras are deactivated at start
        foreach (Camera cam in proximityCameras)
        {
            cam.enabled = false;
        }

        // Start with the camera closest to the NPC
        Camera closestCamera = GetClosestCamera();
        SetActiveCamera(closestCamera);

        // Get the ConvaiHeadTracking component on the NPC
        headTracking = npcTransform.GetComponent<ConvaiHeadTracking>();

        if (headTracking == null)
        {
            Debug.LogError("ConvaiHeadTracking component not found on the NPC.");
        }
    }

    void Update()
    {
        float closestDistance = float.MaxValue;
        Camera closestCamera = null;

        // Check distance from NPC to each proximity camera
        foreach (Camera cam in proximityCameras)
        {
            float distance = Vector3.Distance(npcTransform.position, cam.transform.position);

            if (distance < switchDistance && distance < closestDistance)
            {
                closestDistance = distance;
                closestCamera = cam;
            }
        }

        // Switch to the closest camera if within range
        if (closestCamera != null && currentCamera != closestCamera)
        {
            SetActiveCamera(closestCamera);
        }

        if (currentCamera != null && currentCamera.name == "twitch camera" && headTracking != null)
        {
            headTracking.TargetObject = currentCamera.transform;
            headTracking.ForceImmediateLookAt(); // Ensure immediate look-at
        }

        // Rotate the current active camera to look at the NPC if it’s a proximity camera and not the Twitch camera
        if (currentCamera != null && currentCamera != mainCamera && currentCamera.name != "twitch camera")
        {
            RotateCameraTowardsNPC(currentCamera);
        }
    }

    void SetActiveCamera(Camera newCamera)
    {
        // Deactivate all cameras
        mainCamera.enabled = false;
        foreach (Camera cam in proximityCameras)
        {
            cam.enabled = false;
        }

        // Activate the new camera
        newCamera.enabled = true;
        currentCamera = newCamera;

        // If switching to Twitch camera, force NPC to look at it immediately
if (newCamera.name == "twitch camera" && headTracking != null)
{
    headTracking.TargetObject = newCamera.transform;
    headTracking.ForceImmediateLookAt(); // Ensure immediate look-at
}
    }
    

    void RotateCameraTowardsNPC(Camera camera)
    {
        if (npcTransform != null)
        {
            // Offset the target position to aim at the center of the NPC's body
            Vector3 targetPosition = npcTransform.position + new Vector3(0, 1.0f, 0); // Adjust the y-offset as needed
            camera.transform.LookAt(targetPosition);
        }
    }

    Camera GetClosestCamera()
    {
        float closestDistance = float.MaxValue;
        Camera closestCamera = null;

        // Find the camera closest to the NPC at the start
        foreach (Camera cam in proximityCameras)
        {
            float distance = Vector3.Distance(npcTransform.position, cam.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestCamera = cam;
            }
        }

        // If no proximity camera is available, default to main camera
        return closestCamera != null ? closestCamera : mainCamera;
    }

    public bool IsTwitchCameraActive()
    {
        return currentCamera != null && currentCamera.name == "twitch camera";
    }

}