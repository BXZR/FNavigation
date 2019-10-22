using org.critterai.nav;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FNavigation
{
    public class AgentGroupSettingManager
    {
        private static byte[] mGroup = { 0, 1, 2, 3, 4, 5 };

        public static int GetGroupCount()
        {
            return mGroup.Length;
        }

        public static  NavAgentGroups CreateAgentGroup(int index, int maxPath, int maxStraightPath, out byte group)
        {
            GroupSettings aSetting = new GroupSettings();
            CrowdAgentParams cap = new CrowdAgentParams();
            cap.collisionQueryRange = aSetting.colisionQueryRange;
            cap.height = aSetting.height;
            cap.maxAcceleration = aSetting.maxAcceleration;
            cap.maxSpeed = aSetting.maxSpeed;
            cap.pathOptimizationRange = aSetting.pathOptimizationRange;
            cap.radius = aSetting.radius;
            cap.separationWeight = aSetting.separationWeight;
            cap.updateFlags = aSetting.updateFlags;
            NavAgentGroups aGroup = new NavAgentGroups(cap , maxPath , maxStraightPath ,
                aSetting.maxPoolCorridors , aSetting.maxPoolPaths);

            aGroup.maxTurnSpeed = aSetting.maxTurnSpeed;

            group = mGroup[index];
            return aGroup;
        }

    }

    class GroupSettings
    {
        public int GroupCount = 3;
        public float colisionQueryRange = 3.2f;
        public float height = 5f;
        public float maxAcceleration = 8f;
        public float maxSpeed = 5.5f;
        public float maxTurnSpeed = 8f;
        public float pathOptimizationRange = 8f;
        public float radius = 0.4f;
        public float separationWeight = 2f;
        public int maxPoolCorridors = 100;
        public int maxPoolPaths = 100;
        public CrowdUpdateFlags updateFlags = CrowdUpdateFlags.AnticipateTurns;
    }
}
