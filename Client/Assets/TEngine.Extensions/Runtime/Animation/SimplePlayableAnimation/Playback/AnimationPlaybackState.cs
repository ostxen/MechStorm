namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 描述动画节点处于停止、播放、暂停或完成阶段，供调用方检查播放流程。
    /// </summary>
    public enum AnimationPlaybackState
    {
        Stopped = 0,
        Playing = 1,
        Paused = 2,
        Completed = 3
    }
}
