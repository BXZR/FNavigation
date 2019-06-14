//正式使用的寻路名字空间为FNavigation
namespace FNavigation
{
    //一个用于统一计算处理的基类
    //这个基类的方法会被应用于planner和unitymover(也就是移动计划)
    public class NavState 
    {
        public static NavState Instance = new NavState();
        //移动计划开启的时候发生的事情
        public virtual bool Enter() { return true; }
        //移动的时候发生的事情
        public virtual bool Update() { return true; }
        //移动计划停止的时候发生的事情
        public virtual void Exit() { }
    }
}
