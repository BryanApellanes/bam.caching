# Cache Size Calculation

## Resolution

The original concern about `BinaryFormatter` is no longer applicable.

The `Serialized` class (`Bam.Serialized` in bam.base) uses **JSON serialization** via
`Serialization.Serialize(data, SerializationFormat.Json)`. It stores the serialized
bytes in a `byte[]` property (`Data`) and exposes `Size` as `Data.Length`.

`CacheItem.MemorySize` returns `_serialized.Size`, which is the length of the
JSON byte array — a reasonable proxy for in-memory size. No refactoring is needed.
