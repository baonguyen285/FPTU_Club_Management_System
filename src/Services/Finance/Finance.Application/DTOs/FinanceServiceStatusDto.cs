namespace Finance.Application.DTOs;

public sealed record FinanceServiceStatusDto(
    string Service,
    string Version,
    bool DatabaseConfigured);
