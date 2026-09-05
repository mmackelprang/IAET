namespace Iaet.DukeEnergy.Models;

/// <summary>
/// Result of resolving a Duke Energy account from a phone number.
/// </summary>
/// <param name="Found">Whether the lookup matched an account.</param>
/// <param name="AccountNumber">The matched account number, when found.</param>
/// <param name="ServiceAddress">The service address on the account, when reported.</param>
/// <param name="Fields">Every field extracted from the response by the endpoint profile.</param>
public sealed record AccountLookupResult(
    bool Found,
    string? AccountNumber,
    string? ServiceAddress,
    IReadOnlyDictionary<string, string?> Fields);
