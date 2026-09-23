# UMDF 驱动签名与分发

本项目提供用户态 UMDF HID minidriver，内核桥接使用 Windows 自带的 `MsHidUmdf.sys` 和 `WUDFRd.sys`。这是 Windows 的 [WDF HID minidriver 架构](https://learn.microsoft.com/en-us/windows-hardware/drivers/wdf/creating-umdf-hid-minidrivers)，不是通过应用级 SendInput 注入，也不包含项目自有的内核 SYS。

驱动包仍需签名。**签名由发布者完成一次，使用者不需要申请证书、微软账号或重复签名。** 当前实现可以用自签名代码证书，在目标电脑明确授权信任后安装；不走微软硬件提交流程。它不等于微软认证或默认受所有 Windows 电脑信任，企业设备安装策略或应用控制策略仍可能阻止安装。用户态和内核态签名要求的区别见[微软签名说明](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/windows-driver-signing-tutorial)。

## 使用者安装

1. 引用发布者签好的 DLL/NuGet，或运行随包提供的控制台。
2. 第一次创建 `InputController` 时，库找不到设备便从 DLL 提取驱动及安装器，请求 Windows UAC 管理员安装授权。
3. 如果签名者尚未被信任，安装器显示证书名称及 SHA-256 指纹，用户确认后才加入本机信任。自签名代码证书加入 `Root` 和 `TrustedPublisher`；已有公共证书链信任时只按需加入 `TrustedPublisher`。
4. 安装成功后，程序可用普通权限连接，不再为每次输入请求管理员权限。系统要求重启时会明确报告，不自动重启。

可在应用安装阶段主动调用 `DriverDeployment.InstallEmbeddedDriver()`。DLL 内嵌驱动 DLL、INF、CAT、原生安装器、**公开**证书及 SHA-256 清单；不包含私钥。不要求调用方安装 WDK、Visual Studio、C++ 运行库或第三方输入驱动。调用方自己的 .NET 10 应用仍需运行时或自包含发布。

## Git 中的驱动包

`driver/payload/win-x64` 中的 `XfeInputDriver.dll`、`XfeInput.inf`、`XfeInput.cat`、`XfeInputSetup.exe`、公开证书 `publisher.cer` 和两份元数据 `payload.json`、`payload.props` 均纳入 Git。克隆后直接执行 `dotnet build` 会嵌入这份已签名负载，不需要签名私钥或原生编译环境。驱动版本独立于托管库版本，当前内置驱动为 4.0.1.0。

`.gitattributes` 禁止对签名文件（包括受 CAT 覆盖的 INF）转换换行或自动文本合并。修改原生驱动、安装器或 INF 后，需要重新构建、签名并运行 `Prepare-Payload.ps1`，再将全部 7 个文件作为一组更新。默认 `Build.ps1` 会生成未签名开发负载，不应将其作为已签名驱动包提交。构建目录、`driver/signing.local.json` 和 PFX 私钥继续忽略。

## 本地签名构建

维护者使用专门的非 CA、仅代码签名用途证书，私钥保留在维护者电脑或签名服务。不要在 NuGet、Git 或用户安装包中分发 PFX/私钥。

首次创建项目专用证书，然后复用签名身份构建：

```powershell
pwsh -File .\driver\New-SigningCertificate.ps1
pwsh -File .\Build.ps1 -LocalSigning
dotnet run --project .\XFEExtension.NetCore.InputSimulator.Tests -c Release --no-build -- --artifacts artifacts/tests
dotnet pack .\XFEExtension.NetCore.InputSimulator -c Release --no-build -o .\artifacts\packages
```

证书默认有效期十年，RSA 4096 / SHA-256，非 CA、仅代码签名用途。选择信息写入被 Git 忽略的 `driver/signing.local.json`，私钥保存在 Windows `CurrentUser\My`。重复执行证书脚本会复用已有证书，不会为每次构建更换身份。`-ValidityYears` 可在首次创建时指定 1～20 年；已有其他证书时仍可用 `Build.ps1 -LocalCertificateThumbprint '<指纹>'`。

需要连同自包含控制台一起分发时，执行 `pwsh -File .\Publish-Local.ps1`。脚本复用本机签名身份，构建驱动和库、执行测试、打包 NuGet，再为控制台签名及时间戳，输出到新的 `artifacts/distribution-*` 目录；不上传或发布到外部服务。

构建脚本先编译驱动，签名及时间戳 `XfeInputDriver.dll`，再生成覆盖该 DLL 和 INF 的目录，最后签名及时间戳 CAT 和 `XfeInputSetup.exe`。时间戳使用 [DigiCert RFC 3161 服务](https://knowledge.digicert.com/general-information/rfc3161-compliant-time-stamp-authority-server)，仅发送签名摘要，不上传私钥或完整程序。时间戳失败会终止构建。`Build.ps1` 不创建证书、导入系统信任或安装驱动；签名机需预先信任发布者证书以完成打包校验。

也可分别执行：

```powershell
pwsh -File .\driver\Build-Driver.ps1
pwsh -File .\driver\Sign-LocalDriver.ps1 -CertificateThumbprint '<证书指纹>' -OutputDirectory .\artifacts\driver-local-new
pwsh -File .\driver\Prepare-Payload.ps1 -PackageDirectory .\artifacts\driver-local-new -AllowSelfSigned -RequireTimestamp
dotnet build .\XFEExtension.NetCore.InputSimulator.sln -c Release -p:GeneratePackageOnBuild=false -warnaserror
```

签名输出目录须为空，以免覆盖既有签名包。自签名包标记为 `self-signed`，NuGet 版本在托管库版本后附加 `-driver-local`。它可供明确同意信任的用户安装，属于本地信任分发，不是公共受信任发行版。`-AllowLocalDevelopment` 仍是 `-AllowSelfSigned` 的兼容别名。

## 证书有效期与更新

长期证书为 `CN=XFEstudio InputSimulator Code Signing`，到期时间为 **2036-09-23 03:19:42（UTC+8）**。证书 SHA-256：`B9ACC9FC6FB187B50B9F600389D72FF00534EA5391BD2BE1972C7B2A93BD2A60`。原来的 30 天探针证书仅留作旧包回退，后续版本使用长期身份。通过 `DriverDeployment.EmbeddedDriverCertificateExpires`、`IsEmbeddedDriverTimestamped` 可检查包信息。

这不是“永久签名”。RFC 3161 时间戳用于证明签名时间；Windows 依据签名、时间戳及本机策略验证代码，详见 [SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool)。**当前安装器仍要求发布者证书在安装时有效**，即本包的新安装期限仍为 2036 年；时间戳不会跳过这项检查。已安装设备后续是否加载也由 Windows 策略决定。到期前应发布新证书签名的更新，更换证书时用户需要确认新身份，不能修改旧证书或复用旧 CAT。

已安装 30 天验证版的机器应运行新版控制台 `InputSimulator.Console.exe --driver-install`，或显式调用 `DriverDeployment.InstallEmbeddedDriver()`，以更新驱动和信任。仅执行 `--driver-check` 或普通连接会使用已经存在的兼容设备，不能证明安装包已更新。

## 私钥备份与迁移

长期证书允许**加密导出**，构建过程不导出私钥。建议在本机 PowerShell 手动备份到加密的离线介质；密码通过本地隐藏输入，不发送到聊天或写进脚本：

```powershell
$profile = Get-Content .\driver\signing.local.json -Raw | ConvertFrom-Json
$certificate = Get-Item "Cert:\CurrentUser\My\$($profile.thumbprint)"
$password = Read-Host '输入备份密码' -AsSecureString
Export-PfxCertificate -Cert $certificate -FilePath 'E:\PrivateBackup\InputSimulator-signing.pfx' -Password $password -CryptoAlgorithmOption AES256_SHA256
```

路径按自己的离线介质修改，备份文件和密码分开保管；仓库已忽略 `*.pfx`。迁移机器时用 `Import-PfxCertificate` 将加密备份导入新用户的 `My` 存储，并恢复证书选择文件。缺失选择文件时也可直接指定原证书指纹构建。若私钥丢失且没有备份，将无法继续使用同一身份签名；若泄露，应停止分发该证书签名的包，更新签名身份并通知使用者移除旧信任。

## 公共发行及 CI

当前公开发布工作流只接受在干净 Windows runner 上可验证、非自签名的代码签名发行包；不是要求微软审核项目自有内核驱动。将使用同一发布者证书签好的 DLL、INF、CAT、安装器放入 `driver/signed/win-x64`，执行 `Build.ps1 -ReleasePackage`。公开证书从签名中提取并内嵌，打包前检查签名、证书用途和目录成员哈希。默认开发 CI 仅产出 `-driver-dev` 未签名包，自动安装会拒绝该包。

自签名信任版本目前通过上面的本地命令构建分发，不由公共发布工作流上传。长期自签名发行需要维护证书、更新通道和信任说明；不能将“驱动能在本机运行”等同于“任意机器或游戏都会接受”。

## 卸载

管理员可在管理员 PowerShell 运行仓库中的 `driver/Remove-Driver.ps1`。脚本只移除硬件 ID 为 `Root\XFEInput` 的设备；如果 Windows 拒绝直接移除活动 HID 设备，会核对发布者及包引用，使用 PnPUtil 卸载仅该设备使用的 OEM 包。保留证书信任，系统要求重启时仅提示。需要完全移除证书时，在本机证书管理器按安装时显示的指纹查找，仅在确认没有其他包依赖该证书后从 `Root` 和 `TrustedPublisher` 移除。

验证探针使用独立的 `Root\XFEInputUmProbe`。探针与主驱动共享证书时，应使用 `Remove-Probe.ps1 -KeepCertificateTrust`，避免移除主驱动所需的信任。
