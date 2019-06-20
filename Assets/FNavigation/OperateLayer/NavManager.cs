//正式使用的寻路名字空间为FNavigation
using org.critterai.nav.u3d;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace FNavigation
{
    //调用NavAgent的管理类
    //导航的循环是在这里处理的
    //如果需要多线程处理，这个类的循环调用处理是关键点
    //这个类用于处理上层和底层的位置更新
    public class NavManager
    {
        public static NavManager ActiveManager = null;
        public static float threadUpdateTimer = 0.03f;//多线程等待时间

        //共有的group配置和资源（底层）
        private NavGroup mNavGroup;
        //分组处理
        private Dictionary<byte, NavAgentGroups> mAgentGroups;
        //planners是跟底层打交道的位置计算处理
        private NavState[] mPlanners;
        //movers是跟unity打交道的位置更新
        private NavState[] mMovers;
        //当前移动方式
        public NavAgentMode theModeNow;

        //多线程
        Thread baseUpdateThread = null;
        //赋值为false也就是没有信号
        public static AutoResetEvent myResetEvent = new AutoResetEvent(false);

        public static NavManager Create(int maxAgents, NavGroup navGroup, Dictionary<byte, NavAgentGroups> agentGroups)
        {
            if (navGroup.crowd == null || navGroup.crowd.IsDisposed
                || navGroup.mesh == null || navGroup.mesh.IsDisposed
                || navGroup.query == null || navGroup.query.IsDisposed
                || agentGroups == null || agentGroups.Count == 0)
            {
                return null;
            }

            return new NavManager(maxAgents, navGroup, agentGroups);
        }

        //构造方法，最后被直接调用生成
        //原有的架构使用静态共有方法包装了一层
        public NavManager(int maxAgents, NavGroup navGroup, Dictionary<byte, NavAgentGroups> agentGroups)
        {
            maxAgents = UnityEngine.Mathf.Max(1, maxAgents);

            mPlanners = new NavState[maxAgents];
            mMovers = new NavState[maxAgents];
            mNavGroup = navGroup;
            mAgentGroups = new Dictionary<byte, NavAgentGroups>(agentGroups);
        }

        //创造一个寻路代理
        public NavAgent CreateAgent(byte group, Transform agent)
        {
            if (!agent || !mAgentGroups.ContainsKey(group))
                return null;

            return new NavAgent(agent, mNavGroup, mAgentGroups[group]);
        }

        //根据下表删除一个寻路代理
        //在更新的时候，将已经完成的代理删除掉
        public void RemoveAgent(int index)
        {
            if (mPlanners[index] != null)
                mPlanners[index].Exit();

            mPlanners[index] = null;

            if (mMovers[index] != null)
                mMovers[index].Exit();

            mMovers[index] = null;
        }

        //将一个agent的计算方法加入到计算循环中去
        //返回的是插入的下标，返回-1说明失败了
        public int AddAgent(NavState planner, NavState mover)
        {
            if (planner == null || mover == null)
                return -1;

            for (int i = 0; i < mPlanners.Length; i++)
            {
                if (mPlanners[i] == null)
                {
                    if (!planner.Enter())
                        break;

                    if (!mover.Enter())
                    {
                        planner.Exit();
                        break;
                    }

                    mPlanners[i] = planner;
                    mMovers[i] = mover;

                    return i;
                }
            }
            return -1;
        }

        //更新：寻路的计算更新循环
        //使用多线程更新底层计算
        private void ThreadUpdate()
        {
            while (true)
            {
                Thread.Sleep(TimeSpan.FromSeconds(threadUpdateTimer));
                //执行到这个地方时，会等待set调用后改变了信号才接着执行

                if (mNavGroup.crowd.IsDisposed)
                    return;

                //这里进入critter的dll，然后再调用recast的dll
                //目前有关crowd的方法还在研究中，没有使用
                mNavGroup.crowd.Update(threadUpdateTimer);
                 
                for (int i = 0; i < mPlanners.Length; i++)
                {
                    NavState aPlan = mPlanners[i];

                    if (aPlan == null || aPlan.Update())
                        continue;

                    aPlan.Exit();
                    if (!aPlan.Enter())
                        RemoveAgent(i);
                }
            }
        }

        //开启底层的更新线程
        public void StartThreadUpdate()
        {
            if (baseUpdateThread != null)
                baseUpdateThread.Abort();
            baseUpdateThread = new Thread(ThreadUpdate);
            baseUpdateThread.Start();
        }

        //关闭底层的更新线程
        public void StopThreadUpdate()
        {
            if (baseUpdateThread != null)
                baseUpdateThread.Abort();
        }

        //更新：寻路的计算更新循环
        //这个更新是需要找一个unity上层的类来调用进行不断的更新的
        public void MakeUpdate()
        {
            for (int i = 0; i < mPlanners.Length; i++)
            {
                NavState aMove = mMovers[i];

                if (aMove == null || aMove.Update())
                    continue;

                aMove.Exit();
                if (!aMove.Enter())
                    RemoveAgent(i);
            }
        }

    }
}
