using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FNavigation
{
    //移动计划的统一处理管理类
    //这个类统御各种移动计划的计算类，在外边是通过调用planner来调用底层的移动计划类
    //移动计划类再调用最底层的dll
    public class NavPlanner : NavState
    {
        //要传给移动计划类的NavAgent
        public readonly NavAgent theAgent;
        //空planner,这个是已经选定的当前移动计划
        private NavState mPlanner = NavState.Instance;
        //最基本的移动计划
        private SimpleMovePlan mSimpleMover;
        //带crowd的移动计划
        private CrowdMovePlan mCrowdMover;

        public NavPlanner(NavAgent theAgentIn)
        {
            theAgent = theAgentIn;
            mSimpleMover = new SimpleMovePlan(theAgentIn);
            mCrowdMover = new CrowdMovePlan(theAgentIn);
        }


        public override bool Enter()
        {
            if (theAgent == null)
            {
                Debug.LogError(theAgent.transform.name + ": Planner failed on enter: Navigation agent is null.");
                theAgent.flags |= NavFlag.PlannerFailed;
                return false;
            }

            Suspend();
            theAgent.desiredPosition = theAgent.position;
            theAgent.flags &= ~NavFlag.PlannerFailed;

            return true;
        }

        public override void Exit()
        {
            Suspend();
        }

        public override bool Update()
        {
            //根据不同的移动计划进行不同的移动
            HandleState();

            if ((theAgent.flags & NavFlag.PlannerFailed) != 0)
            {
                //Debug.LogError(theAgent.transform.name + ": Planner failed.");
                Suspend();
            }
            //在这里进行移动计划的移动，并根据返回值判断
            else if (!mPlanner.Update())
            {
                theAgent.flags |= NavFlag.PlannerFailed;
                //Debug.LogError(theAgent.transform.name + ": Planner update failed: " + mPlanner);
            }
            return true;
        }

        //状态处理：根据不同的移动模式选择不同的移动计划进行移动
        //目前只有SimpleMove
        private void HandleState()
        {
            NavAgentMode target = theAgent.targetMode;

            switch (target)
            {
                case NavAgentMode.SimpleMove:
                        if (mSimpleMover != mPlanner)
                            TransitionState(mSimpleMover);
                        break;
                case NavAgentMode.CrowdMove:
                    if (mCrowdMover != mPlanner)
                        TransitionState(mCrowdMover);
                    break;
            }
        }

        //修改当前选定的移动计划
        private void TransitionState(NavState state)
        {
            mPlanner.Exit();
            mPlanner = state;

            if (mPlanner.Enter())
                theAgent.flags &= ~NavFlag.PlannerFailed;
            else
            {
                theAgent.flags |= NavFlag.PlannerFailed;
                //Debug.LogError(theAgent.transform.name + ": Planner transition failed: " + state);
            }
        }

        //暂停,感觉其实就是刷新一下数据
        private void Suspend()
        {
            mPlanner.Exit();
            mPlanner = NavState.Instance;

            theAgent.RemoveFromCrowd();
            theAgent.SetCorridorAssets(false);
            theAgent.SetPathAssets(true);

            theAgent.desiredVelocity = Vector3.zero;
            theAgent.desiredSpeedSq = 0;

            theAgent.flags &= ~NavFlag.PlannerFailed;
        }
    }
}
