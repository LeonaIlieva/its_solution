using Claims.Domain;

namespace Claims.Controllers.Requests;

public sealed record CreateCoverRequest(DateTime StartDate, DateTime EndDate, CoverType Type);
