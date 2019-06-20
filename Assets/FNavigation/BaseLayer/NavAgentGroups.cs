using org.critterai.nav;
using org.critterai.nav.u3d;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//正式使用的寻路名字空间为FNavigation
namespace FNavigation
{
    //共有的寻路设定和资源
    //这是一个数据的共有容器
    //多个agent共有一个group的资源就可以了，主要是PathCorridor和NavPaths的复用
    //不过这里应该还有可以挖的优化的空间
    public class NavAgentGroups
    {
        //基础的crowd设定
        public CrowdAgentParams crowdAgentConfig;
        //更底层NavAgent的参数设定
        public float radiusAt = 0.1f;
        public float radiusNear = 1.0f;
        public float heightTolerance = 1.0f;
        //判定两个角度之间是否相等的依据
        public float angleAt = 1.0f;
        //貌似在crowd的时候就会有效，用于控制转向
        //原文介绍道：当旋转是速度控制的时候使用这个数值
        public float turnThreshold = 0.05f;  
        
        //最大转向速度
        public float maxTurnSpeed = 8;

        //共享的路径缓冲区
        public readonly uint[] pathBuffer;

        private readonly int mMaxPathSize;
        private readonly int mMaxStraightPathSize;

        //路径和通道的还是需要再好好看看
        private readonly int mMaxCorridors;
        private readonly Stack<PathCorridor> mCorridors;

        private readonly int mMaxPaths;
        private readonly Stack<NavPaths> mPaths;

        public NavAgentGroups(CrowdAgentParams crowdAgentConfig, int maxPathSize, int maxStraightPathSize, int maxPoolCorridors, int maxPoolPaths)
        {
            mMaxStraightPathSize = Mathf.Max(1, maxStraightPathSize);
            mMaxPathSize = Mathf.Max(1, maxPathSize);

            this.crowdAgentConfig = crowdAgentConfig;
            this.pathBuffer = new uint[maxPathSize];

            mMaxCorridors = Mathf.Max(0, maxPoolCorridors);
            mCorridors = new Stack<PathCorridor>(mMaxCorridors);

            mMaxPaths = Mathf.Max(0, maxPoolPaths);
            mPaths = new Stack<NavPaths>(mMaxPaths);
        }

        //从池子里面获取通道，如果没有就需要建立一个（目前都是在NavAgent里面处理的）
        public PathCorridor GetCorridor(NavmeshPoint position, NavmeshQuery query, NavmeshQueryFilter filter)
        {
            if (mCorridors.Count > 0)
            {
                PathCorridor corr = mCorridors.Pop();

                if (PathCorridor.LoadLocals(corr, position, query, filter))
                    return corr;

                return null;
            }
            return new PathCorridor(mMaxPathSize, mMaxStraightPathSize, query, filter);
        }

        //回收这个通道（目前都是在NavAgent里面处理的）
        public void ReturnCorridor(PathCorridor corridor)
        {
            if (mCorridors.Count >= mMaxCorridors || corridor == null || corridor.IsDisposed
                || corridor.MaxPathSize != mMaxPathSize
                || corridor.MaxCorners != mMaxStraightPathSize)
            {
                return;
            }

            PathCorridor.ReleaseLocals(corridor);
            mCorridors.Push(corridor);
        }

        //从池子里面获取路径（目前都是在NavAgent里面处理的）
        public NavPaths GetPath()
        {
            if (mPaths.Count > 0)
                return mPaths.Pop();

            return new NavPaths(mMaxPathSize, mMaxStraightPathSize);
        }

        //回收这个路径（目前都是在NavAgent里面处理的）
        public void ReturnPath(NavPaths path)
        {
            if (mPaths.Count >= mMaxPaths || path == null
                || path.path.Length != mMaxPathSize
                || path.straightPath.Length != mMaxStraightPathSize)
            {
                return;
            }

            path.pathCount = 0;
            path.straightCount = 0;

            mPaths.Push(path);
        }
    }
}
