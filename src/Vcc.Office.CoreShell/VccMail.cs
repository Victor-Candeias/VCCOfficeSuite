using System.Text.Json;

namespace Vcc.Office.CoreShell;

public enum MailFolder { Inbox, Drafts, Sent, Archive, Trash }
public sealed record MailAccount(string Id, string Address, string DisplayName, string ImapHost = "", string SmtpHost = "", bool Enabled = true);
public sealed record MailAttachment(string Id, string FileName, long Length, string ContentType, string LocalPath);
public sealed record MailMessage(string Id, string AccountId, string From, IReadOnlyList<string> To, string Subject, string Body, MailFolder Folder = MailFolder.Drafts, bool IsRead = false, IReadOnlyList<MailAttachment>? Attachments = null, DateTimeOffset? Date = null);

public static class MailPolicy
{
    public static ValidationResult Validate(MailAccount account) { var errors = new List<string>(); if (string.IsNullOrWhiteSpace(account.Id)) errors.Add("A conta requer ID."); if (!IsAddress(account.Address)) errors.Add("O endereço da conta não é válido."); if (account.Enabled && (string.IsNullOrWhiteSpace(account.ImapHost) || string.IsNullOrWhiteSpace(account.SmtpHost))) errors.Add("Uma conta ativa requer servidores IMAP e SMTP."); return new ValidationResult(errors); }
    public static ValidationResult Validate(MailMessage message) { var errors = new List<string>(); if (string.IsNullOrWhiteSpace(message.Id)) errors.Add("A mensagem requer ID."); if (!IsAddress(message.From)) errors.Add("O remetente não é válido."); if (message.To.Count == 0 || message.To.Any(item => !IsAddress(item))) errors.Add("A mensagem requer destinatários válidos."); return new ValidationResult(errors); }
    private static bool IsAddress(string value) => !string.IsNullOrWhiteSpace(value) && value.Contains('@', StringComparison.Ordinal) && value.IndexOf('@') > 0 && value.IndexOf('@') < value.Length - 1;
}

public sealed class VccMailService
{
    private readonly AtomicFileStore _files = new(); private readonly string? _statePath; private readonly List<MailAccount> _accounts = []; private readonly List<MailMessage> _messages = [];
    public VccMailService(string? statePath = null) => _statePath = statePath;
    public IReadOnlyList<MailAccount> Accounts => _accounts.ToArray(); public IReadOnlyList<MailMessage> Messages => _messages.ToArray();
    public async Task<MailAccount> ConfigureAccountAsync(MailAccount account, CancellationToken cancellationToken = default) { var validation = MailPolicy.Validate(account); if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors)); if (_accounts.Any(item => item.Id.Equals(account.Id, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe uma conta com esse ID."); _accounts.Add(account); await PersistAsync(cancellationToken); return account; }
    public async Task<MailMessage> ComposeAsync(MailMessage message, CancellationToken cancellationToken = default) { var validation = MailPolicy.Validate(message); if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors)); if (!_accounts.Any(item => item.Id.Equals(message.AccountId, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("A conta da mensagem não existe."); var next = message with { Folder = MailFolder.Drafts, Date = message.Date ?? DateTimeOffset.UtcNow, Attachments = message.Attachments ?? [] }; _messages.Add(next); await PersistAsync(cancellationToken); return next; }
    public async Task<MailMessage> SendAsync(string id, CancellationToken cancellationToken = default) { var index = Find(id); var message = _messages[index]; _messages[index] = message with { Folder = MailFolder.Sent, Date = DateTimeOffset.UtcNow }; await PersistAsync(cancellationToken); return _messages[index]; }
    public async Task<MailMessage> MoveAsync(string id, MailFolder folder, CancellationToken cancellationToken = default) { var index = Find(id); _messages[index] = _messages[index] with { Folder = folder }; await PersistAsync(cancellationToken); return _messages[index]; }
    public IReadOnlyList<MailMessage> Search(string? text = null, MailFolder? folder = null) => _messages.Where(item => (!folder.HasValue || item.Folder == folder) && (string.IsNullOrWhiteSpace(text) || item.Subject.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase) || item.Body.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase) || item.From.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase))).ToArray();
    public MailMessage Open(string id) { var index = Find(id); _messages[index] = _messages[index] with { IsRead = true }; return _messages[index]; }
    public async Task<MailMessage> AddAttachmentAsync(string id, MailAttachment attachment, CancellationToken cancellationToken = default) { if (attachment.Length < 0 || string.IsNullOrWhiteSpace(attachment.LocalPath)) throw new ArgumentException("O anexo não é válido."); var index = Find(id); _messages[index] = _messages[index] with { Attachments = (_messages[index].Attachments ?? []).Append(attachment).ToArray() }; await PersistAsync(cancellationToken); return _messages[index]; }
    public async Task SaveAsync(CancellationToken cancellationToken = default) => await PersistAsync(cancellationToken);
    public async Task LoadAsync(CancellationToken cancellationToken = default) { if (string.IsNullOrWhiteSpace(_statePath)) throw new InvalidOperationException("Este serviço não foi configurado com um caminho de estado."); if (!File.Exists(_statePath)) return; var state = JsonSerializer.Deserialize<MailState>(await File.ReadAllTextAsync(_statePath, cancellationToken)) ?? throw new InvalidDataException("O estado de Mail é inválido."); foreach (var account in state.Accounts) { var result = MailPolicy.Validate(account); if (!result.IsValid) throw new InvalidDataException(string.Join(" ", result.Errors)); } foreach (var message in state.Messages) { var result = MailPolicy.Validate(message); if (!result.IsValid) throw new InvalidDataException(string.Join(" ", result.Errors)); } _accounts.Clear(); _accounts.AddRange(state.Accounts); _messages.Clear(); _messages.AddRange(state.Messages); }
    private int Find(string id) { var index = _messages.FindIndex(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)); return index >= 0 ? index : throw new ArgumentException("A mensagem não existe.", nameof(id)); }
    private async Task PersistAsync(CancellationToken cancellationToken) { if (string.IsNullOrWhiteSpace(_statePath)) return; await _files.WriteAsync(_statePath, JsonSerializer.SerializeToUtf8Bytes(new MailState(_accounts, _messages), new JsonSerializerOptions { WriteIndented = true }), cancellationToken); }
    private sealed record MailState(IReadOnlyList<MailAccount> Accounts, IReadOnlyList<MailMessage> Messages);
}
