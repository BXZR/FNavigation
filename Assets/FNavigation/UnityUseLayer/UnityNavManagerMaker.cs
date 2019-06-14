using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using org.critterai.nav.u3d;
using org.critterai.nav;

namespace FNavigation
{
    public class UnityNavManagerMaker : MonoBehaviour
    {
        [SerializeField]
        private ScriptableObject mNavmeshData = null;

        [SerializeField]
        private AgentGroupSettings mGroupsSettings = null;

        [SerializeField]
        private CrowdAvoidanceSet mAvoidanceSet = null;

        [SerializeField]
        private int mMaxQueryNodes = 2048;

        [SerializeField]
        private int mMaxCrowdAgents = 20;

        [SerializeField]
        private float mMaxAgentRadius = 0.5f;

        [SerializeField]
        private int mMaxPath = 256;

        [SerializeField]
        private int mMaxStraightPath = 4;

        [SerializeField]
        private int mMaxAgents = 20;

        [SerializeField]
        private Vector3 mExtents = new Vector3(1, 1, 1);

        [SerializeField]
        private Vector3 mWideExtents = new Vector3(10, 10, 10);

        [SerializeField]
        private float mRadiusAt = 0.1f;

        [SerializeField]
        private float mRadiusNear = 1.0f;

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

        public NavManager CreateManager()
        {
            if (!(mNavmeshData && NavmeshData.HasNavmesh))
            {
                Debug.LogError(name + ": Aborted initialization. Navigation mesh not available.");
                return null;
            }

            if (!mAvoidanceSet || !mGroupsSettings)
            {
                Debug.LogError(
                    name + ": Aborted initialization. Avoidance and/or agent groups not available.");
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

            int count = mGroupsSettings.GroupCount;
            Dictionary<byte, NavAgentGroups> mAgentGroups = new Dictionary<byte, NavAgentGroups>(count);

            for (int i = 0; i < count; i++)
            {
                byte groupId;
                NavAgentGroups group = mGroupsSettings.CreateAgentGroup(i, mMaxPath, mMaxStraightPath, out groupId);
                group.angleAt = mAngleAt;
                group.heightTolerance = mHeightTolerance;
                group.radiusAt = mRadiusAt;
                group.radiusNear = mRadiusNear;
                group.turnThreshold = mTurnThreshold;

                mAgentGroups.Add(groupId, group);
            }
            return NavManager.Create(mMaxAgents, mGroup, mAgentGroups);
        }
    }
}
