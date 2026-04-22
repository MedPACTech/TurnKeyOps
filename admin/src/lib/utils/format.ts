/** Formatting helpers — keep it simple for contractors on the go. */

export function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 0,
    maximumFractionDigits: 2
  }).format(amount);
}

export function formatDate(dateStr: string | undefined): string {
  if (!dateStr) return '—';
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short', day: 'numeric', year: 'numeric'
  });
}

export function formatDateTime(dateStr: string | undefined): string {
  if (!dateStr) return '—';
  return new Date(dateStr).toLocaleString('en-US', {
    month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit'
  });
}

export function formatPhone(phone: string | undefined): string {
  if (!phone) return '—';
  const digits = phone.replace(/\D/g, '');
  if (digits.length === 10) {
    return `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6)}`;
  }
  return phone;
}

export function statusColor(status: string): string {
  const map: Record<string, string> = {
    Created: 'badge-gray', Lead: 'badge-gray', Estimated: 'badge-blue', Scheduled: 'badge-blue', InProgress: 'badge-yellow',
    OnHold: 'badge-yellow', Completed: 'badge-green', Closed: 'badge-gray', Cancelled: 'badge-red', Paid: 'badge-green',
    Draft: 'badge-gray', Submitted: 'badge-blue', UnderReview: 'badge-yellow', Revised: 'badge-blue', Awarded: 'badge-green',
    ConvertedToJob: 'badge-green', Sent: 'badge-blue', Accepted: 'badge-green',
    Rejected: 'badge-red', Declined: 'badge-red', Expired: 'badge-red', Overdue: 'badge-red', Void: 'badge-gray', Invoiced: 'badge-blue'
  };
  return map[status] ?? 'badge-gray';
}

export function initials(firstName?: string, lastName?: string): string {
  return ((firstName?.[0] ?? '') + (lastName?.[0] ?? '')).toUpperCase() || '?';
}
