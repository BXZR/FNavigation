using org.critterai.nav;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//正式使用的寻路名字空间为FNavigation
namespace FNavigation
{
    //路径信息存储区
    public class NavPaths 
    {
        //路径存储
        public uint[] path;
        //直接路径存储
        public uint[] straightPath;
        //直接路径路点存储
        public Vector3[] straightPoints;
        //直接路径flag
        public WaypointFlag[] straightFlags;
        //路径缓存长度
        public int pathCount = 0;
        //直接路径缓存长度
        public int straightCount = 0;
        //记录：最大直接路径信息长度
        private int mMaxStraightPathSize = 0;

        public bool isDirty = false;

        public NavPaths(int maxPathSize, int maxStraightPathSize)
        {
            path = new uint[Mathf.Max(1, maxPathSize)];
            maxStraightPathSize = Mathf.Max(1, maxStraightPathSize);
            straightPoints = new Vector3[maxStraightPathSize];
            straightFlags = new WaypointFlag[maxStraightPathSize];
            straightPath = new uint[maxStraightPathSize];
            mMaxStraightPathSize = maxStraightPathSize;
        }

        //返回路径中最后一个多边形
        public uint LastPathPolyRef
        {
            get { return (pathCount == 0 ? 0 : path[pathCount - 1]); }
        }

        //返回提供多边形引用的路径索引
        public int FindPolyRefReverse(int startIndex, uint endRef)
        {
            for (int i = pathCount - 1; i >= startIndex; i--)
            {
                if (path[i] == endRef)
                    return i;
            }
            return -1;
        }

        public int FindPolyRef(int startIndex, uint polyRef)
        {
            for (int i = startIndex; i < pathCount; i++)
            {
                if (path[i] == polyRef)
                    return i;
            }
            return -1;
        }
        //获取指定直线路径缓冲区索引处的点
        public NavmeshPoint GetStraightPoint(int index)
        {
            return new NavmeshPoint(straightPath[index], straightPoints[index]);
        }

        //修正路径，用来放置planner哪里全盘重新制定计划
        //目前来看是一个特殊的移动计划会用这个方法
        public bool FixPath(uint startRef, uint endRef)
        {
            if (pathCount == 0)
                return false;

            if (startRef == endRef)
            {
                path[0] = startRef;
                pathCount = 1;
                straightCount = 0;
                return true;
            }
            if (startRef == path[0] && endRef == path[pathCount - 1])
                return true;

            int iStart = FindPolyRef(0, startRef);
            if (iStart == -1)
                return false;

            int iEnd = FindPolyRefReverse(0, endRef);
            if (iEnd == -1)
                return false;

            pathCount = Mathf.Abs(iEnd - iStart) + 1;

            if (iStart > iEnd)
            {
                System.Array.Reverse(path, iEnd, pathCount);
                iStart = iEnd;
            }
            for (int i = 0; i < pathCount; i++)
            {
                path[i] = path[iStart + i];
            }

            straightCount = 0;
            return true;
        }

        //使用现有路径从起点到目标点建立标准的直接路径
        public NavStatus BuildStraightPath(NavmeshPoint start, NavmeshPoint goal, NavmeshQuery query)
        {
            int iStart = FindPolyRef(0, start.polyRef);
            int iGoal = -1;

            if (iStart != -1)
                iGoal = FindPolyRefReverse(iStart, goal.polyRef);

            if (iGoal == -1)
                return (NavStatus.Failure | NavStatus.InvalidParam);
            //NavmeshQuery是直通C++的底层寻路

            Array.Clear(straightPoints , 0 , straightPoints.Length);

            NavStatus status = query.GetStraightPath(start.point, goal.point
                , path, iStart, iGoal - iStart + 1
                , straightPoints, straightFlags, straightPath, out straightCount);

            if (straightCount == 0)
                return NavStatus.Failure;

            return status;
        }

        //创作一个新的直接路径，并且沿路获取点
        public int GetLocalTarget(NavmeshPoint start, NavmeshPoint goal, int maxLength, NavmeshQuery query, out NavmeshPoint target)
        {
            if (NavUtil.Failed(BuildStraightPath(start, goal, query)))
            {
                target = new NavmeshPoint();
                return -1;
            }

            int targetIndex = straightCount; 
            int iStart = FindPolyRef(0, start.polyRef);

            uint targetRef = 0;
            do
            {
                targetIndex--;
                targetRef = (straightPath[targetIndex] == 0 ? goal.polyRef : straightPath[targetIndex]);
            }
            while (FindPolyRefReverse(iStart, targetRef) - iStart + 1 > maxLength && targetIndex > 0);
            target = new NavmeshPoint(targetRef, straightPoints[targetIndex]);

            return targetIndex;
        }
    }
}
