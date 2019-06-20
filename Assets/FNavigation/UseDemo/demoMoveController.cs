using FNavigation;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum  mouseType  { left , right}
public class demoMoveController : UnityAgent
{
    public mouseType mouseIndex;
    void Update()
    {
        if (Input.GetMouseButtonDown((int)mouseIndex) )
        {
            Move();
        }

    }
    private void Move()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100))
        {
            SetDestination(hit.point);
        }
    }
}
