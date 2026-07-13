using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PclOfflineLogin;

/// <summary>
/// 在 WPF 路由事件进入 Nex 的新建按钮前接管点击，复用 Nex 自带的登录选项和页面。
/// </summary>
internal static class CreateProfileUnlockPatch
{
    private static Window? _window;
    private static MouseButtonEventHandler? _mouseHandler;
    private static Action<string>? _logInfo;
    private static bool _handling;

    internal static void Apply(Action<string> logInfo)
    {
        if (_window is not null)
            return;

        _window = Application.Current?.MainWindow
            ?? throw new InvalidOperationException("PCL 主窗口尚未创建。");
        _logInfo = logInfo;
        _mouseHandler = OnPreviewMouseLeftButtonDown;
        _window.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, _mouseHandler, true);
        logInfo("档案创建选项事件拦截器已注册。");
    }

    internal static void Remove(Action<string> logInfo, Action<string, Exception?> logWarn)
    {
        try
        {
            if (_window is not null && _mouseHandler is not null)
            {
                _window.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, _mouseHandler);
                logInfo("档案创建选项事件拦截器已移除。");
            }
        }
        catch (Exception ex)
        {
            logWarn("移除档案创建选项事件拦截器失败。", ex);
        }
        finally
        {
            _mouseHandler = null;
            _logInfo = null;
            _window = null;
            _handling = false;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_handling || !IsNewProfileButton(e.OriginalSource as DependencyObject))
            return;

        e.Handled = true;
        _handling = true;
        try
        {
            ShowUnlockedProfileSelection();
        }
        finally
        {
            _handling = false;
        }
    }

    private static bool IsNewProfileButton(DependencyObject? source)
    {
        for (var current = source; current is not null; current = GetParent(current))
        {
            if (current is FrameworkElement { Name: "BtnNew" } element &&
                string.Equals(element.GetType().FullName, "PCL.MyIconButton", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is FrameworkContentElement contentElement)
            return contentElement.Parent;
        return VisualTreeHelper.GetParent(current);
    }

    private static void ShowUnlockedProfileSelection()
    {
        var modProfile = FindType("PCL.ModProfile")
            ?? throw new InvalidOperationException("未找到 PCL.ModProfile。");
        var modMain = FindType("PCL.ModMain")
            ?? throw new InvalidOperationException("未找到 PCL.ModMain。");
        var mcLoginType = FindType("PCL.ModLaunch+McLoginType")
            ?? throw new InvalidOperationException("未找到 McLoginType。");

        var getSelection = modProfile.GetMethod(
            "_GetAvailableProfileSelection",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(bool)],
            null)
            ?? throw new MissingMethodException(modProfile.FullName, "_GetAvailableProfileSelection(Boolean)");
        var selection = getSelection.Invoke(null, [true])
            ?? throw new InvalidOperationException("Nex 未返回登录选项。");

        var msgBox = modMain.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "MyMsgBoxSelect" && method.GetParameters().Length >= 4);
        var parameters = msgBox.GetParameters();
        var arguments = new object?[parameters.Length];
        arguments[0] = selection;
        arguments[1] = GetLocalizedText("Launch.Account.Profile.Create.SelectAuthType.Title");
        arguments[2] = GetLocalizedText("Common.Action.Continue");
        arguments[3] = GetLocalizedText("Common.Action.Cancel");
        for (var index = 4; index < arguments.Length; index++)
            arguments[index] = parameters[index].HasDefaultValue ? parameters[index].DefaultValue : null;

        var result = msgBox.Invoke(null, arguments);
        if (result is not int selected)
            return;

        modProfile.GetField("isCreatingProfile", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?.SetValue(null, true);
        var launchLeft = modMain.GetField("frmLaunchLeft", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null)
            ?? throw new InvalidOperationException("启动页尚未初始化。");
        var refreshPage = launchLeft.GetType().GetMethod(
            "RefreshPage",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            [typeof(bool), mcLoginType],
            null)
            ?? throw new MissingMethodException(launchLeft.GetType().FullName, "RefreshPage");

        var loginType = selected switch
        {
            0 => "Ms",
            1 => "Auth",
            _ => "Legacy"
        };
        refreshPage.Invoke(launchLeft, [true, Enum.Parse(mcLoginType, loginType)]);
        if (loginType == "Legacy" && _window is not null)
            _window.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                RenameStandardUuidOption);
    }

    private static void RenameStandardUuidOption()
    {
        var modMain = FindType("PCL.ModMain");
        var offlinePage = modMain?.GetField(
            "frmLoginOffline",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        if (offlinePage is not FrameworkElement page)
        {
            _logInfo?.Invoke("未找到离线登录页面，未修改 UUID 选项文字。");
            return;
        }

        var radio = page.FindName("RadioUuidStandard");
        var textProperty = radio?.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
        if (radio is null || textProperty is null)
        {
            _logInfo?.Invoke("未找到 RadioUuidStandard，未修改 UUID 选项文字。");
            return;
        }

        textProperty.SetValue(radio, "一般");
        _logInfo?.Invoke("离线登录 UUID 选项文字已改为“一般”。");
    }

    private static string GetLocalizedText(string key)
    {
        var lang = FindType("PCL.Core.App.Localization.Lang")
            ?? throw new InvalidOperationException("未找到本地化服务。");
        return (string?)lang.GetMethod(
            "Text",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(string)],
            null)?.Invoke(null, [key]) ?? key;
    }

    private static Type? FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName, false, false);
            if (type is not null)
                return type;
        }

        return null;
    }
}
