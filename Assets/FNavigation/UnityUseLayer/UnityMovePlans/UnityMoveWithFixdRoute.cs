using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FNavigation
{
    //这个是用于unity表现的上层移动，更新的是NavAgent所记录的Transform
    public class UnityMoveWithFixdRoute : UnityNavState
    {

        public override Vector3[] GetRoutePoint()
        {
            Vector3  [] routePoints  = theNavAgent.GetRoutePoints();
            //对获得的路点信息做滤镜（后期处理）
            routePoints = RoutePointFixer.Instance.FixRouteWithPhysics(routePoints);

            List<Vector3> used = new List<Vector3>();
            for (int i = index; i < routePoints.Length; i++)
                used.Add(routePoints[i]);

            return used.ToArray();
        }

        public override bool Enter()
        {
            return (theNavAgent != null);
        }

        Vector3[] points = new Vector3[0];
        Vector3[] rePoints = null;
        int index = 0;

        public override bool Update()
        {
            Transform trans = theNavAgent.transform;
            if (isMoveControling)
            {
                //强制性的位移，这应该是一个比较不常用到的移动
                if ((theNavAgent.flags & NavFlag.ForceMove) != 0)
                {
                    theNavAgent.position = theNavAgent.goal;

                    if ((theNavAgent.flags & NavFlag.GoalRotationEnabled) != 0)
                        theNavAgent.rotation = theNavAgent.goalRotation;

                    //更新更细表现的位置和旋转
                    theNavAgent.transform.position = theNavAgent.position.point;
                    theNavAgent.transform.rotation = theNavAgent.rotation;

                    theNavAgent.flags = (theNavAgent.flags | NavFlag.HasNewPosition) & ~NavFlag.ForceMove;

                    Debug.Log(theNavAgent.transform.name + ": Force Move");
                    return true;
                }

                // Note: Keep this after the force move handling.
                if ((theNavAgent.flags & NavFlag.PlannerFailed) != 0)
                    // 如果底层没有移动的方案，就不移动
                    return true;


                /*
                theNavAgent.position = theNavAgent.desiredPosition;
                trans.position = yAxisFreeze ? new Vector3(theNavAgent.position.point.x, trans.position.y, theNavAgent.position.point.z) : theNavAgent.position.point;
                */

                Vector3[] pointsUse =  theNavAgent.GetRoutePoints();
                Debug.Log("pointsUseL ==== "+ pointsUse.Length);
                if (Enumerable.SequenceEqual(pointsUse, points) == false)
                {
                    theNavAgent.moveSpeed = 999;
                    theNavAgent.desiredSpeedSq = 999;

                    points = pointsUse;
                    rePoints = RoutePointFixer.Instance.FixRouteWithPhysics(points);
                    index = 0;
                    Debug.Log("hehehehehehehehe");
                }

                if (points == null || points.Length == 0)
                    return true;

                Debug.Log(" rePoints.Length ==== " + rePoints.Length + "   index = "+ index);
                if (index < rePoints.Length)
                {
                    theNavAgent.path.isDirty = true;
                    trans.transform.position = Vector3.MoveTowards(trans.transform.position, rePoints[index], 9f *Time.deltaTime);
                    float distane = Vector3.Distance(trans.transform.position, rePoints[index]);
                    Debug.Log("distance =     ======"+distane);
                    if (distane < 0.1f)
                    {
                        index++;

                    }
                }


            }


            return true;
        }


    }
}
