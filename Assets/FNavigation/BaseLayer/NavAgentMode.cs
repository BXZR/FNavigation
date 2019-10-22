//正式使用的寻路名字空间为FNavigation
namespace FNavigation
{
    //导航的算法切换标识（可以参考F_OperateMode）
    //如果需要扩展底层寻路的方针，需要扩展这里的标识并进行切换
    public enum NavAgentMode : byte
    {
        //简单寻路，这种事最基本的寻路，但是多个agent的情况下会有重叠的现象发生
        SimpleMove,
        //Crowd寻路，这个是更加复杂一点的寻路，多个agent不会发生重叠
        CrowdMove,
        //跟踪一个目标的Crowd寻路
        FollowGoalCrowdMove

    }
}