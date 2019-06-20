using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class UnityNav : MonoBehaviour
{
    NavMeshAgent s;
    void Start()
    {
        s = this.GetComponent<NavMeshAgent>();
    }


    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100))
            {
                s.SetDestination(hit.point);
            }
        }
    }
}
