using FNavigation;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum  mouseType  { left , right}
public class demoMoveController : UnityAgent
{
    public mouseType mouseIndex;

    private void Start()
    {
        MakeInitialize();
        this.SetWayPointMaterial(Resources.Load<Material>("NavigationBake/navigationEffects/wayPointMaterial"));
    }

    void Update()
    {
        if (Input.GetMouseButtonDown((int)mouseIndex) )
        {
            Move();
        }
        if (Input.GetKeyDown(KeyCode.Z))
        {
            SetCanControl(false);
        }
    }
    private void Move()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 1000))
        {
            SetDestination(hit.point);
        }
    }
}
