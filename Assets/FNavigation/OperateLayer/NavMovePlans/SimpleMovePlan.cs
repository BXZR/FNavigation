using org.critterai;
using org.critterai.nav;
using UnityEngine;

//正式使用的寻路名字空间为FNavigation
namespace FNavigation
{
    //这个move是一个较为底层的move,收到planner的控制来操作critterAI的底层计算
    //基础移动计划，可以有更多的移动计划，例如Crowd
    public class SimpleMovePlan : NavState
    {
        //计算的时候一个中间量，和mOptimizationTimer有关
        //强制优化的间隔
        private static int OptimizationFrequency = 10;
        private int mOptimizationTimer;

        //计算得到的速度，有关速度的处理还需要更深入的学习，貌似这也是一个中间量
        private float mSpeed;

        //用于控制的navAgent，这就是为什么直接将这个对象发给manager就直接可以不管了
        //全是内部计算，控制NavAgent的引用
        public readonly NavAgent theAgent;

        //构造的时候必须要获得这个agent
        public SimpleMovePlan(NavAgent theAgentIn)
        {
            theAgent = theAgentIn;
        }

        //实际上这是一个不断开闭的过程
        //循环过程中也会不断Enter
        public override bool Enter()
        {
            if (theAgent.navGroup.query == null)
                return false;

            //在导航网格上找到自己的位置
            NavmeshPoint pos = theAgent.GetPointSearch(theAgent.position);
            if (pos.polyRef == 0)
            {
                Debug.LogError(string.Format("{0}: Could not constrain position to navigation mesh. {1}", theAgent.transform.name, pos.ToString()));
                return false;
            }

            //在导航网格上找到目标的位置
            NavmeshPoint goal = theAgent.GetPointSearch(theAgent.goal);
            if (goal.polyRef == 0)
            {
                Debug.LogError(string.Format("{0}: Could not constrain goal to navigation mesh. {1}", theAgent.transform.name, goal.ToString()));
                return false;
            }

            theAgent.RemoveFromCrowd();
            theAgent.SetCorridorAssets(true);
            theAgent.SetPathAssets(false);

            //agent自己寻路，发现没有路就报错
            if (theAgent.PlanCorridor(pos, goal) <= 0)
            {
                Debug.LogError(string.Format("{0}: Could not plan corridor on enter.", theAgent.transform.name));
                theAgent.flags &= ~NavFlag.CorridorInUse;
                return false;
            }

            //存储寻路之后的结果
            theAgent.desiredPosition = theAgent.corridor.Position;
            theAgent.plannerGoal = theAgent.corridor.Target;
            theAgent.flags &= ~(NavFlag.HasNewPosition | NavFlag.HasNewGoal);

            //设定速度和重置计数器
            mSpeed = Mathf.Sqrt(theAgent.desiredSpeedSq);
            mOptimizationTimer = OptimizationFrequency;

            return true;
        }

        //结束的时候调用一次
        public override void Exit()
        {
            //重置一下flag
            theAgent.flags &= ~NavFlag.CorridorInUse;
        }

        //只要没有结束就会一直更新
        //这些更新是在NavManager里的更新方法进行统一更新的
        public override bool Update()
        {
            //检测位置和目标变化，并约束到导航网格
            bool newPos = (theAgent.flags & NavFlag.HasNewPosition) != 0;
            bool newGoal = (theAgent.flags & NavFlag.HasNewGoal) != 0;

            NavmeshPoint pt;

            if (newPos)
            {
                //和critterai的dll交互，然后调到recast的dll
                //获得当前的navmesh上面的点位置
                pt = theAgent.GetPointSearch(theAgent.position);

                //如果点无效就报个错
                if (pt.polyRef == 0)
                {
                    Debug.LogWarning(string.Format("{0}: Could not constrain position to navigation mesh. Ignoring: {1}", 
                        theAgent.transform.name, pt.ToString()));
                    newPos = false;
                }
                else
                    theAgent.desiredPosition = pt;
            }

            if (newGoal)
            {
                //和critterai的dll交互，然后调到recast的dll
                //获得当前的navmesh上面的点位置
                pt = theAgent.GetPointSearch(theAgent.goal);

                if (pt.polyRef == 0)
                {
                    // Ignore new goal.
                    Debug.LogWarning(string.Format("{0}: Could not constrain goal to navigation mesh. Ignoring: {1}"
                        , theAgent.transform.name, pt.ToString()));

                    newGoal = false;
                }
                else
                    theAgent.plannerGoal = pt;
            }

            theAgent.flags &= ~(NavFlag.HasNewPosition | NavFlag.HasNewGoal);

            if (newGoal || newPos)
            {
                //重新制定移动计划
                if (!HandlePlanning(newPos, newGoal))
                    return false;
            }

            //是否需要进行移动的判定
            if (theAgent.IsAtDestination())
            {
                //在目标就不用移动了
                mSpeed = 0;
                theAgent.desiredPosition = theAgent.plannerGoal;
                theAgent.desiredVelocity = Vector3.zero;
                theAgent.desiredSpeedSq = 0;
                return true;
            }

            //在这里调整速度（这理由可以非常任性地修改的空间）
            float maxDelta = theAgent.crowdConfig.maxAcceleration * 0.02f;
            float desiredSpeed = theAgent.crowdConfig.maxSpeed;
            if (Vector3Util.GetDistance2D(theAgent.desiredPosition.point, theAgent.plannerGoal.point) < theAgent.crowdConfig.radius * 3)
            {
                //如果已经很贴近目标，就做了个减速，这个还是根据具体需求来搞吧
                //这个效果目前还不用,感觉略显魔性
                desiredSpeed = Mathf.Max(mSpeed - maxDelta, desiredSpeed * 0.2f);
            }
            else
            {
                //正常飚速度
                desiredSpeed = Mathf.Min(mSpeed + maxDelta, desiredSpeed);
            }

            //运行到这里，终于，开始真正正正的移动了
            //每个间隔几次，来一次优化
            if (--mOptimizationTimer < 1)
            {
                theAgent.corridor.OptimizePathTopology(true);
                mOptimizationTimer = OptimizationFrequency;
            }

            //desiredSpeed *= 10;//用于测试速度
            Vector3 movePos = Vector3.MoveTowards(theAgent.desiredPosition.point, theAgent.corridor.Corners.verts[0], desiredSpeed * NavManager.threadUpdateTimer);
            //运行底层的move进行移动（这句话是无比关键的关键）
            theAgent.corridor.MovePosition(movePos);
            //获取移动之后的位置
            movePos = theAgent.corridor.Position.point;

            //更新agent的数组记录
            theAgent.desiredVelocity =(movePos - theAgent.desiredPosition.point).normalized * desiredSpeed;

            theAgent.desiredSpeedSq = desiredSpeed * desiredSpeed;
            theAgent.desiredPosition = theAgent.corridor.Position;

            mSpeed = desiredSpeed;

            return true;
        }


        //这个方法是真的移动计划的移动方法
        //根据路线进行移动(注意，这个Agent目标移动数据的修改)
        private bool HandlePlanning(bool retargetPos, bool retargetGoal)
        {
            bool needsFullReplan = false;
            //获取记录下来的这些信息
            NavmeshPoint goal = theAgent.plannerGoal;
            NavmeshPoint pos = theAgent.desiredPosition;
            //获取路线引用
            PathCorridor corridor = theAgent.corridor;

            if (retargetGoal && retargetPos)
            {
                //此处调用CritterAI的PathCorridor,进而调用PathCorridorEx走recast
                //从当前的位置转移到希望运动到的位置，另外将目标移动到希望移动到的目标
                corridor.Move(pos.point, goal.point);
                retargetGoal = (goal.polyRef != corridor.Target.polyRef || pos.polyRef != corridor.Position.polyRef);
            }
            else if (retargetPos)
            {
                //此处调用CritterAI的PathCorridor,进而调用PathCorridorEx走recast
                //从当前的位置转移到希望运动到的位置
                corridor.MovePosition(pos.point);
                needsFullReplan = (pos.polyRef != corridor.Position.polyRef);
            }
            else if (retargetGoal)
            {
                //此处调用CritterAI的PathCorridor,进而调用PathCorridorEx走recast
                //将目标移动到希望移动到的目标
                corridor.MoveTarget(goal.point);
                needsFullReplan = (goal.polyRef != corridor.Target.polyRef);
            }

            if (needsFullReplan)
            {
                //完全重新计算路径
                if (theAgent.PlanCorridor(pos, goal) <= 0)
                {
                    Debug.LogError(theAgent.transform.name + ": Could not replan corridor.");
                    return false;
                }
            }
            else if (retargetPos || retargetGoal)
            {
                //CritterAI作者加的强制优化
                theAgent.corridor.OptimizePathTopology(true);
                mOptimizationTimer = OptimizationFrequency;
            }

            //最后重新记录位置就可以了
            theAgent.desiredPosition = theAgent.corridor.Position;
            theAgent.plannerGoal = theAgent.corridor.Target;

            return true;
        }

    }
}
