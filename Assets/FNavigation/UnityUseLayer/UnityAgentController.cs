using org.critterai.nav;
using org.critterai.nav.u3d;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FNavigation
{
    public class UnityAgentController 
    {
        public bool isSetup = false;

        public NavAgentMode theAgentMode = NavAgentMode.SimpleMove;
        [SerializeField]
        private ScriptableObject mNavmeshData = null;
        private byte[] navMeshData = new byte[0];

        [SerializeField]
        public CrowdAvoidanceParams[] CrowdAvoidanceConfig;

        [SerializeField]
        private int mMaxQueryNodes = 2048;//2048

        [SerializeField]
        private int mMaxCrowdAgents = 64;//200

        [SerializeField]
        private float mMaxAgentRadius = 0.5f;

        [SerializeField]
        private int mMaxPath = 512;//256

        [SerializeField]
        private int mMaxStraightPath = 32;//32

        [SerializeField]
        private int mMaxAgents = 128;//32

        [SerializeField]
        private Vector3 mExtents = new Vector3(1, 1, 1);

        [SerializeField]
        private Vector3 mWideExtents = new Vector3(10, 10, 10);

        [SerializeField]
        private float mHeightTolerance = 1.0f;

        [SerializeField]
        private float mTurnThreshold = 0.05f;

        [SerializeField]
        private float mAngleAt = 1.0f;

        public INavmeshData NavmeshData
        {
            get { return (mNavmeshData ? (INavmeshData)mNavmeshData : null); }
            set
            {
                if (value is ScriptableObject)
                    mNavmeshData = (ScriptableObject)value;
            }
        }

        private NavManager mManager;

        public NavManager CreateManager()
        {
            CheckCrowdAvoidanceSet();

            if (!(mNavmeshData && NavmeshData.HasNavmesh))
            {
                Debug.LogError("Aborted initialization. Navigation mesh not available.");
                return null;
            }

            //Debug.Log("NavmeshData-------"+ NavmeshData);
            Navmesh navmesh =  NavmeshData.GetNavmesh();
            if (navmesh == null)
            {
                NavStatus theStatus = Navmesh.Create(navMeshData, out navmesh);

                Debug.Log("Navmesh.Create ---->" + theStatus + "---->" + (int)(theStatus & NavStatus.Sucess));
                if (NavUtil.Failed(theStatus))
                {
                    Debug.LogError("NavUtil.Failed(Navmesh.Create(navMeshData, out navmesh) Fail!");
                }
                Debug.Log("--------------------\n" + navMeshData + "---" + navMeshData.Length + "\n-----------------\nNavmesh-------" + navmesh);
            }
            if (navmesh == null)
            {
                Debug.LogError(" navmesh is null");
                return null;
            }
            NavmeshQuery query;
            NavStatus status = NavmeshQuery.Create(navmesh, mMaxQueryNodes, out query);
            if ((status & NavStatus.Sucess) == 0)
            {
                Debug.LogError(" Aborted initialization. Failed query creation: " + status.ToString());
                return null;
            }

            CrowdManager crowd = CrowdManager.Create(mMaxCrowdAgents, mMaxAgentRadius, navmesh);
            if (crowd == null)
            {
                Debug.LogError("Aborted initialization. Failed crowd creation.");
                return null;
            }

            for (int i = 0; i < CrowdManager.MaxAvoidanceParams; i++)
            {
                crowd.SetAvoidanceConfig(i, CrowdAvoidanceConfig[i]);
            }
            NavGroup mGroup = new NavGroup(navmesh, query, crowd, crowd.QueryFilter, mExtents, false);

            int count = AgentGroupSettingManager.GetGroupCount();
            Dictionary<byte, NavAgentGroups> mAgentGroups = new Dictionary<byte, NavAgentGroups>(count);

            for (int i = 0; i < count; i++)
            {
                byte groupId;
                NavAgentGroups group = AgentGroupSettingManager.CreateAgentGroup(i, mMaxPath, mMaxStraightPath, out groupId);
                group.angleAt = mAngleAt;
                group.heightTolerance = mHeightTolerance;
                group.turnThreshold = mTurnThreshold;

                mAgentGroups.Add(groupId, group);
            }
            return NavManager.Create(mMaxAgents, mGroup, mAgentGroups);
        }


        private void CheckCrowdAvoidanceSet()
        {
            if(CrowdAvoidanceConfig == null || CrowdAvoidanceConfig.Length != CrowdManager.MaxAvoidanceParams)
            {
                CrowdAvoidanceConfig = new CrowdAvoidanceParams[CrowdManager.MaxAvoidanceParams];

                CrowdAvoidanceConfig[0] = CrowdAvoidanceParams.CreateStandardLow();
                CrowdAvoidanceConfig[1] = CrowdAvoidanceParams.CreateStandardMedium();
                CrowdAvoidanceConfig[2] = CrowdAvoidanceParams.CreateStandardGood();
                CrowdAvoidanceConfig[3] = CrowdAvoidanceParams.CreateStandardHigh();

                for (int i = 4; i < CrowdAvoidanceConfig.Length; i++)
                {
                    CrowdAvoidanceConfig[i] = new CrowdAvoidanceParams();
                }
            }
        }

        public void MakeStart(ScriptableObject bakedMesh , byte [] myNavData )
        {
            try
            {
                mNavmeshData = bakedMesh;
                navMeshData = myNavData;
                /*
                Debug.Log("got navMeshData version1: " + BitConverter.ToUInt32(new byte[] 
                { navMeshData[0] , navMeshData[1] , navMeshData[2], navMeshData[3]
                    } , 0));
                 Debug.Log("got navMeshData version2: " + BitConverter.ToUInt32(new byte[] 
                { navMeshData[0] , navMeshData[1] , navMeshData[2], navMeshData[3],
                  navMeshData[4] , navMeshData[5] , navMeshData[6], navMeshData[7]
                    } , 0));
                */

                if (mNavmeshData == null && navMeshData == null)
                {
                    Debug.Log("have not get a baked mesh , UnityAgentController shut down");
                    isSetup = false;
                }
                else
                {
                    //Debug.Log("starting NavManager now!");
                    NavManager.ActiveManager = CreateManager();
                    //Debug.Log("NavManager.ActiveManager  == "+ NavManager.ActiveManager + " after  CreateManager()");
                    mManager = NavManager.ActiveManager;
                    mManager.theModeNow = this.theAgentMode;
                    mManager.StartThreadUpdate();
                    isSetup = true;
                    //Debug.Log("Navigation Agent controller started!!!!!");
                }
            }
            catch(Exception X)
            {
                Debug.LogError("UnityAgentController Start with error:" + X.Message + "--" + X.ToString());
                isSetup = false;
            }
        }

        public void Update()
        {
            if (isSetup && mManager != null)
                mManager.MakeUpdate();
        }

        public  void MakeEnd()
        {
            if (mManager != null)
                mManager.StopThreadUpdate();
            else
                Debug.Log("mManager == null!");
        }

    }
}
