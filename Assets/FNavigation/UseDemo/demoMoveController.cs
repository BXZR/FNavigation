using FNavigation;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class demoMoveController : UnityAgent
{

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100))
            {
                SetDestination(hit.point);
            }
        }
    }
}
