using org.critterai;
using org.critterai.nav;
using org.critterai.nav.u3d;
using UnityEngine;

//正式使用的寻路名字空间为FNavigation
namespace FNavigation
{
    //与底层交互的代理
    //相较于CritterAI的demo参数做了精简，如果有额外需求可以在这里扩展
    //这个代理包含寻路的调用，并会背控制单元如planner调用
    //暂时先不考虑数据和控制的分离
    public class NavAgent  
    {
        //----------------------数据部分------------------------------//
        //操作的标记
        public NavFlag flags;
        public NavAgentMode targetMode;

        //当前状态
        public NavStatus navStatus;
        //当前的位置
        public NavmeshPoint position;
        //最终目标
        public NavmeshPoint goal;
        //planner得到的目标，如果有其他的planner，可能需要根据这个和最终目标的对照切换planner
        public NavmeshPoint plannerGoal;
        //目标方向
        public Quaternion goalRotation;
        //自定义的移动速度
        public float moveSpeed;

        //当前的方向
        public Quaternion rotation;
        //目标位置，这个是在plannner的mover里面进行设定的
        public NavmeshPoint desiredPosition;
        //所需要的速度，这个也是在mover里面设定
        public Vector3 desiredVelocity;
        //计算的时候的一个缓存值，内部计算使用
        public float desiredSpeedSq;

        //确定是否到达使用
        public float radiusAt;
        //确定一个点是否在另一个点附近时要使用的半径
        public float radiusNear;
        //这个参数用于高度检查使用
        public float heightTolerence;

        //----------------------操作数据部分------------------------------//
        //共有的group配置和资源（上层）
        public NavAgentGroups agentGroup;
        //共有的group配置和资源（底层）
        public NavGroup navGroup;
        //被控制的物体，主要用于上层的位置更新，同时底层报错会引用名字
        public Transform transform;
        //路径检查范围,受到navGroup的控制
        public Vector3 wideExtents = new Vector3(10, 10, 10);

        //planner使用的通道
        public PathCorridor corridor;
        //planner使用的路径信息
        public NavPaths path;
        //内部使用的crowd的配置
        public CrowdAgentParams crowdConfig;
        //planner设定的crowd代理，如果不使用就是空
        public CrowdAgent crowdAgent;

        //----------------------------方法部分------------------------------//
        public NavAgent(Transform theTransform, NavGroup theGroup, NavAgentGroups theAgentGroup)
        {
            this.agentGroup = theAgentGroup;
            this.navGroup = new NavGroup(theGroup , false);
            this.transform = theTransform;
            this.wideExtents = this.navGroup.extents * 10f;
            RevertToAgentGroup();
        }

        public void RevertToAgentGroup()
        {
            crowdConfig = agentGroup.crowdAgentConfig;
            radiusAt = agentGroup.radiusAt;
            radiusNear = agentGroup.radiusNear;
            heightTolerence = agentGroup.heightTolerance;

            if (crowdAgent != null)
                crowdAgent.SetConfig(crowdConfig);
        }

        //调用底层的NavmeshQuery（导航网格查询）进行查询，这样就将设定的点与导航网格的点联系起来
        //如果该点没有多边形引用，则对其执行加宽搜索
        public NavmeshPoint GetPointSearch(NavmeshPoint point)
        {
            NavmeshPoint result = point;
            if (result.polyRef == 0)
            {
                navStatus = navGroup.query.GetNearestPoint(point.point, navGroup.extents, navGroup.filter, out result);
                if (result.polyRef == 0)
                {
                    navStatus = navGroup.query.GetNearestPoint(point.point, wideExtents, navGroup.filter, out result);
                }
            }
            return result;
        }
        //调用底层的NavmeshQuery（导航网格查询）进行查询，这样就将设定的点与导航网格的点联系起来
        //如果该点没有多边形引用，则对其执行加宽搜索
        public NavmeshPoint GetPointSearch(Vector3 point)
        {
            NavmeshPoint result;
            navStatus = navGroup.query.GetNearestPoint(point, navGroup.extents, navGroup.filter, out result);
            if (result.polyRef == 0)
            {
                navStatus = navGroup.query.GetNearestPoint(point, wideExtents, navGroup.filter, out result);
            }
            return result;
        }

        //设置道路资源，目前来看是一个必要的过程
        //主要是为了操作group
        public void SetCorridorAssets(bool enabled)
        {
            if (enabled)
            {
                if (corridor == null)
                {
                    corridor = agentGroup.GetCorridor(position, navGroup.query, navGroup.filter);
                }
                flags |= NavFlag.CorridorInUse;
            }
            else
            {
                agentGroup.ReturnCorridor(corridor);
                corridor = null;
                flags &= ~NavFlag.CorridorInUse;
            }
        }


        public void RemoveFromCrowd()
        {
            if (crowdAgent != null)
            {
                navGroup.crowd.RemoveAgent(crowdAgent);
                crowdAgent = null;
            }
        }

        public void SetPathAssets(bool enabled)
        {
            if (enabled)
            {
                if (path == null)
                    path = agentGroup.GetPath();

                flags |= NavFlag.PathInUse;
            }
            else
            {
                agentGroup.ReturnPath(path);

                path = null;
                flags &= ~NavFlag.PathInUse;
            }
        }


        //寻找路线：查找从起点到终点的路径，并将其加载到道路中
        public int PlanCorridor(NavmeshPoint start, NavmeshPoint end)
        {
            //原作标识：不要尝试重用corridor，这个东西有更多的可能性变得很畸形

            int pathCount;
            //如果点不可用就会直接失败
            if (start.polyRef == 0 || end.polyRef == 0 || corridor == null)
                return -1;

            //如果相同点直接结束，navStatus更新
            if (start.polyRef == end.polyRef)
            {
                corridor.Reset(start);
                corridor.MoveTarget(end.point);
                navStatus = NavStatus.Sucess;
                return 1;
            }
            else
            {
                //调用NavmeshQuery的方法进行寻路，然后内部走NavmeshQueryEx类调用dll
                //最后初步猜测调用Detour的DetourNavMeshQuery（有待考证）
                navStatus = navGroup.query.FindPath(start, end, navGroup.filter, agentGroup.pathBuffer , out pathCount);
            }

            corridor.Reset(start);

            if (pathCount > 0)
            {
                corridor.SetCorridor(end.point, agentGroup.pathBuffer , pathCount);
            }

            return pathCount;
        }


        //原则上需要根据flag的情况切换mode的，但是作为初始版本，先简化为simple和Crowd
        //这个方法需要与其他地方一起扩展
        //这里的mode其实应该考虑性能等等的问题的，这是一个很大的坑
        public NavAgentMode ChangeMode()
        {
            //CrowdManager crowd = navGroup.crowd;
            //if ((flags & NavFlag.UseCrowd) != 0 && ((crowdAgent != null) || (crowd != null && crowd.AgentCount < crowd.MaxAgents)))
            //{
            //    // Want to use the crowd, and can use crowd.
            //    if ((flags & NavFlag.GoalIsDynamic) == 0)
            //        return NavAgentMode.CrowdMove;
            //    else
            //        return NavAgentMode.FollowGoalCrowdMove;
            //}
            if(NavManager.ActiveManager == null || NavManager.ActiveManager.theModeNow == NavAgentMode.SimpleMove)
                return NavAgentMode.SimpleMove;

            return NavAgentMode.CrowdMove;
        }

        //判断两个点是否相近，调用底层来做的
        public bool IsNear(Vector3 PointA, Vector3 pointB)
        {
            return Vector3Util.IsInRange(PointA, pointB, radiusNear, heightTolerence);
        }

        //--------------------------------------------Crowd调用相关方法----------------------------------------------------------------//

        //如果不在crowd里面就把agent加入到crowd里面去
        public CrowdAgent AddToCrowd(Vector3 position)
        {
            if (navGroup.crowd == null)
                return null;
            if (crowdAgent == null)
            {
                crowdAgent = navGroup.crowd.AddAgent(position, crowdConfig);
            }
            else
            {
                if (crowdAgent.Position != position)
                {
                    RemoveFromCrowd();
                    crowdAgent = navGroup.crowd.AddAgent(position, crowdConfig);
                }
                else if ((flags | NavFlag.CrowdConfigUpdated) != 0)
                    crowdAgent.SetConfig(crowdConfig);
            }

           flags &= ~NavFlag.CrowdConfigUpdated;
            return crowdAgent;
        }

        //个人理解是这位了存crowdAgent的数值
        public void SyncCrowdToDesired()
        {
            desiredPosition.polyRef = crowdAgent.PositionPoly;
            desiredPosition.point = crowdAgent.Position;

            desiredVelocity = crowdAgent.Velocity;
            desiredSpeedSq = desiredVelocity.sqrMagnitude;
        }

        //从起点到终点的寻路
        //如果没找到路就返回-1
        public int PlanPath(NavmeshPoint start, NavmeshPoint end)
        {
            path.straightCount = 0;

            if (start.polyRef == 0 || end.polyRef == 0 || path == null)
                return -1;

            if (start.polyRef == end.polyRef)
            {
                path.pathCount = 1;
                path.path[0] = start.polyRef;
                navStatus = NavStatus.Sucess;
                return 1;
            }

            if (path.FixPath(start.polyRef, end.polyRef))
            {
                navStatus = NavStatus.Sucess;
                return path.pathCount;
            }

           //这是底层寻路的方法了
           navStatus = navGroup.query.FindPath(start , end , navGroup.filter , path.path, out path.pathCount);

           return path.pathCount;
        }


        //额外辅助方法部分,暂定到达目标就算停止
        public bool IsAtDestination()
        {
            //原作中crowd用2D做检查，simple用3D做检查。
            //这目前来看似乎没什么道理，所以索性整合在一起做一个方法，还可以返回给上层UnityAgent用
            bool Check3D = Vector3Util.SloppyEquals(desiredPosition.point, plannerGoal.point, 0.005f);
            Vector3 pos = desiredPosition.point;
            Vector3 goal = plannerGoal.point;
            bool Check2D = Vector2Util.SloppyEquals(new Vector2(pos.x, pos.z), new Vector2(goal.x, goal.z), 0.005f);
            return (Check3D | Check2D);
        }

        //获取路点信息
        public Vector3[] GetRoutePoints()
        {
            if (NavManager.ActiveManager == null)
            {
                return new Vector3[0];
            }

            Vector3[] theRoutePoints = new Vector3[path.straightCount];
            for (int i = 0; i < path.straightCount; i++)
                theRoutePoints[i] = path.straightPoints[i];
            return theRoutePoints;

            /*
             *如果是simplemove模式，也可以在corridor.Corners.verts里面获得相同的路点信息
            if (NavManager.ActiveManager.theModeNow == NavAgentMode.CrowdMove)
            {
                return path.straightPoints;
            }
            else
            {
                return corridor.Corners.verts;
            }
            */
        }

        //获取当前路点下标
        public int GetCurrentWayPointIndex()
        {
            if (NavManager.ActiveManager == null)
                return -1;
            if (path.straightCount <= 0)
                return -1;

            if (corridor == null || corridor.Corners == null)
                return -1;

            WaypointFlag[] flags = corridor.Corners.flags;
            /*
            string showString = "";
            for (int i = 0; i < flags.Length; i++)
            {
                showString += flags[i];
            }
            Debug.Log(showString);
            */

            /*
            路点的处理是到这来的
            第一个路点结束
            0000EndEnd00000000000000000000000000
            第二个路点结束
            000EndEndEnd00000000000000000000000000
            第三个路点结束
            00EndEndEndEnd00000000000000000000000000
            所以起点算一个路点
            */
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i] == WaypointFlag.End)
                {
                    return path.straightCount -1 - i;
                }
            }
            //返回的是当前目标路点下标
            return path.straightCount - 1;
        }

    }
}
