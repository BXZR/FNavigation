using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FNavigation
{
    //unity层的navstate（上层刷新），另外也包含了一些额外的应用方法
    public class UnityNavState : NavState
    {
        public  NavAgent theNavAgent = null;
        public bool yAxisFreeze = false;

        public void SetNavAgent(NavAgent agent)
        {
            theNavAgent = agent;
        }

        public virtual Vector3[] GetRoutePoint()
        {
            return theNavAgent.GetRoutePoints();
        }
    }
}
