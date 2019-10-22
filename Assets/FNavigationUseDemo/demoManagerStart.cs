using FNavigation;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class demoManagerStart : MonoBehaviour
{
    public ScriptableObject mesh;
    UnityAgentController theController;
    private void Awake()
    {
        theController = new UnityAgentController();
        theController.theAgentMode = NavAgentMode.SimpleMove;
        theController.MakeStart(mesh , null);
    }

    private void Update()
    {
        theController.Update();
        //print(theController.isSetup);
    }

    private void OnDestroy()
    {
        theController.MakeEnd();
    }

}
