using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIKernel.Hosting;

/// <summary>Single-host durable reservations. Incomplete journal records fail closed; unknown usage is not refunded.</summary>
public sealed class DurableTokenBudget
{
    private readonly string path;
    private readonly int dailyTokens,totalCalls;
    /// <summary>The private path is server configuration. Limits persist across process restarts.</summary>
    public DurableTokenBudget(string privatePath,int dailyTokenLimit,int lifetimeCallLimit)
    {
        if(dailyTokenLimit<1||lifetimeCallLimit<1)throw new ArgumentOutOfRangeException(nameof(dailyTokenLimit));
        path=Path.GetFullPath(privatePath);dailyTokens=dailyTokenLimit;totalCalls=lifetimeCallLimit;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }
    /// <summary>Checks availability without spending. The actual reservation still enforces limits atomically.</summary>
    public bool CanReserve(int tokens=1)
    {
        if(tokens<1||tokens>dailyTokens||tokens>2000000)return false;
        try
        {
            using var stream=Open();var state=Read(stream);
            string day=DateTime.UtcNow.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
            return state.Count<totalCalls&&state.Values.Where(r=>r.Day==day).Sum(r=>(long)r.Tokens)+tokens<=dailyTokens;
        }
        catch(Exception error) when(error is IOException or InvalidDataException or FormatException or JsonException or UnauthorizedAccessException){return false;}
    }
    /// <summary>Durably reserve before dispatch. The returned ID may be settled only once.</summary>
    public string Reserve(int tokens)
    {
        if(tokens<1||tokens>dailyTokens||tokens>2000000)throw new InvalidOperationException("Token budget exhausted.");
        using var stream=Open();var state=Read(stream);string day=DateTime.UtcNow.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
        if(state.Count>=totalCalls||state.Values.Where(r=>r.Day==day).Sum(r=>(long)r.Tokens)+tokens>dailyTokens)
            throw new InvalidOperationException("Token budget exhausted.");
        string id=Guid.NewGuid().ToString("N");
        Append(stream,new(){Id=id,Day=day,Tokens=tokens});return id;
    }
    /// <summary>Refund unused reservation only with authoritative, nonnegative usage. Missing usage stays charged.</summary>
    public void Settle(string id,int? inputTokens,int? outputTokens)
    {
        if(inputTokens is null||outputTokens is null)return;
        if(inputTokens<0||outputTokens<0||inputTokens>1000000||outputTokens>1000000)throw new ArgumentOutOfRangeException(nameof(inputTokens));
        using var stream=Open();var state=Read(stream);
        if(!state.TryGetValue(id,out var reservation)||reservation.Settled)return;
        Append(stream,new(){Id=id,Day=reservation.Day,Tokens=checked(inputTokens.Value+outputTokens.Value),Settled=true});
    }
    private FileStream Open()
    {
        var stream=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        if(stream.Length>1024*1024){stream.Dispose();throw new InvalidOperationException("Budget journal requires maintenance.");}
        return stream;
    }
    private static Dictionary<string,Entry> Read(FileStream stream)
    {
        using var reader=new StreamReader(stream,Encoding.UTF8,false,4096,leaveOpen:true);
        string text=reader.ReadToEnd();var state=new Dictionary<string,Entry>(StringComparer.Ordinal);
        if(text.Length>0&&!text.EndsWith('\n'))throw new InvalidDataException("Incomplete budget journal.");
        foreach(string line in text.Split('\n',StringSplitOptions.RemoveEmptyEntries))
        {
            if(line.Length<66||line[64]!='|')throw new InvalidDataException("Invalid budget journal.");
            var bytes=Convert.FromBase64String(line[65..]);
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(line[..64],StringComparison.Ordinal))throw new InvalidDataException("Budget journal checksum mismatch.");
            var entry=JsonSerializer.Deserialize<Entry>(bytes)??throw new InvalidDataException("Invalid budget entry.");
            if(!Guid.TryParseExact(entry.Id,"N",out _)||entry.Tokens<0||entry.Tokens>2000000 ||
                !DateTime.TryParseExact(entry.Day,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _))
                throw new InvalidDataException("Invalid budget entry.");
            if(entry.Settled)
            {
                if(!state.TryGetValue(entry.Id,out var original)||original.Settled||original.Day!=entry.Day)
                    throw new InvalidDataException("Invalid budget settlement.");
                state[entry.Id]=entry;
            }
            else if(!state.TryAdd(entry.Id,entry))throw new InvalidDataException("Repeated reservation.");
        }
        return state;
    }
    private static void Append(FileStream stream,Entry entry)
    {
        byte[] data=JsonSerializer.SerializeToUtf8Bytes(entry);
        byte[] record=Encoding.ASCII.GetBytes(Convert.ToHexString(SHA256.HashData(data))+"|"+Convert.ToBase64String(data)+"\n");
        stream.Position=stream.Length;stream.Write(record);stream.Flush(true);
    }
    private sealed class Entry {public string Id {get;set;}="";public string Day {get;set;}="";public int Tokens {get;set;}public bool Settled {get;set;}}
}
