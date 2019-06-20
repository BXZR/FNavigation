using org.critterai.nav;
using org.critterai.nav.u3d;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FNavigation
{
    //这个AgentController的功能比较全，可以切simpe进而crowd两种模式
    public class UnityAgentController : MonoBehaviour
    {

        public NavAgentMode theAgentMode = NavAgentMode.SimpleMove;

        [SerializeField]
        private ScriptableObject mNavmeshData = null;

        [SerializeField]
        private CrowdAvoidanceSet mAvoidanceSet = null;

        [SerializeField]
        private int mMaxQueryNodes = 2048;

        [SerializeField]
        private int mMaxCrowdAgents = 200;

        [SerializeField]
        private float mMaxAgentRadius = 0.5f;

        [SerializeField]
        private int mMaxPath = 256;

        [SerializeField]
        private int mMaxStraightPath = 4;

        [SerializeField]
        private int mMaxAgents = 200;

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
            if (!(mNavmeshData && NavmeshData.HasNavmesh))
            {
                Debug.LogError(name + ": Aborted initialization. Navigation mesh not available.");
                return null;
            }

            Navmesh navmesh = NavmeshData.GetNavmesh();
            NavmeshQuery query;
            NavStatus status = NavmeshQuery.Create(navmesh, mMaxQueryNodes, out query);
            if ((status & NavStatus.Sucess) == 0)
            {
                Debug.LogError(
                    name + ": Aborted initialization. Failed query creation: " + status.ToString());
                return null;
            }

            CrowdManager crowd = CrowdManager.Create(mMaxCrowdAgents, mMaxAgentRadius, navmesh);
            if (crowd == null)
            {
                Debug.LogError(name + ": Aborted initialization. Failed crowd creation.");
                return null;
            }

            for (int i = 0; i < CrowdManager.MaxAvoidanceParams; i++)
            {
                crowd.SetAvoidanceConfig(i, mAvoidanceSet[i]);
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

        void Awake()
        {
            NavManager.ActiveManager = CreateManager(); 
            mManager = NavManager.ActiveManager;
            mManager.theModeNow = this.theAgentMode;
            mManager.StartThreadUpdate();
        }

        void Update()
        {
            mManager.MakeUpdate();
        }

        private void OnDestroy()
        {
            mManager.StopThreadUpdate();
        }
    }
}
