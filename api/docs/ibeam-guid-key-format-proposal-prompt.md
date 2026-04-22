# IBeam Proposal Prompt: Global GUID Key Format + Mapping/Read Key Unification

## Prompt
We use `IBeam.Repositories.AzureTables` in a multi-service codebase and need key generation consistency across:
- `WriteKey` mapping delegates
- id locator behavior
- repository read APIs (`GetById`, `GetByKeys`)
- app-side key helpers

Please propose and implement an IBeam enhancement with these requirements:

1. Add a **global, configurable GUID key format** option:
- New option on Azure Tables configuration, e.g. `GuidKeyFormat` with allowed values `"N"` and `"D"`.
- Default should be `"N"`.
- This format must be applied consistently anywhere IBeam stringifies GUIDs for keys.

2. Centralize key formatting:
- Introduce a single formatter abstraction used by mapping writes and id-locator internals.
- Example: `IAzureEntityKeyFormatter` or equivalent.
- Avoid duplicated GUID formatting logic in different code paths.

3. Improve service/repository ergonomics:
- Add or standardize `GetByIdAsync(Guid id, ...)` support when `EnableIdLocator = true`, so callers do not need to pass `partitionKey` + `rowKey` in common flows.
- Keep existing key-based APIs for backward compatibility.

4. Expose mapping key resolution for consumers:
- Provide a DI-resolvable component that can compute keys using registered mapping rules (same logic as `WriteKey`).
- This allows app services to avoid custom string-building and guarantees parity with mapping.

5. Backward compatibility and migration:
- Support opt-in migration from existing `"D"` key datasets.
- Document migration behavior and provide guidance for mixed existing data.

## Acceptance criteria
- Changing `GuidKeyFormat` to `"N"` or `"D"` changes all generated GUID keys globally and consistently.
- Read/write/id-locator paths remain aligned under both formats.
- Existing consumers using explicit keys continue to work.
- New `GetByIdAsync` and mapping-key resolver APIs are documented with examples.

## Suggested API sketch
```csharp
services.ConfigureIBeamAzureTables(o =>
{
    o.ConnectionString = "...";
    o.GuidKeyFormat = "N"; // or "D"
});
```

```csharp
public interface IAzureEntityKeyResolver<T>
{
    AzureEntityKey ResolveWriteKey(Guid? tenantId, T entity);
}
```

```csharp
Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
```
