using org.critterai;
using org.critterai.nav;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//正式使用的寻路名字空间为FNavigation
namespace FNavigation
{
    //这个move是一个较为底层的move,收到planner的控制来操作critterAI的底层计算
    //希望可以做到多个agent相互作用的agent移动计划
    //更底层使用 crowd manager进行移动
    //crowd寻路需要顾虑的东西很多，适相对比较消耗性能的移动计划
    //传说有20个agent的限制，有待考证
    public class CrowdMovePlan : NavState
    {
        private const byte StateNormal = 0;
        private const byte StateInRange = 1;
        private const byte StateIdle = 2;
        //被控制的NavAgent
        public readonly NavAgent theAgent;
        //当前的目标
        private NavmeshPoint mTarget;
        //因为是crowd的目标，似乎不需要太高级的移动计划
        private bool mGoalInRange = false;
        //0 没有目标   1 需要移动到目标   2 正在目标
        private byte mAtGoalState = StateNormal;

        public CrowdMovePlan(NavAgent theAgentIn)
        {
            theAgent = theAgentIn;
        }

        //进入到状态
        public override bool Enter()
        {
            if (theAgent == null || theAgent.navGroup.query == null || theAgent.navGroup.crowd == null)
                return false;

            //当前的位置映射到navmesh的位置
            NavmeshPoint pos = theAgent.GetPointSearch(theAgent.position);
            if (pos.polyRef == 0)
            {
                Debug.LogError(string.Format("{0}: Could not constrain position to navigation mesh. {1}"
                    , theAgent.transform.name, pos.ToString()));

                return false;
            }
            //目标映射到navmesh的位置
            NavmeshPoint goal = theAgent.GetPointSearch(theAgent.goal);
            if (goal.polyRef == 0)
            {
                Debug.LogError(string.Format("{0}: Could not constrain goal to navigation mesh. {1}"
                    , theAgent.transform.name, goal.ToString()));

                return false;
            }

            //NavAgent开始进入到crowd
            if (theAgent.AddToCrowd(pos.point) == null)
            {
                Debug.LogError(string.Format("{0}: Could not add agent to the crowd.", theAgent.transform.name));
                return false;
            }

            theAgent.desiredPosition = pos;
            theAgent.plannerGoal = goal;

            theAgent.flags &= ~(NavFlag.HasNewPosition | NavFlag.HasNewGoal);

            theAgent.SetCorridorAssets(false);
            theAgent.SetPathAssets(true);

            return HandlePositionFeedback();
        }

        public override void Exit()
        {
            theAgent.flags &= ~NavFlag.PathInUse;

            if (mAtGoalState != StateNormal)
                // Need to alert the next planner that it needs
                // to repost the crowd configuration.
                theAgent.flags |= NavFlag.CrowdConfigUpdated;
        }

        public override bool Update()
        {
            if (mAtGoalState == StateNormal)
                theAgent.SyncCrowdToDesired();

            //这一块的套路和Simple的套路是差不多的
            bool newPos = (theAgent.flags & NavFlag.HasNewPosition) != 0;
            bool newGoal = (theAgent.flags & NavFlag.HasNewGoal) != 0;

            NavmeshPoint pt;
            if (newPos)
            {
                //和critterai的dll交互，然后调到recast的dll
                //获得当前的navmesh上面的点位置
                pt = theAgent.GetPointSearch(theAgent.position);
                if (pt.polyRef == 0)
                {
                    Debug.LogWarning(string.Format(
                       "{0}: Could not constrain new position to the navigation mesh. Ignoring: {1}"
                        , theAgent.transform.name, pt.ToString()));
                    newPos = false;
                }
                else
                    theAgent.desiredPosition = pt;
            }

            if (newGoal)
            {
                pt = theAgent.GetPointSearch(theAgent.goal);
                if (pt.polyRef == 0)
                {
                    Debug.LogWarning(string.Format(
                        "{0}: Could not constrain new goal to the navigation mesh. Ignoring: {1}"
                        , theAgent.transform.name, pt.ToString()));

                    newGoal = false;
                }
                else
                {
                    theAgent.plannerGoal = pt;
                }
            }

            if (mAtGoalState != StateNormal && newGoal || newPos)
                TransitionToNormalGoalState();

            theAgent.flags &= ~(NavFlag.HasNewPosition | NavFlag.HasNewGoal);

            //如果有新坐标，就走这个feedback，这里与SimpleMove不同
            if (newPos)
                return HandlePositionFeedback();

            if (!HandleNormalPlanning(newGoal))
                return false;

            if ((theAgent.flags & NavFlag.CrowdConfigUpdated) != 0)
            {
                CrowdAgentParams config = theAgent.crowdConfig;

                if (mAtGoalState != StateNormal)
                    config.maxSpeed = 0;

                theAgent.crowdAgent.SetConfig(config);
                theAgent.flags &= ~NavFlag.CrowdConfigUpdated;
            }

            if (mAtGoalState == StateInRange)
            {
                //手工移动agent到目标
                if (theAgent.IsAtDestination())
                {
                    //到达目标之后，将自身转化为idle
                    mAtGoalState = StateIdle;
                    theAgent.desiredPosition = theAgent.plannerGoal;
                    theAgent.desiredSpeedSq = 0;
                    theAgent.desiredVelocity = Vector3.zero;
                }
                else
                {
                    Vector3 pos = theAgent.desiredPosition.point;
                    Vector3 goal = theAgent.plannerGoal.point;

                    //手动移动agent到目标
                    float desiredSpeed = Mathf.Sqrt(theAgent.desiredSpeedSq);

                    theAgent.desiredPosition.point = Vector3.MoveTowards(pos, goal, desiredSpeed * NavManager.threadUpdateTimer);
                    theAgent.desiredPosition.polyRef = 0;

                    theAgent.desiredVelocity = (theAgent.desiredPosition.point - pos).normalized * desiredSpeed;
                }
            }

            return true;
        }


        //管理目标位置，有比较的话需要对这个目标进行变更
        private bool HandleNormalPlanning(bool hasNewGoal)
        {
            if (hasNewGoal || !mGoalInRange && theAgent.IsNear(theAgent.desiredPosition.point, mTarget.point))
            {
                //在不变更移动计划的时候，看看能不能走
                if (!SetLocalTarget())
                {
                    if (theAgent.PlanPath(theAgent.desiredPosition, theAgent.plannerGoal) <= 0)
                    {
                        Debug.LogError(string.Format("{0}: Path replan failed. Position: {1}, Goal: {2}"
                            , theAgent.transform.name , theAgent.desiredPosition.ToString() , theAgent.plannerGoal.ToString()));
                        return false;
                    }

                    return SetLocalTarget();
                }
            }
            else if (mGoalInRange)
            {
                //这个方法根据CritterAI原有的架构来做的，由于这个部分已经被魔改，所以此处可能需要很慎重处理
                if (mAtGoalState == StateNormal)
                {
                    if (Vector3Util.IsInRange(theAgent.desiredPosition.point
                        , theAgent.plannerGoal.point, 0.12f, theAgent.heightTolerence))
                    {

                        mAtGoalState = StateInRange;
                        theAgent.RemoveFromCrowd();

                        CrowdAgentParams config = theAgent.crowdConfig;
                        config.maxSpeed = 0;

                        theAgent.crowdAgent = theAgent.navGroup.crowd.AddAgent(theAgent.plannerGoal.point, config);

                        theAgent.flags &= ~NavFlag.CrowdConfigUpdated;
                    }
                }
            }
            return true;
        }

        //这是一个替换移动计划的方法
        private bool HandlePositionFeedback()
        {
            //原方法作者注释：只应在所需位置和计划器目标调用一次此方法

            //没找到路径就直接返回否，报错
            if (theAgent.PlanPath(theAgent.desiredPosition, theAgent.plannerGoal) <= 0)
            {
                // A critical failure.
                Debug.LogError(string.Format(
                    "{0}: Path planning failed on position feedback. Position: {1}, Goal: {2}"
                    , theAgent.transform.name, theAgent.desiredPosition.ToString(), theAgent.plannerGoal.ToString()));

                return false;
            }

            theAgent.RemoveFromCrowd();
            theAgent.AddToCrowd(theAgent.desiredPosition.point);

            //根据crowd的情况重新规划目标
            mTarget = theAgent.desiredPosition;

            return SetLocalTarget();
        }

        //使用长距离路径确定最佳目标，并在必要时发送它给crowd manager
        private bool SetLocalTarget()
        {
            mGoalInRange = false;

            NavmeshPoint target;

            int targetIndex = theAgent.path.GetLocalTarget( theAgent.desiredPosition, theAgent.plannerGoal, 
                32 , theAgent.navGroup.query , out target);

            if (targetIndex == -1)
            {
                //crowd manager在当前路径上面没找到自身位置或者目标位置
                //原作者说这里没有必要报错，先看看
                return false;
            }
            else if (target.point != mTarget.point)
            {
                // 一个新的目标
                if (mAtGoalState != StateNormal)
                {
                    theAgent.flags |= NavFlag.CrowdConfigUpdated;

                    if (mAtGoalState != StateNormal)
                        TransitionToNormalGoalState();
                }

                theAgent.crowdAgent.RequestMoveTarget(target);
            }

            mTarget = target;
            mGoalInRange = (mTarget.point == theAgent.plannerGoal.point);

            return true;
        }

        private void TransitionToNormalGoalState()
        {
            mAtGoalState = StateNormal;
            //如果目标点无效，就需要重新找一个目标点
            if (theAgent.desiredPosition.polyRef == 0)
                theAgent.desiredPosition = theAgent.GetPointSearch(theAgent.desiredPosition);

            theAgent.flags |= NavFlag.CrowdConfigUpdated;
        }

    }
}
