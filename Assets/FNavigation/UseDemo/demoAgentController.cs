using FNavigation;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class demoAgentController : MonoBehaviour
{
    private NavManager mManager;

    void Awake()
    {
        UnityNavManagerMaker provider = this.GetComponent<UnityNavManagerMaker>();
        NavManager.ActiveManager = provider.CreateManager();  // Provides error reporting.
        mManager = NavManager.ActiveManager;
        Destroy(provider);

        mManager.StartThreadUpdate();
    }

    void Update()
    {
        mManager.MakeUpdate();
    }

    private void OnDestroy()
    {
        mManager.StopThreadUpdate();
    }
}
