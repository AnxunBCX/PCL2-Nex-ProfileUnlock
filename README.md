# 档案创建解锁

适用于 PCL Nex 插件 API 1.1.0 的独立插件。

## 作用

PCL Nex 在联网且尚未存在微软正版档案时，会把「新建档案」弹窗限制为仅显示正版登录。
本插件在运行时解锁该 UI 分支，使新建档案始终显示：

- 正版（微软）
- 第三方（含 Little Skin 等 Authlib-Injector）
- 离线

插件会复用 Nex 自带的离线登录页与第三方/Little Skin 登录页，不再自建登录界面。

## 不会做什么

- 不修改 Nex 源码，所有代码仅位于本插件目录。
- 不伪造微软正版认证，也不把离线/Little Skin 冒充成正版。
- 不绕过实例级「要求正版 / 要求第三方」启动校验。
- 不绕过正版服务器的真实鉴权。

## 安装

1. 使用 `dotnet build -c Release` 生成发布包 `cn.pclnex.offline-login.pclx`。
2. 在 PCL Nex 中直接安装该 `.pclx` 插件包，或双击打开（需已关联插件包扩展名）。
3. 也可手动解压到 `Plugins/cn.pclnex.offline-login/`，确认目录中至少包含：
   - `plugin.json`
   - `PclOfflineLogin.dll`
4. 重启 PCL Nex。

## 技术说明

插件在 PCL 主窗口注册可卸载的 WPF 预览鼠标事件，仅拦截账户页中名为 `BtnNew`
的原生新建档案按钮。点击后通过反射调用 Nex 自带的
`PCL.ModProfile._GetAvailableProfileSelection(true)`，然后进入 Nex 原生登录页面。

该实现不修改 Nex 方法体、不使用 Harmony，也不会改写启动器文件。

若未来 Nex 重构该方法签名，插件加载时会报错并提示版本不兼容。
