namespace Worktime.Domain.Tenants;

/// <summary>Billing drives the transitions (Fase 2+): PendingPayment → Active ⇄ PastDue → Canceled.</summary>
public enum TenantStatus { PendingPayment, Active, PastDue, Canceled }
