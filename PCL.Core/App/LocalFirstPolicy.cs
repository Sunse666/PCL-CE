namespace PCL.Core.App;

/// <summary>
/// 本地优先策略。
/// 用于切断一切由云端内容驱动本地行为的通道，相关能力在此处统一收口，
/// 避免在各个调用点散落判断。当前构建下这些能力被强制关闭，且不提供用户开关。
/// </summary>
public static class LocalFirstPolicy
{
    /// <summary>
    /// 启动器自动更新（含手动检查、公告内更新引导、整合包导出时获取最新版）。
    /// </summary>
    public static readonly bool AllowUpdate = false;

    /// <summary>
    /// 云端公告。公告内容可携带命令、下载、写配置等本地动作，属于高风险通道。
    /// </summary>
    public static readonly bool AllowAnnouncement = false;

    /// <summary>
    /// 遥测与联机数据上报。
    /// </summary>
    public static readonly bool AllowTelemetry = false;

    /// <summary>
    /// 由远端内容（公告按钮等）触发的本地动作，如执行命令、下载文件、写配置、安装整合包。
    /// 该动作表同时服务于用户自行编写的自定义主页，因此不能整体删除，只能按来源拒绝远端调用。
    /// </summary>
    public static readonly bool AllowRemoteActions = false;

    /// <summary>
    /// 下载源迁移版本。递增此值可让存量用户重新应用一次默认下载源。
    /// </summary>
    private const int DownloadSourcePolicyVersion = 1;

    /// <summary>
    /// 将存量用户的下载源迁移为官方源。
    /// 仅在首次执行时生效，之后用户在设置中自行选择的镜像源会被保留。
    /// </summary>
    public static void ApplyDownloadSourceDefaults()
    {
        if (States.System.DownloadSourcePolicyVersion >= DownloadSourcePolicyVersion) return;

        Config.Download.FileSource = 2;
        Config.Download.VersionListSource = 2;
        Config.Download.Comp.CompSourceSolution = 2;
        States.System.DownloadSourcePolicyVersion = DownloadSourcePolicyVersion;
    }
}
