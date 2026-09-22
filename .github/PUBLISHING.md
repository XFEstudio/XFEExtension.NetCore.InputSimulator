# 构建与发布

工作流参考 `XFEExtension.NetCore.ServerInteractive` 的 `Validate` 和 `Publish Packages`，使用 Windows runner 和 .NET 10 SDK。输入模拟库与测试项目均以 `net10.0` 为目标框架。

## 自动验证

`validate.yml` 在以下情况运行：向 `master` / `main` 推送、创建或更新 Pull Request、从 Actions 页面手动触发。

先还原固定版本的微软 WDK/SDK NuGet 工具，编译项目内的 UMDF HID 驱动和安装器，运行原生报告及会话测试、INF 校验及目录生成，再内嵌开发负载、构建 .NET 10 项目、运行 `XFEExtension.NetCore.XUnit` 测试并打包主库。测试使用 `dotnet run`，返回失败退出码或未发现测试用例时会阻止后续打包。工作流保存 JSON / JUnit XML、未签名驱动及开发 `.nupkg`，不会发布包。

`unsigned-driver` 产物包含 UMDF DLL、INF、CAT、PDB 及安装器。CI 不使用签名私钥、不生成或提交微软审核 CAB。签名及后续处理见 [驱动签名说明](../driver/SIGNING.md)。

开发 NuGet 包默认带 `-driver-dev` 后缀。完整驱动文件作为资源嵌入 DLL；它不会自动安装未签名负载。CI 不安装驱动或修改 runner 的证书信任与启动安全策略。

自动测试使用记录后端，不向 runner 的桌面发送真实输入。Windows CI 使用默认的 x64 .NET 主机；本地可按 README 中的命令额外运行 x86 测试。

## 发布凭据

发布前，当前 ref 必须包含 `driver/signed/win-x64` 下使用同一公共代码签名证书签好的 `XfeInputDriver.dll`、`XfeInput.cat`、`XfeInputSetup.exe`，以及 CAT 覆盖的 `XfeInput.inf`。工作流检查证书用途、非自签名、有效签名和 DLL/INF 目录成员哈希；不再检查项目 SYS 的内核签名，也不要求提交微软硬件审核。公钥证书从签名中提取并内嵌，私钥不得入库。

当前 `-driver-local` 自签名包供用户明确授权信任后安装。维护者首次运行 `driver/New-SigningCertificate.ps1` 创建十年专用证书，再通过 `Build.ps1 -LocalSigning` 复用身份、签名并请求 RFC 3161 时间戳；也支持显式指定 `-LocalCertificateThumbprint '<指纹>'`。私钥不进入工作流或包。公共发布工作流不会将它误发布为公共受信任版本，也不会在 runner 上导入本地 Root 证书。

签名是维护者的发布步骤。调用方只需要 DLL/NuGet，首次使用由内嵌安装器申请管理员授权和所需的签名者信任；无需自己签名、下载驱动或配置 WDK。`driver/signed/win-x64` 中只保存发行二进制，不保存私钥。

在 GitHub 仓库的 Settings → Secrets and variables → Actions 中配置：

| Secret | 用途 |
| --- | --- |
| `NUGET_API_KEY` | 发布至 NuGet.org，需具有 `XFEExtension.NetCore.InputSimulator` 的推送权限。仅在手动跳过 NuGet.org 时可以不配置。 |
| `GH_PACKAGES_TOKEN` | 可选；需要自定义 GitHub Packages 身份时使用。默认使用内置 `GITHUB_TOKEN`，发布 job 已请求 `packages: write`。 |

创建 GitHub Release 使用内置 `GITHUB_TOKEN` 和 `contents: write` 权限。已有 GitHub Package 必须允许当前仓库写入，否则需调整包的 Actions 访问权限或配置合适的 `GH_PACKAGES_TOKEN`。

## 标签发布

1. 确认代码和验证结果已准备好。
2. 推送版本标签，例如 `v4.0.0` 或 `v4.1.0-preview.1`。
3. `publish-packages.yml` 从标签去掉 `v` 得到包版本，执行构建、全部单元测试、打包。
4. 测试通过后，上传包文件并发布至 GitHub Packages 和 NuGet.org。
5. 为原始标签创建 GitHub Release 并附加 `.nupkg`。带 `-` 的版本标记为预发布。

发布构建显式设置 `Version` 和 `PackageVersion`，不会修改仓库内的项目文件。包与 Release 版本来自同一次运行；例如 `v4.0.0` 对应 `4.0.0` 包，Release 使用已有的 `v4.0.0` 标签。驱动自身的 INF 版本独立维护，签名后的文件不能再被改写。

## 手动发布

在 Actions → Publish Packages → Run workflow 中选择待发布的 ref，填写不带 `v` 的 `version`。支持三段或四段数字版本，以及 `-preview.1` 形式的预发布后缀。

`skip_nuget_org=true` 时仅发布至 GitHub Packages。手动运行不创建 GitHub Release，避免产生与当前 ref 无关的版本标签。工作流使用 `--skip-duplicate` 处理已发布的相同版本，不覆盖现有 NuGet 包。

## 本地验证发布步骤

在仓库根目录执行：

```powershell
pwsh -File .\Build.ps1 -ReleasePackage
dotnet restore .\XFEExtension.NetCore.InputSimulator.sln
dotnet build .\XFEExtension.NetCore.InputSimulator.sln -c Release --no-restore -p:GeneratePackageOnBuild=false -p:ContinuousIntegrationBuild=true -warnaserror
dotnet run --project .\XFEExtension.NetCore.InputSimulator.Tests -c Release --no-build --no-restore -- --artifacts artifacts/tests
dotnet pack .\XFEExtension.NetCore.InputSimulator\XFEExtension.NetCore.InputSimulator.csproj -c Release --no-build --no-restore --output artifacts/packages -p:GeneratePackageOnBuild=false -p:ContinuousIntegrationBuild=true
```

仅进行开发验证时使用 `pwsh -File .\Build.ps1`，生成内嵌未签名驱动的开发包。程序集强名称签名与驱动包 Authenticode 签名是两回事；默认程序集不强名称签名，工作流不读取仓库历史 PFX 或其他私钥。
