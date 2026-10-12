using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SingleCurrentCameraSensor : MonoBehaviour
{
    public Camera currentAgentCam;

    public Transform importantObjects;
    public Transform importantAgents;

    public int imageWidth = 1024;
    public int imageHeight = 512;


    int currentLayerMask;
    private void Awake()
    {
        currentLayerMask = LayerMask.GetMask("Current");
    }

    private void Start()
    {
        currentAgentCam = this.GetComponent<Camera>();
        if (currentAgentCam != null)
        {
            print("Check the agentCam of the CameraVisionSensor.cs");
        }
        if (importantObjects != null)
        {
            print("Check the importantObjects of the CameraVisionSensor.cs");
        }
        if (importantAgents != null)
        {
            print("Check the importantAgents of the CameraVisionSensor.cs");
        }
    }

    public Texture2D CaptureRawTexture(int waypoint)
    {
        RenderTexture rt = new RenderTexture(imageWidth / 3, imageHeight / 3, 24);
        Camera cam = GetComponent<Camera>();
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        cam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        //if (saveImage)
        //{
        //    byte[] bytes = tex.EncodeToPNG();
        //    System.IO.File.WriteAllBytes($"Image_{index}_WP{waypoint}.png", bytes);
        //}

        return tex;
    }
    public string GetCauputreImageToBase64String(int count = -1, int waypointIndex = -1, bool saveImages = false)
    {
        Texture2D capturedImage = CaptureCameraImage();
        byte[] imageBytes = capturedImage.EncodeToPNG();
        string base64Image = System.Convert.ToBase64String(imageBytes);

        // 디버깅용 저장
        if (count >= 0 && waypointIndex >= 0 && saveImages)  // 값이 전달되었을 때만 저장
        {
            string folderPath = Path.Combine(Application.dataPath, "CurrentCapturedImages");
            Directory.CreateDirectory(folderPath); // 폴더 없으면 생성


            string fileName = $"이미지_{count}_{waypointIndex}.png";

            string fullPath = Path.Combine(folderPath, fileName);
            File.WriteAllBytes(fullPath, imageBytes);

        }

        return base64Image;
    }
    public string GetVisibleImportantObjects()
    {

        List<Transform> visibleObjects = new List<Transform>();

        foreach (Transform obj in importantObjects)
        {
            Vector3 viewportPoint = currentAgentCam.WorldToViewportPoint(obj.position);

            if (viewportPoint.z > 0 && viewportPoint.x > 0 && viewportPoint.x < 1 && viewportPoint.y > 0 && viewportPoint.y < 1)
            {
                Ray ray = new Ray(currentAgentCam.transform.position, obj.position - currentAgentCam.transform.position);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    if (hit.transform == obj || (obj.childCount > 0 && hit.transform == obj.GetChild(0)))
                    {
                        if (hit.distance < 15.0f)
                            visibleObjects.Add(obj);
                    }
                }
            }
        }


        string visibleObjectNames = string.Join(",", visibleObjects.ConvertAll(obj => obj.name));
        return visibleObjectNames;
    }


    public string GetVisibleImportantAgents()
    {
        List<Transform> visibleAgents = new List<Transform>();

        foreach (Transform obj in importantAgents)
        {
            if (obj == this.transform) continue;

            Transform target = null;
            Vector3 viewportPoint = Vector3.zero;
            Vector3 rayDir = Vector3.zero;

            if (obj.Find("ControlAvatar_Single_Current") == null)
            {
                Collider col = obj.GetComponent<Collider>();
                Vector3 colliderCenter = col.bounds.center;
                viewportPoint = currentAgentCam.WorldToViewportPoint(colliderCenter);
                //print("A: "+ obj.name);
                rayDir = colliderCenter - currentAgentCam.transform.position;
            }
            else
            {
                target = obj.Find("ControlAvatar_Single_Current");

                Collider targetCol = target.GetComponent<Collider>();
                Vector3 targetCenter = targetCol.bounds.center;
                viewportPoint = currentAgentCam.WorldToViewportPoint(targetCenter);
                //print("A: " + obj.name);
                // Ray 방향 계산
                rayDir = targetCenter - currentAgentCam.transform.position;
            }


            if (viewportPoint.z > 0 && viewportPoint.x > 0 && viewportPoint.x < 1 && viewportPoint.y > 0 && viewportPoint.y < 1)
            {
                // 시각 디버깅용
                if (target != null)
                    Debug.DrawRay(currentAgentCam.transform.position, rayDir, Color.blue, 1f);
                else
                    Debug.DrawRay(currentAgentCam.transform.position, rayDir, Color.red, 1f);
                Ray ray = new Ray(currentAgentCam.transform.position, rayDir);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, currentLayerMask, QueryTriggerInteraction.Collide))
                {
                    if (target != null)
                    {
                        if (hit.transform == target || hit.transform.IsChildOf(target))
                        {
                            if (hit.distance < 30.0f)
                                visibleAgents.Add(obj);
                        }
                    }
                    else
                    {
                        if (hit.transform == obj || hit.transform.IsChildOf(obj))
                        {
                            if (hit.distance < 30.0f)
                                visibleAgents.Add(obj);
                        }
                    }
                }
            }
        }
        Debug.Log($"[CHECK] visibleAgents.Count = {visibleAgents.Count}");

        string visibleAgentNames = string.Join(",", visibleAgents.ConvertAll(obj => obj.name));
        return visibleAgentNames;
    }



    public Transform FindImportantObjectByName(string objectName)
    {
        foreach (Transform obj in importantObjects)
        {
            if (obj.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return obj;
            }
        }

        return null;
    }

    public Transform FindImportantAgentByName(string objectName, bool isFuture)
    {
        foreach (Transform obj in importantAgents)
        {
            if (obj.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                if (obj.GetComponent<SingleFutureCurrentAgentData>() != null)
                {
                    if (isFuture)
                        return obj.Find("ControlHeadOffset_Single_Future");
                    else
                        return obj.Find("ControlHeadOffset_Single_Current");
                }
                else
                {
                    return obj;
                }
            }
        }

        return null;
    }

    // ====================

    private Texture2D CaptureCameraImage()
    {

        RenderTexture renderTexture = new RenderTexture(imageWidth, imageHeight, 24);
        currentAgentCam.targetTexture = renderTexture;
        Texture2D screenShot = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);

        currentAgentCam.Render();
        RenderTexture.active = renderTexture;
        screenShot.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        screenShot.Apply();

        currentAgentCam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);

        return screenShot;
    }
}
