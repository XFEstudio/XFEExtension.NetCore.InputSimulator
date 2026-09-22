# UMDF HID 可行性验证

这是保留用于复现 UMDF 可行性验证的最小包。主库现已采用完整 UMDF 驱动，普通使用者无需安装此探针。微软支持由用户态 UMDF HID minidriver 配合系统自带 `MsHidUmdf.sys` / `WUDFRd.sys` 工作，见 [WDF HID minidriver 架构](https://learn.microsoft.com/en-us/windows-hardware/drivers/wdf/creating-umdf-hid-minidrivers)。用户态驱动与新内核驱动的签名要求不同，但设备安装包仍有签名要求，见 [微软签名说明](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/windows-driver-signing-tutorial)。

## 验证范围

此包只测试 Windows 11 x64 能否加载本地签名的 UMDF 驱动、枚举键盘/鼠标/控制集合，并回答一个只读 HID Feature 请求。驱动没有生成按键或移动的接口；客户端不会发送输入。本机已在 Secure Boot 开启时验证成功，完整输入传输已接入主库；游戏兼容性仍未验证。

## 构建与本地签名

在仓库根目录使用 PowerShell 7：

```powershell
pwsh -File .\driver\umdf-probe\Build-Probe.ps1
pwsh -File .\driver\umdf-probe\Sign-Probe.ps1
```

输出分别为 `artifacts/umdf-probe`、`artifacts/umdf-probe-signed`。签名脚本在当前用户个人证书存储生成一个有效期 30 天、仅用于代码签名的非 CA 证书，私钥不可导出。只导出公开 `.cer`，不改系统信任、不安装设备。该签名不属于微软、WHQL 或 Attestation 签名。

`probe.json` 保存签名者指纹、公开证书 SHA-256、包文件哈希与未验证状态。已经签名的目录不会被重复签名命令覆盖。原生 DLL、客户端、安装器静态链接 C 运行库，只依赖 Windows 自带组件。

## 安装验证

以下操作要求管理员权限，并明确同意将这个专用签名者加入本机 `Root` 和 `TrustedPublisher` 存储。这会信任该证书签名的代码。核对签名脚本显示的公开证书 SHA-256，将它填入 `$fingerprint` 后，在管理员 PowerShell 中执行：

```powershell
cd .\artifacts\umdf-probe-signed
$fingerprint = '填写签名脚本输出的64位证书SHA256'
.\Install-Probe.ps1 -ExpectedCertificateSha256 $fingerprint -TrustLocalDevelopmentCertificate
```

安装脚本先核对包哈希与明确指定的证书，再导入信任、核验文件签名并注册 `Root\XFEInputUmProbe`。签名或安装失败时撤回本次新增的证书信任。若设备安装成功但运行验证失败，会保留现场供诊断，可执行下方脚本移除。

脚本不关闭 Secure Boot、不修改签名强制策略、不设置测试模式、不自动重启。系统仍可因自身安全策略拒绝加载；安装成功也不等于实际加载成功。状态查询必须得到 `UMDF probe loaded` 才证明 Feature 通道工作。未安装时客户端返回 `ERROR_NOT_FOUND`（1168）。

## 移除验证设备

在管理员 PowerShell 中使用同一指纹：

```powershell
.\Remove-Probe.ps1 -ExpectedCertificateSha256 $fingerprint
# 如果主库也使用同一证书，请保留信任：
.\Remove-Probe.ps1 -ExpectedCertificateSha256 $fingerprint -KeepCertificateTrust
```

仅移除硬件 ID 完全匹配的验证设备。Windows 拒绝直接移除时，核对包的发布者及所有引用后卸载探针专用 OEM 包，再清理设备节点。`-KeepCertificateTrust` 保留证书信任；发现主驱动存在时必须使用此选项。构建用户个人存储中的签名私钥始终保留，不删除其他驱动或证书。此验证设备使用独立硬件 ID，不修改已有物理键鼠驱动。
