using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SingleFutureHeadController : MonoBehaviour
{
    public Transform futureHead;
    public Transform futureBody;
    public SingleFutureCameraVisionSensor futureSensor;
    public void HandleFutureAction(string actionText)
    {
        if (actionText.StartsWith("Looking at"))
        {
            string objectName = actionText.Substring("Looking at ".Length).Trim();
            Transform targetObject = futureSensor.FindImportantObjectByName(objectName);
            Transform targetAgent = futureSensor.FindImportantAgentByName(objectName, true);

            if (targetObject != null)
            {
                futureHead.GetComponent<SmoothRotate>().RotateToLookAt(targetObject, 36f);
            }
            else if (targetAgent != null)
            {
                futureHead.GetComponent<SmoothRotate>().RotateToLookAt(targetAgent, 36f);
            }
            else
            {
                futureHead.GetComponent<SmoothRotate>().RotateToAction("Search forward", 36f, futureBody);
            }
        }
        else if (actionText.StartsWith("Search "))
        {
            string direction = actionText.Substring("Search ".Length).Trim().ToLower();

            futureHead.GetComponent<SmoothRotate>().RotateToAction(direction, 36f, futureBody);
        }
        else
        {
            futureHead.GetComponent<SmoothRotate>().RotateToAction("forward", 36f, futureBody);
        }

        //this.transform.GetComponent<SigneFutureMovement>().previousImage = null;

    }
}
