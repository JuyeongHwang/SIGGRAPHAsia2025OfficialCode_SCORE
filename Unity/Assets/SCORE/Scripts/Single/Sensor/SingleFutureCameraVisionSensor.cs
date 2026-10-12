using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SingleFutureCameraVisionSensor : MonoBehaviour
{
    public Camera futureAgentCam;

    public Transform importantObjects;
    public Transform importantAgents;

    public int imageWidth = 1024;
    public int imageHeight = 512;

    private void Start()
    {
        futureAgentCam = this.GetComponent<Camera>();
        if(futureAgentCam != null)
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
    public Texture2D CaptureRawTexture(int index, int waypoint, bool saveImage)
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

        if (saveImage)
        {
            byte[] bytes = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes($"Image_{index}_WP{waypoint}.png", bytes);
        }

        return tex;
    }

    public string GetVisibleImportantObjects()
    {

        List<Transform> visibleObjects = new List<Transform>();

        foreach (Transform obj in importantObjects)
        {
            Vector3 viewportPoint = futureAgentCam.WorldToViewportPoint(obj.position);

            if (viewportPoint.z > 0 && viewportPoint.x > 0 && viewportPoint.x < 1 && viewportPoint.y > 0 && viewportPoint.y < 1)
            {
                Ray ray = new Ray(futureAgentCam.transform.position, obj.position - futureAgentCam.transform.position);
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

            if (obj.Find("ControlAvatar_Single_Future") == null)
            {
                Collider col = obj.GetComponent<Collider>();
                Vector3 colliderCenter = col.bounds.center;
                viewportPoint = futureAgentCam.WorldToViewportPoint(colliderCenter);

                rayDir = colliderCenter - futureAgentCam.transform.position;
            }
            else
            {
                target = obj.Find("ControlAvatar_Single_Future");

                Collider targetCol = target.GetComponent<Collider>();
                Vector3 targetCenter = targetCol.bounds.center;
                viewportPoint = futureAgentCam.WorldToViewportPoint(targetCenter);

                // Ray 방향 계산
                rayDir = targetCenter - futureAgentCam.transform.position;
            }


            if (viewportPoint.z > 0 && viewportPoint.x > 0 && viewportPoint.x < 1 && viewportPoint.y > 0 && viewportPoint.y < 1)
            {
                // 시각 디버깅용
                if (target != null)
                    Debug.DrawRay(futureAgentCam.transform.position, rayDir, Color.blue, 1f);
                else
                    Debug.DrawRay(futureAgentCam.transform.position, rayDir, Color.red, 1f);
                Ray ray = new Ray(futureAgentCam.transform.position, rayDir);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide))
                {
                    if (target != null)
                    {
                        if (hit.transform == target)
                        {
                            if (hit.distance < 20.0f)
                                visibleAgents.Add(obj);
                        }
                    }
                    else
                    {
                        if (hit.transform == obj)
                        {
                            if (hit.distance < 20.0f)
                                visibleAgents.Add(obj);
                        }
                    }
                }
            }
        }

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
                if (obj.GetComponent<SingleFutureCurrentAgentData>())
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
}
