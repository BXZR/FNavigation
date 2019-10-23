using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace FNavigation
{
    //这个是用于unity表现的上层移动，更新的是NavAgent所记录的Transform
    public class UnitySimpleMovePlan : UnityNavState
    {

        public override bool Enter()
        {
            return (theNavAgent != null);
        }

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

                theNavAgent.position = theNavAgent.desiredPosition;
                Quaternion rotation = theNavAgent.rotation;

                if ((theNavAgent.flags & NavFlag.GoalRotationEnabled) != 0 && theNavAgent.IsNear(theNavAgent.position.point, theNavAgent.goal.point))
                {
                    //接近的时候使用目标旋转角
                    rotation = theNavAgent.goalRotation;
                }
                else if (theNavAgent.desiredSpeedSq > theNavAgent.agentGroup.turnThreshold)
                {
                    //不接近的时候仍然保持自身的旋转
                    rotation = Quaternion.LookRotation(new Vector3(theNavAgent.desiredVelocity.x, 0, theNavAgent.desiredVelocity.z));
                } 
                trans.position = yAxisFreeze ? new Vector3(theNavAgent.position.point.x, trans.position.y, theNavAgent.position.point.z) : theNavAgent.position.point;
                trans.rotation = Quaternion.Slerp(trans.rotation, rotation, Time.deltaTime * theNavAgent.agentGroup.maxTurnSpeed);
                //记录旋转角
                theNavAgent.rotation = trans.rotation;
            }
            else
            {
                theNavAgent.rotation = trans.rotation;
                theNavAgent.position = theNavAgent.GetPointSearch(trans.position);
                theNavAgent.desiredPosition = theNavAgent.position;
            }

            return true;
        }

    }
}
