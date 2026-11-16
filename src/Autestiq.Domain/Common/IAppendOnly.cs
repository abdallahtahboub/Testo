namespace Autestiq.Domain.Common;

/// <summary>
/// A record that is written once and never changed or deleted: the audit log, the usage ledger. If one is wrong,
/// another is written that says so. Enforced when saving.
/// </summary>
public interface IAppendOnly;
