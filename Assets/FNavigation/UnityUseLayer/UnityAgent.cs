using org.critterai.nav;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FNavigation
{
    //Unity上层的agent的控制
    public class UnityAgent : MonoBehaviour
    {
        private NavManager mNavManager;
        private NavAgent mNavAgent;
        public byte agentGroup = 0;
        public float moveSpeed = 4f;
        private int mManagerIndex = -1;


        private void Start()
        {
            MakeInitialize();
        }

        //外部的设定目标的方法
        public void SetDestination(Vector3 aimPoint)
        {
            NavmeshPoint pt = GetNavPoint(aimPoint);
            if (pt.polyRef == 0)
                return;

            MoveTo(pt, true);
        }

        //外部设定，停止的方法
        public void Stop()
        {
            SetDestination(this.transform.position);
        }


        //根据目标点来转换目标，vector3 -> NavmeshPoint
        private NavmeshPoint GetNavPoint(Vector3 point)
        {
            return mNavAgent.GetPointSearch(point);
        }

        //给出目标进行移动的方法
        private void MoveTo(NavmeshPoint goal, bool ignoreGoalRotation)
        {
            goal = mNavAgent.GetPointSearch(goal);

            if (goal.polyRef == 0)
                return;

            mNavAgent.flags &= ~NavFlag.GoalIsDynamic;
            mNavAgent.targetMode = mNavAgent.ChangeMode();
            mNavAgent.goal = goal;

            if (ignoreGoalRotation)
                mNavAgent.flags =(mNavAgent.flags | NavFlag.HasNewGoal) & ~NavFlag.GoalRotationEnabled;
            else
                mNavAgent.flags |= (NavFlag.HasNewGoal | NavFlag.GoalRotationEnabled);
        }


        //在Start里面进行的初始化
        private void MakeInitialize()
        {
            //NavManager.ActiveManager的获取还需要一个更好的设计
            if (!enabled || NavManager.ActiveManager == null)
            {
                Debug.LogError(this.name + ": no activeManager or is not enabled");
                return;
            }
            mNavManager = NavManager.ActiveManager;
            //建立navAgent,这是真正用于移动的agent
            mNavAgent = mNavManager.CreateAgent(agentGroup, transform);
            mNavAgent.moveSpeed = this.moveSpeed;
            if (mNavAgent == null)
            {
                Debug.LogError(this.name + ": agent create failed");
                enabled = false;
                return;
            }
            mNavAgent.rotation = transform.rotation;
            mNavAgent.position = mNavAgent.GetPointSearch(transform.position);
            if (mNavAgent.position.polyRef == 0)
            {
                mNavAgent = null;
                Debug.LogError(this.name + ": take agent into navmesh failed");
                enabled = false;
                return;
            }

            //planner是底层计算推进（可以考虑多线程）
            //mover是unity位置改变，只能在主线程
            NavState planner = new NavPlanner(mNavAgent);
            //用于表现，也就是修改Unity中的移动的move
            NavState mover = new UnitySimpleMovePlan(mNavAgent);
            mManagerIndex = mNavManager.AddAgent(planner, mover);
            if (mManagerIndex == -1)
            {
                Debug.LogError(this.name + ": add agent to navigation manager failed");
                enabled = false;
                return;
            }
            Stop();
        }

        //额外辅助的一些方法
        public bool IsAtDestiontion()
        {
            return mNavAgent.IsAtDestination();
        }

    }
}
