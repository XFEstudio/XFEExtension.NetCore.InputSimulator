# 构建与发布

工作流参考 `XFEExtension.NetCore.ServerInteractive` 的 `Validate` 和 `Publish Packages`，使用 Windows runner、PowerShell 7 和 .NET 10 SDK。输入模拟库与测试项目均以 `net10.0` 为目标框架。两个工作流都复用 Git 中的已签名驱动，不需要 Visual Studio、C++ Build Tools、vswhere、WDK 或签名私钥；自建运行机也无需安装这些原生构建工具。

## 自动验证

`validate.yml` 在以下情况运行：向 `master` / `main` 推送、创建或更新 Pull Request、从 Actions 页面手动触发。

先执行 `driver/Test-Payload.ps1` 检查已提交的完整驱动负载及元数据，再直接构建 .NET 10 项目、运行 `XFEExtension.NetCore.XUnit` 测试并打包主库。测试使用 `dotnet run`，返回失败退出码或未发现测试用例时会阻止后续打包。工作流保存 JSON / JUnit XML、已签名驱动及 `.nupkg`，不会发布包。

`signed-driver-payload` 产物包含 Git 中的全部 7 个负载文件：UMDF DLL、INF、CAT、安装器、公开证书及两份元数据。CI 不重编译驱动、不生成或替换签名文件。签名及后续处理见 [驱动签名说明](../driver/SIGNING.md)。

当前自签名负载在普通验证构建中默认生成 `-driver-local` NuGet 包。发布工作流使用标签或手动输入的版本，驱动在包内仍标记为 `self-signed`，不会因上传 NuGet.org 就获得公共证书信任。CI 不安装驱动或修改 runner 的证书信任与启动安全策略。

自动测试使用记录后端，不向 runner 的桌面发送真实输入。Windows CI 使用默认的 x64 .NET 主机；本地可按 README 中的命令额外运行 x86 测试。

## 发布凭据

发布前，当前 ref 必须包含 `driver/payload/win-x64` 下的完整已签名负载。工作流检查 SHA-256、签名类型和时间戳声明、公开证书用途及有效期、JSON 与 MSBuild 元数据一致性；未签名、缺失或被改动的文件会使工作流失败。

CI 的仓库完整性检查不替代 Authenticode 信任验证或 CAT 成员验证。维护者首次运行 `driver/New-SigningCertificate.ps1` 创建长期专用证书，再通过 `Build.ps1 -LocalSigning` 编译、运行原生测试、签名和校验，然后提交更新后的整套负载。也支持显式指定 `-LocalCertificateThumbprint '<指纹>'`。私钥不进入工作流或包；自签名证书不需要被 runner 信任。

签名是维护者的发布步骤。调用方只需要 DLL/NuGet，使用 `DriverDeployment.CheckDriver()` 检测设备，在显式调用 `InstallEmbeddedDriver()` 时由内嵌安装器申请管理员授权和所需的签名者信任。连接和输入均不会自动安装；无需自己签名、下载驱动或配置 WDK。

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
pwsh -File .\driver\Test-Payload.ps1
dotnet restore .\XFEExtension.NetCore.InputSimulator.sln
dotnet build .\XFEExtension.NetCore.InputSimulator.sln -c Release --no-restore -p:GeneratePackageOnBuild=false -p:ContinuousIntegrationBuild=true -warnaserror
dotnet run --project .\XFEExtension.NetCore.InputSimulator.Tests -c Release --no-build --no-restore -- --artifacts artifacts/tests
dotnet pack .\XFEExtension.NetCore.InputSimulator\XFEExtension.NetCore.InputSimulator.csproj -c Release --no-build --no-restore --output artifacts/packages -p:GeneratePackageOnBuild=false -p:ContinuousIntegrationBuild=true
```

只有需要修改原生驱动时才运行 `pwsh -File .\Build.ps1`，此路径需要 C++ 环境，并生成内嵌未签名驱动的开发包；重新签名后才能提交负载。复用仓库驱动时使用上面的命令，不调用 `Build-Driver.ps1` 或 `Prepare-Payload.ps1`。程序集强名称签名与驱动包 Authenticode 签名是两回事；默认程序集不强名称签名，工作流不读取仓库历史 PFX 或其他私钥。
