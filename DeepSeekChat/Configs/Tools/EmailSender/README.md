# EmailSender 邮件发送工具

一个基于.NET 8.0的命令行邮件发送工具，支持发送带附件的邮件。

## 命令行用法

```bash
EmailSender [选项]
```

## 选项说明

| 选项 | 长选项 | 说明 |
|------|--------|------|
| `-r` | `--recipient` | 收件人邮箱地址（必需） |
| `-s` | `--subject` | 邮件主题（必需） |
| `-b` | `--body` | 邮件正文（必需） |
| `-a` | `--attachment` | 附件文件路径（可选，可多次指定以添加多个附件） |

## 示例

### 示例1：发送简单邮件
发送一封只有主题和正文的邮件：

```bash
EmailSender -r recipient@example.com -s "测试邮件" -b "这是一封测试邮件"
```

### 示例2：发送带单个附件的邮件
发送一封带有一个附件的邮件：

```bash
EmailSender -r recipient@example.com -s "带附件的邮件" -b "请查收附件" -a "C:\path\to\file.pdf"
```

### 示例3：发送带多个附件的邮件
发送一封带有多个附件的邮件：

```bash
EmailSender -r recipient@example.com -s "多附件邮件" -b "包含多个文件" -a "file1.pdf" -a "file2.docx" -a "file3.jpg"
```

### 示例4：使用长选项
使用长选项格式发送邮件：

```bash
EmailSender --recipient recipient@example.com --subject "重要通知" --body "请及时处理" --attachment "report.pdf"
```

## 返回值

程序返回以下退出代码：

| 退出代码 | 含义 | 说明 |
|----------|------|------|
| 0 | 成功 | 邮件发送成功 |
| 1 | 参数错误 | 命令行参数验证失败 |
| 2 | 配置错误 | 配置文件加载或验证失败 |
| 3 | 发送失败 | 邮件发送过程中发生错误 |

## 配置文件

程序需要配置文件 `mail_config.json` 来设置SMTP服务器信息。该文件必须位于可执行文件同级目录。

### 配置文件示例
请参考 `mail_config.example.json` 文件创建您的配置文件：

```json
{
  "SmtpServer": "smtp.example.com",
  "SmtpPort": 587,
  "SenderEmail": "your-email@example.com",
  "SenderPassword": "your-password-or-auth-code",
  "EnableSsl": true,
  "SenderName": "Your Name"
}
```

### 配置项说明
- **SmtpServer**: SMTP服务器地址（如：smtp.gmail.com, smtp.qq.com等）
- **SmtpPort**: SMTP端口号（通常为587或465）
- **SenderEmail**: 发件人邮箱地址
- **SenderPassword**: 发件人密码或授权码（建议使用应用专用密码）
- **EnableSsl**: 是否启用SSL/TLS加密（建议为true）
- **SenderName**: 发件人显示名称（可选）

## 依赖项

- .NET 8.0 Runtime
- MailKit 4.7.0
- MimeKit 4.7.0
- System.CommandLine 2.0.0-beta4

## 日志记录

程序会自动记录发送操作到 `email_sender.log` 文件，日志文件位于可执行文件同级目录。日志包含以下信息：
- 时间戳
- 收件人邮箱
- 邮件主题
- 发送状态（成功/失败）
- 错误信息（如果发送失败）

## 注意事项

1. 确保配置文件中的SMTP服务器信息正确
2. 对于Gmail等邮箱，可能需要使用应用专用密码而非普通密码
3. 附件文件路径可以是绝对路径或相对路径
4. 程序支持发送纯文本邮件，暂不支持HTML格式
5. 建议在发送前测试配置文件是否正确