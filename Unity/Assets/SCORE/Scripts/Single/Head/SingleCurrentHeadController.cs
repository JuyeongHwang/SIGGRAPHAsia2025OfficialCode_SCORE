using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SingleCurrentHeadController : MonoBehaviour
{
    public Transform currentHead;
    public Transform currentBody;
    public SingleCurrentCameraSensor currentSensor;
    public void HandleFutureAction(string actionText)
    {
        if (actionText.StartsWith("Looking at"))
        {
            string objectName = actionText.Substring("Looking at ".Length).Trim();
            Transform targetObject = currentSensor.FindImportantObjectByName(objectName);
            Transform targetAgent = currentSensor.FindImportantAgentByName(objectName, false);

            if (targetObject != null)
            {
                currentHead.GetComponent<SmoothRotate>().RotateToLookAt(targetObject, 60f);
            }
            else if (targetAgent != null)
            {
                currentHead.GetComponent<SmoothRotate>().RotateToLookAt(targetAgent, 60f);
                print("Pos: " + targetAgent.position);
                print("Target Pose: " + targetAgent.localPosition);
                print("obj name: " + targetAgent.name);
                print("obj parent name: " + targetAgent.parent.name);
            }
            else
            {
                currentHead.GetComponent<SmoothRotate>().RotateToAction("forward", 60f, currentBody);
            }
        }
        else if (actionText.StartsWith("Search "))
        {
            string direction = actionText.Substring("Search ".Length).Trim().ToLower();

            currentHead.GetComponent<SmoothRotate>().RotateToAction(direction, 60f, currentBody);
        }
        else
        {
            currentHead.GetComponent<SmoothRotate>().RotateToAction("forward", 60f, currentBody);
        }
    }
}
