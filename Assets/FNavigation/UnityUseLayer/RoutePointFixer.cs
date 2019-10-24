using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FNavigation
{
    //这个类用于对UnityAgent寻路获得的路点信息进行后期处理
    //例如动态规避collider等
    class RoutePointFixer
    {
        private static RoutePointFixer instanece = null;
        public static RoutePointFixer Instance
        {
            get
            {
                if (instanece == null)
                    instanece = new RoutePointFixer();
                return instanece;
            }
        }

        //根据物理场景修正路点之间的联系
        public Vector3[] FixRouteWithPhysics(Vector3[] basicRoutPoints)
        {
            return MakeFixedRoute(basicRoutPoints); 
        }

        private Vector3 [] MakeFixedRoute(Vector3[] routePoints)
        {
            for (int i = 1; i < routePoints.Length; i++)
            {
                Debug.Log("routePoints.Length = " + routePoints.Length);
                Vector3 direction = (routePoints[i] - routePoints[i - 1]).normalized;
                bool hasObstacle = HasObstacleInWay(routePoints[i -1], direction);
                //Debug.Log("hasObstacle = "+ hasObstacle + "   direction = "+ direction);
                if (hasObstacle)
                {
                    //根据射线撞击的点向后退一点点建立一个新的路点newPoint
                    Vector3 hitPoint = GetObstacleHitPoiint(routePoints[i -1], direction);
                    Vector3 offset = (routePoints[i -1 ] - hitPoint).normalized * 0.1f;
                    Vector3 newPoint = hitPoint + offset;
                    //CreateGiz(newPoint , "newPoint");

                    //寻找以这个新的路点作为起点转向之后能够走出障碍的方向
                    Vector3 AimDir = Vector3.zero;
                     bool isOk =  GetNoObstacleDirection(newPoint , direction, out AimDir);
                    Debug.Log("isOK = " + isOk);
                    if (!isOk)
                        continue;

                   // CreateGiz(newPoint + AimDir.normalized*3, "AimDir");
                   // LineRenderer xl= x.AddComponent<LineRenderer>();
                   // xl.SetPositions(new Vector3[] { newPoint , newPoint + AimDir.normalized * 3 });

                    //获得一个新的转折点
                    Vector3 newPoint2 = GetNewPointInNewDiretion(newPoint , AimDir);
                    //CreateGiz(newPoint2 , "newPoint2");
                    //将这两个点插入到路点中
                    routePoints = InstertPoints(i -1 , newPoint, newPoint2 , routePoints);
                    i++;

                    //调试用////////////////////////////
                    //break;
                }
            }
            return routePoints;
        }

        GameObject x;
        private void CreateGiz(Vector3 aim , string name)
        {
            x =  GameObject.CreatePrimitive(PrimitiveType.Sphere);
            x.transform.position = aim;
            GameObject.Destroy(x.GetComponent<Collider>());
            x.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            x.name = name;
        }

        private Vector3[] InstertPoints(int index, Vector3 point1, Vector3 point2, Vector3[] aim)
        {
            Vector3[] newVec = new Vector3[aim.Length + 2];
            for (int i = 0; i <= index; i++)
            {
                newVec[i] = aim[i];
            }
            newVec[index + 1] = point1;
            newVec[index + 2] = point2;
            for (int i = index + 1; i < aim.Length; i++)
            {
                newVec[i + 2] = aim[i];
            }
            return newVec;
        }

        private Vector3 GetNewPointInNewDiretion(Vector3 startPoint, Vector3 diretion)
        {
            /*
             * angle：旋转度数
               axis:围绕哪个轴旋转
               oriVec:初始向量
             */
            //将这个方向逆时针旋转90度做检测用
            Vector3 newVec =  (Quaternion.AngleAxis(90, Vector3.up) * diretion).normalized;
            Debug.Log("newVec ===" + Quaternion.FromToRotation(Vector3.forward, -newVec).eulerAngles);
            Vector3 positionNow = startPoint;
            while (HasObstacleInWay(positionNow, -newVec))
            {
                positionNow += diretion * 0.5f;
            }
            return positionNow;
        }

        //环形搜索可以走珠当前障碍物的方向
        private bool GetNoObstacleDirection(Vector3 startPoint , Vector3 startDirection , out Vector3 aimVec )
        {
            Vector3 eulerAngles = Quaternion.FromToRotation(Vector3.forward, startDirection ).eulerAngles;
            Debug.Log("eulerAngles = "+ eulerAngles.y);
            float nowAngle = eulerAngles.y;
            for (float angle = 0f; angle  < 360f; angle += 10f)

            {
                Debug.Log("startDirection = " + startDirection);
                Vector3 checkDir = ( Quaternion.AngleAxis(angle, Vector3.up) * startDirection );
                Debug.Log(" checkDir = " + checkDir);

                bool hasObInThisDir = HasObstacleInWay(startPoint , checkDir);
                if (hasObInThisDir == false)
                {
                    Debug.Log("aimDir = " + checkDir);
                    if (x != null)
                    {
                        x.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, checkDir);
                     }

                    aimVec =  checkDir;
                    return true;
                }
            }
            aimVec = Vector3.zero;
            return false;
        }


        private bool HasObstacleInWay(Vector3 start, Vector3 direction)
        {
            return Physics.Raycast(start, direction , 10f);
        }

        private Vector3 GetObstacleHitPoiint(Vector3 start, Vector3 direction)
        {
            RaycastHit hits;
            Physics.Raycast(start, direction, out hits, 10f);
            return hits.point;
        }


        //检查两点之间是不是有障碍物
        private bool HasObstaclesInWay(Vector3 start , Vector3  end)
        {
            Vector3 direction = (end - start).normalized;
            float distance = Vector3.Distance(start, end);
            RaycastHit[] hits = Physics.RaycastAll(start, direction, distance);
            return hits.Length != 0;
        }

    }
}
