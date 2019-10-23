using org.critterai.nav;
using System.Collections;
using System.Collections.Generic;
using System.Resources;
using UnityEngine;

namespace FNavigation
{
    //Unity上层的agent的控制
    public class UnityAgent : MonoBehaviour
    {
        private NavManager mNavManager;
        private  NavAgent mNavAgent;
        public byte agentGroup = 0;
        public float moveSpeed = 4f;
        private int mManagerIndex = -1;
        //下层的刷新控制单元
        NavState planner;
        //上层（unity层的刷新控制单元）
        UnityNavState mover;

        public bool moverCanControl = true;

        [SerializeField]
        private bool showWayPointsLabel = false;

        public bool showWayPoints
        {
            set
            {
                showWayPointsLabel = value;
                if (value == true)
                {
                    InvokeRepeating("MovePointShow", 0f, wayPointShowTimer);
                }
                else
                {
                    CancelInvoke();
                }
            }

            get
            {
                return showWayPointsLabel;
            }
        }


        public Vector3 nowDestination = Vector3.zero;
        private Material wayPointMaterial;
        //下层刷新坐标的时候如果freeze了Y轴，就仅仅更新XZ轴坐标
        public bool yAxisFreeze = true;

        //设定路点显示材质
        public void SetWayPointMaterial(Material materialIn)
        {
            wayPointMaterial = materialIn;
        }

        #region 导航相关内容
        //外部的设定目标的方法
        public void SetDestination(Vector3 aimPoint)
        {
           NavmeshPoint pt = GetNavPoint(aimPoint);
            if (pt.polyRef == 0)
            {
                //Debug.Log("没有路线："+aimPoint);
                return;
            }
            nowDestination = aimPoint;
            MoveTo(pt, true);
        }


        //是否有路径
        public bool HasPathToAim(Vector3 aim)
        {
            NavmeshPoint pt = GetNavPoint(aim);
            if (pt.polyRef == 0)
                return false;

            return true;
        }

        //闪现到目标位置
        public void FlashToDestionatin(Vector3 aimPoint)
        {

            this.transform.position = aimPoint;
            ResetWayPointCanculate();
            //Stop();
        }


        //确定导航是不是可以控制位移
        public void SetCanControl(bool can)
        {
            if (mover == null)
            {
                //Debug.Log("No Mover!!!!");
                return;
            }
            mover.isMoveControling = can;
            moverCanControl = can;//这个变量仅仅用于调试查看状态
            //Debug.Log("navigation agent Can Control:"+can);
        }
        public bool GetCanControl()
        {
            if (mover == null)
                return false;
             return mover.isMoveControling;
        }

        //设定速度
        public void SetSpeed(float speed)
        {
            moveSpeed = speed;
            if (mNavAgent != null)
            mNavAgent.moveSpeed = this.moveSpeed;
        }

        //外部设定，停止的方法
        public void Stop()
        {
            SetDestination(this.transform.position);
        }

        //返回导航的时候下一个点的坐标
        public Quaternion GetNavAgentForward()
        {
            if (mNavAgent != null)
                return mNavAgent.goalRotation;

            return this.transform.rotation;
        }

        //根据目标点来转换目标，vector3 -> NavmeshPoint
        private NavmeshPoint GetNavPoint(Vector3 point)
        {
            if (mNavAgent == null)
                return default(NavmeshPoint);

            return mNavAgent.GetPointSearch(point);
        }

        //给出目标进行移动的方法
        private void MoveTo(NavmeshPoint goal, bool ignoreGoalRotation)
        {
            if (mNavAgent == null)
                return;

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
        public bool MakeInitialize()
        {
            //NavManager.ActiveManager的获取还需要一个更好的设计
            if (!enabled)
            {
                Debug.LogError(this.name + ": is not enabled ");
                return false;
            }

            if ( NavManager.ActiveManager == null)
            {
                Debug.LogError(this.name + ": no activeManager ");
                return false;
            }
            mNavManager = NavManager.ActiveManager;
            //建立navAgent,这是真正用于移动的agent
            mNavAgent = mNavManager.CreateAgent(agentGroup, transform);
            mNavAgent.moveSpeed = this.moveSpeed;
            if (mNavAgent == null)
            {
                Debug.LogError(this.name + ": agent create failed");
                enabled = false;
                return false;
            }
            mNavAgent.rotation = transform.rotation;
            mNavAgent.position = mNavAgent.GetPointSearch(transform.position);
            if (mNavAgent.position.polyRef == 0)
            {
                mNavAgent = null;
                Debug.LogError(this.name + ": take agent into navmesh failed: "+ mNavAgent.position);
                enabled = false;
                return false;
            }

            //planner是底层计算推进（可以考虑多线程）
            //mover是unity位置改变，只能在主线程
            planner = new NavPlanner(mNavAgent);
            //用于表现，也就是修改Unity中的移动的move
            //UnitySimpleMovePlan unityMovePlan  = new UnitySimpleMovePlan();
            UnityMoveWithFixdRoute unityMovePlan = new UnityMoveWithFixdRoute();
            unityMovePlan.SetNavAgent(mNavAgent);
            unityMovePlan.yAxisFreeze = this.yAxisFreeze;
            mover = unityMovePlan;
            mManagerIndex = mNavManager.AddAgent(planner, unityMovePlan);
            if (mManagerIndex == -1)
            {
                Debug.LogError(this.name + ": add agent to navigation manager failed");
                enabled = false;
                return false;
            }
            Stop();
            return true;
        }

        //额外辅助的一些方法
        public bool IsAtDestiontion()
        {
            if (mNavAgent == null)
            {
                Debug.Log("NavAgent == null , can not check IsAtDestiontion");
                return true;
            }

            return mNavAgent.IsAtDestination();
        }


        //删除删除下层
        void OnDisable()
        {
            if (this.mManagerIndex >= 0)
            {
                NavManager.ActiveManager.RemoveAgent(this.mManagerIndex);
                if (showWayPoints)
                {
                    //直接调用属性的set来取消一些东西
                    showWayPoints = false;
                }
            }
        }
        #endregion

        #region 路点指示器

        LineRenderer theShowLineRenderer = null;
        private static Vector3 [] nullVector = new Vector3[0];
        private static Vector3 thisPosition = Vector3.zero;
        private static Vector3 positionOffset = new Vector3(0, 0.2f, 0);

        private float wayPointShowTimer = 0.25f;
        //将这个方法
        //设定路点的显示内容(每次设定目标的手刷)
        private void MovePointShow()
        {
            if (mNavAgent == null)
                return;

            if (!showWayPoints  || IsAtDestiontion())
                return;

            Vector3[] routePoints = mover.GetRoutePoint();
            GetLineRenderer();
            theShowLineRenderer.SetPositions(nullVector);
            theShowLineRenderer.material = wayPointMaterial;

            int currentIndex = mNavAgent.GetCurrentWayPointIndex();

            int newLength = routePoints.Length - currentIndex + 1;
            if (currentIndex < 0 || newLength <= 1 || routePoints.Length <= 0)
            {
                //ClearWayPointShow();
                ResetWayPointCanculate();
                return;
            }

            Vector3[] newLine = new Vector3[newLength];
            thisPosition.x = this.transform.position.x;
            thisPosition.z = this.transform.position.z;
            thisPosition.y = GetTerrainPosY(thisPosition.x, thisPosition.z);
            newLine[0] = thisPosition;
            for (int i = 1 ; i < newLength; i++)
            {
                newLine[i] = routePoints[currentIndex+i-1]+ positionOffset;
            }
            theShowLineRenderer.SetVertexCount(newLength);
            theShowLineRenderer.SetPositions(newLine);
        }


        public  float GetTerrainPosY(float x, float z)
        {
            Ray ray = new Ray(new Vector3(x, 50, z), Vector3.down);
            RaycastHit hitInfo;
            if (Physics.Raycast(ray, out hitInfo, 100, 1 << 12))
            {
                return hitInfo.point.y;
            }

            return 0;
        }

        //清空路点显示
        public void ClearWayPointShow()
        {
            GetLineRenderer();
            theShowLineRenderer.SetPositions(nullVector);
            theShowLineRenderer.SetVertexCount(0);
        }



        public void ResetWayPointCanculate()
        {
            // mNavAgent.path.straightPath = null;
            mNavAgent.path.isDirty = true;
        }

        private void  GetLineRenderer()
        {
            if (theShowLineRenderer == null)
            {
                theShowLineRenderer = this.gameObject.GetComponent<LineRenderer>();
            }
            if (theShowLineRenderer == null)
            {
                theShowLineRenderer = this.gameObject.AddComponent<LineRenderer>();
                theShowLineRenderer.textureMode = LineTextureMode.Tile;
                theShowLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                theShowLineRenderer.receiveShadows = false;
                theShowLineRenderer.shadowBias = 0;
                theShowLineRenderer.alignment = LineAlignment.TransformZ;
                theShowLineRenderer.SetWidth(1f, 1f);
            }
        }
        #endregion

    }
}
