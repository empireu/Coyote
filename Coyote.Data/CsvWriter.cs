namespace Coyote.Data;

public readonly struct CsvHeader
{
    public string Name { get; }

    public CsvHeader(string name)
    {
        Name = name;
    }

    public static implicit operator CsvHeader(string source)
    {
        return new CsvHeader(source);
    }

    public override string ToString()
    {
        return Name;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not CsvHeader other)
        {
            return false;
        }

        return Equals(other);
    }

    public bool Equals(CsvHeader other)
    {
        return Name.Equals(other.Name);
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }

    public static bool operator ==(CsvHeader a, CsvHeader b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(CsvHeader a, CsvHeader b)
    {
        return !a.Equals(b);
    }
}

public readonly struct CsvEntry
{
    public CsvHeader Header { get; }
    public string Value { get; }

    public CsvEntry(CsvHeader header, string value)
    {
        Header = header;
        Value = value;
    }

    public static implicit operator CsvEntry((CsvHeader, string) source)
    {
        return new CsvEntry(source.Item1, source.Item2);
    }

    public static implicit operator CsvEntry((CsvHeader, object) source)
    {
        return new CsvEntry(source.Item1, source.Item2.ToString());
    }

    public static implicit operator CsvEntry((string, string) source)
    {
        return new CsvEntry(new CsvHeader(source.Item1), source.Item2);
    }

    public static implicit operator CsvEntry((string, object) source)
    {
        return new CsvEntry(new CsvHeader(source.Item1), source.Item2.ToString());
    }

    public override string ToString()
    {
        return $"{Header}: {Value}";
    }
}

public sealed class CsvWriter : IDisposable
{
    public StreamWriter Writer { get; }

    private readonly Dictionary<CsvHeader, int> _indices = new();
    private readonly List<CsvEntry> _submittedData = new();

    public CsvWriter(StreamWriter writer, params CsvHeader[] headers)
    {
        if (headers.Length == 0)
        {
            throw new ArgumentException("Empty headers", nameof(headers));
        }

        Writer = writer;

        for (var index = 0; index < headers.Length; index++)
        {
            var header = headers[index];

            if (!_indices.TryAdd(header, index))
            {
                throw new ArgumentException($"Duplicate header {header}", nameof(headers));
            }

            writer.Write(header);

            if (index < headers.Length - 1)
            {
                writer.Write(',');
            }
        }

        writer.WriteLine();
    }

    public void Add(CsvEntry entry)
    {
        if (!_indices.ContainsKey(entry.Header))
        {
            throw new ArgumentException($"Invalid entry {entry}", nameof(entry));
        }

        if (_submittedData.Any(x => x.Header == entry.Header))
        {
            throw new InvalidOperationException($"Data for {entry.Header} already submitted.");
        }

        _submittedData.Add(entry);
    }

    public void Add(CsvHeader header, object value)
    {
        Add((header, value));
    }

    public void Flush()
    {
        if (_submittedData.Count != _indices.Count)
        {
            throw new InvalidOperationException("Did not submit all required data");
        }

        _submittedData.Sort((a, b) => _indices[a.Header]
            .CompareTo(_indices[b.Header]));

        for (var index = 0; index < _submittedData.Count; index++)
        {
            var csvEntry = _submittedData[index];
          
            Writer.Write(csvEntry.Value);

            if (index < _submittedData.Count - 1)
            {
                Writer.Write(',');
            }
        }

        Writer.WriteLine();

        _submittedData.Clear();
    }

    public void Dispose()
    {
        Writer.Flush();
        Writer.Dispose();
    }
}