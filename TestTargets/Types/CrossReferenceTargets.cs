namespace ILSpy.Mcp.TestTargets.CrossRef;

/// <summary>
/// Interface for cross-reference testing — find_implementors target.
/// </summary>
public interface IRepository
{
    void Save(string data);
    string Load(int id);
}

/// <summary>
/// Direct implementor of IRepository.
/// </summary>
public class FileRepository : IRepository
{
    private readonly string _basePath;

    public FileRepository(string basePath)
    {
        _basePath = basePath;
    }

    public void Save(string data)
    {
        // Simulates file save
        _ = _basePath + "/" + data;
    }

    public string Load(int id)
    {
        return $"file-data-{id}";
    }
}

/// <summary>
/// Another direct implementor of IRepository.
/// </summary>
public class DatabaseRepository : IRepository
{
    public void Save(string data)
    {
        // Simulates DB save
        _ = data.Length;
    }

    public string Load(int id)
    {
        return $"db-data-{id}";
    }
}

/// <summary>
/// Extends FileRepository (indirect implementor of IRepository).
/// </summary>
public class CachedFileRepository : FileRepository
{
    private readonly Dictionary<int, string> _cache = new();

    public CachedFileRepository(string basePath) : base(basePath) { }

    public new string Load(int id)
    {
        if (_cache.TryGetValue(id, out var cached))
            return cached;

        var result = base.Load(id);
        _cache[id] = result;
        return result;
    }
}

/// <summary>
/// Caller class — uses IRepository methods, creates instances.
/// Used to test find_usages, find_dependencies, and find_instantiations.
/// </summary>
public class DataService
{
    private readonly IRepository _repo;

    public DataService()
    {
        // Instantiation of FileRepository — find_instantiations target
        _repo = new FileRepository("/data");
    }

    public void ProcessData(string input)
    {
        // Usage of IRepository.Save — find_usages target
        _repo.Save(input);
    }

    public string GetData(int id)
    {
        // Usage of IRepository.Load — find_usages target
        return _repo.Load(id);
    }

    public static DataService CreateWithDatabase()
    {
        // Another instantiation pattern
        var service = new DataService();
        return service;
    }
}

/// <summary>
/// Another caller that instantiates FileRepository directly.
/// </summary>
public class FileProcessor
{
    public void Process()
    {
        // Direct instantiation — find_instantiations target
        var repo = new FileRepository("/tmp");
        repo.Save("processed");

        // Also creates a DatabaseRepository
        var dbRepo = new DatabaseRepository();
        dbRepo.Save("db-processed");
    }
}
