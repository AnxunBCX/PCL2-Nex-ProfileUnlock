using System.Threading;
using System.Threading.Tasks;
using PCL.Plugin.Abstractions;

namespace PclOfflineLogin;

[Plugin(
    id: "cn.pclnex.offline-login",
    name: "档案创建解锁",
    version: "2.1.0.0",
    Author = "AnxunBCX",
    Description = "新建档案时始终显示正版、第三方与离线选项，无需先登录正版账号。",
    MinApiVersion = "1.1.0.0",
    Capabilities = PluginCapabilities.None,
    LoadTiming = PluginLoadTiming.WindowCreated)]
public sealed class LoginPlugin : PclPluginBase
{
    private IPluginContext? _context;

    public override Task LoadAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        base.LoadAsync(context, cancellationToken);
        _context = context;

        var logger = context.Host.Core.GetLogger("main");
        try
        {
            CreateProfileUnlockPatch.Apply(message => logger.Info(message));
            context.Host.Core.Hint("档案创建已解锁：无需先登录正版即可选择离线/第三方", PluginHintType.Success);
        }
        catch (Exception ex)
        {
            logger.Error("解锁新建档案选项失败。", ex);
            context.Host.Core.Hint($"档案创建解锁失败：{ex.Message}", PluginHintType.Error);
            throw;
        }

        return Task.CompletedTask;
    }

    public override Task UnloadAsync(CancellationToken cancellationToken = default)
    {
        if (_context is not null)
        {
            var logger = _context.Host.Core.GetLogger("main");
            CreateProfileUnlockPatch.Remove(
                message => logger.Info(message),
                (message, ex) =>
                {
                    if (ex is null) logger.Warn(message);
                    else logger.Warn(message, ex);
                });
        }

        _context = null;
        return base.UnloadAsync(cancellationToken);
    }
}
