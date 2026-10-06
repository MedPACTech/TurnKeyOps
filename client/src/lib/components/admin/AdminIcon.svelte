<script lang="ts">
  import { LayoutDashboard, CalendarDays, HardHat, FileText, Receipt, Users, ShieldCheck, Inbox, Globe, Sparkles, Settings, CircleCheck, TriangleAlert, Clock, CircleDollarSign, Search, ListChecks, MapPin, Wrench, Phone, Trees, Logs, Truck, Tractor, Layers, Building2 } from 'lucide-svelte';
  let { name, size = 20 }: { name: string; size?: number } = $props();
  const icons = { dashboard: LayoutDashboard, calendar: CalendarDays, jobs: HardHat, estimates: FileText, invoices: Receipt, customers: Users, users: ShieldCheck, requests: Inbox, content: Globe, bob: Sparkles, settings: Settings, check: CircleCheck, warning: TriangleAlert, clock: Clock, money: CircleDollarSign, search: Search, tasks: ListChecks, location: MapPin, tools: Wrench, phone: Phone, trees: Trees, wood: Logs, truck: Truck, tractor: Tractor, layers: Layers, building: Building2 };
  const Icon = $derived(icons[name as keyof typeof icons] ?? ListChecks);
</script>
<Icon {size} strokeWidth={1.75} aria-hidden="true" />
