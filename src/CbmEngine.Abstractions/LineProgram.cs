namespace CbmEngine.Abstractions;

public readonly record struct MemoryWrite(ushort Address, byte Value);

public sealed class LineProgram
{
    private readonly Dictionary<int, IReadOnlyList<MemoryWrite>> _byLine;

    public LineProgram(IDictionary<int, IReadOnlyList<MemoryWrite>> byLine)
    {
        ArgumentNullException.ThrowIfNull(byLine);
        _byLine = new Dictionary<int, IReadOnlyList<MemoryWrite>>(byLine);
    }

    public bool TryGet(int line, out IReadOnlyList<MemoryWrite> writes) => _byLine.TryGetValue(line, out writes!);

    public int Count => _byLine.Count;

    public IEnumerable<int> Lines => _byLine.Keys;

    public sealed class Builder
    {
        private readonly Dictionary<int, List<MemoryWrite>> _data = new();

        public Builder At(int line, ushort address, byte value)
        {
            if (!_data.TryGetValue(line, out var list))
                _data[line] = list = new List<MemoryWrite>();
            list.Add(new MemoryWrite(address, value));
            return this;
        }

        public Builder At(int line, IEnumerable<MemoryWrite> writes)
        {
            if (!_data.TryGetValue(line, out var list))
                _data[line] = list = new List<MemoryWrite>();
            list.AddRange(writes);
            return this;
        }

        public LineProgram Build()
        {
            var dict = new Dictionary<int, IReadOnlyList<MemoryWrite>>();
            foreach (var kv in _data) dict[kv.Key] = kv.Value.AsReadOnly();
            return new LineProgram(dict);
        }
    }
}
