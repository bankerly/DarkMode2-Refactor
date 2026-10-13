# 代码签名（SignPath Foundation）

本项目的 Windows 可执行文件由 [SignPath Foundation](https://signpath.org/) 免费签名——用于开源项目的代码签名服务，
签名后文件带有可验证的发布者信息，用户不会再看到"未知发布者 / 无法验证发布者"的提示。

工作流：[.github/workflows/release-signed.yml](../.github/workflows/release-signed.yml)

## 为什么是 SignPath
- 免费（面向 OSS 项目），不需要自己买 OV 证书（$150–300/年）
- 私钥在 SignPath 侧，不需要硬件令牌；签名在 CI 里完成
- SignPath 会校验"产物确实由本仓库的 GitHub workflow 构建"，用户可追溯来源

## 需要人工完成的准备（一次性）
1. **申请**：在 https://signpath.org/apply 提交项目（填写本仓库地址、许可证 GPL-3.0、说明项目用途）。
2. **安装 GitHub App**：SignPath 审核通过后，按邮件指引安装 SignPath GitHub App，并授权访问本仓库。
   然后在该 Organization 里启用预置的 Trusted Build System **GitHub.com**。
3. **建项目与策略**（SignPath 后台）：
   - Project slug：`darkmode2-refactor`
   - Signing policy slug：`release-signing`（可另建 `test-signing` 用于试签）
   - 把该项目链接到 GitHub.com trusted build system
4. **建 Artifact Configuration**（告诉 SignPath 该签哪些文件），根元素必须是 `zip-file`，因为 `actions/upload-artifact` 默认打包成 zip：
   ```xml
   <artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
     <zip-file>
       <pe-file path="DarkMode.Ui.exe" />
       <pe-file path="DarkMode.Ui.dll" />
       <pe-file path="DarkMode.Core.dll" />
     </zip-file>
   </artifact-configuration>
   ```
   在项目里把它设为**默认** artifact configuration（或把 slug 填到 workflow 的 `artifact-configuration-slug`）。
5. **建 API Token**（SignPath 后台 → API tokens，权限选 submitter）→ 写入仓库 Secrets：
   | Secret | 值 |
   |---|---|
   | `SIGNPATH_API_TOKEN` | 上一步生成的 token |
   | `SIGNPATH_ORGANIZATION_ID` | 后台 Organization 页显示的 ID |
   路径：仓库 → Settings → Secrets and variables → Actions → New repository secret

## 怎么出带签名的版本
- 推一个 tag：`git push fork v2.2.3` → workflow 自动构建 + 签名 + 建 Release（附件名 `DarkMode2-<版本>-win-x64-signed.zip`）
- 或在 Actions 页面手动 `Run workflow`，填版本号

## 校验签名（本机）
```powershell
# 需要 Windows SDK 的 signtool
signtool verify /pa /v .\DarkMode.Ui.exe
# 或：右键 exe → 属性 → 数字签名，应看到发布者不再是"未知发布者"
```

## 注意事项
- SignPath **要求 OSS 项目的所有 job 都跑在 GitHub 托管的 runner** 上（`windows-latest` ✓），不要在签名前插入自托管 job。
- 产物必须**先上传为 workflow artifact** 再提交签名请求（workflow 里已按此顺序）。
- 若在 SignPath 侧改用了非 zip 的 artifact configuration，记得把 `upload-artifact` 的 `archive` 设为 `false`。
- 证书签发给"项目"而非个人；仓库改名/转移后需要到 SignPath 后台更新项目设置。
- 未完成上述准备前，该 workflow 会因缺少 secrets 失败——这是预期行为，原有的 `alpha_publish.yml` 不受影响。
