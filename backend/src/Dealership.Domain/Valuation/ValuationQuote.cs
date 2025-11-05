using Dealership.Domain.Common;

namespace Dealership.Domain.Valuation;

public sealed record ValuationQuote(Money MinValuation, Money MaxValuation);
