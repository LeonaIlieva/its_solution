using Claims.Domain;

namespace Claims.Controllers.Requests;

public sealed record CreateClaimRequest(string CoverId, DateTime Created, string Name, ClaimType Type, decimal DamageCost);
