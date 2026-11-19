using AIKernel.Hosting;
using System.Text;

string root=Path.Combine(Path.GetTempPath(),"aikernel-host-validation-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string journal=Path.Combine(root,"budget.journal");
    var budget=new DurableTokenBudget(journal,100,2);
    var first=budget.Reserve(80);
    ExpectFailure(()=>new DurableTokenBudget(journal,100,2).Reserve(30));
    budget.Settle(first,10,10);
    budget.Settle(first,0,0); // A duplicate settlement must never refund a second time.
    ExpectFailure(()=>budget.Reserve(81));
    if(!budget.CanReserve(80)||budget.CanReserve(81))throw new Exception("Availability mismatch.");
    string second=new DurableTokenBudget(journal,100,2).Reserve(80);
    budget.Settle(second,null,null);
    if(budget.CanReserve())throw new Exception("Exhausted budget advertised as available.");
    ExpectFailure(()=>new DurableTokenBudget(journal,100,2).Reserve(1));
    Console.WriteLine("Restart, daily limit, lifetime limit, duplicate settlement and unknown usage: passed");
    string race=Path.Combine(root,"race.journal");int accepted=0;
    Parallel.For(0,16,_=>{try{new DurableTokenBudget(race,1000,1).Reserve(1);Interlocked.Increment(ref accepted);}catch(IOException){}catch(InvalidOperationException){}});
    if(accepted!=1)throw new Exception("Concurrent budget over-admitted.");
    Console.WriteLine("Concurrent reservation: exactly one accepted");
    File.AppendAllText(journal,"incomplete");if(budget.CanReserve())throw new Exception("Corrupt budget advertised as available.");ExpectFailure(()=>budget.Reserve(1));
    string corrupt=Path.Combine(root,"corrupt.journal");var broken=new DurableTokenBudget(corrupt,100,2);broken.Reserve(1);
    byte[] bytes=File.ReadAllBytes(corrupt);bytes[0]=bytes[0]==(byte)'0'?(byte)'1':(byte)'0';File.WriteAllBytes(corrupt,bytes);
    ExpectFailure(()=>broken.Reserve(1));
    Console.WriteLine("Truncated and corrupt journal: fail closed");
    string web=Path.Combine(root,"www");Directory.CreateDirectory(web);
    string secret=Path.Combine(root,"secret.txt");File.WriteAllText(secret,"test-placeholder-credential");
    if(!OperatingSystem.IsWindows())File.SetUnixFileMode(secret,UnixFileMode.UserRead|UnixFileMode.UserWrite);
    var provider=new PrivateFileCredentialProvider(new Dictionary<string,string>{{"allowed",secret}},web);
    if(await provider.GetSecretAsync("allowed")!="test-placeholder-credential")throw new Exception("Secret mismatch.");
    await ExpectAsyncFailure(async()=>{await provider.GetSecretAsync("unknown");});
    string publicSecret=Path.Combine(web,"secret.txt");File.Copy(secret,publicSecret);
    var unsafeProvider=new PrivateFileCredentialProvider(new Dictionary<string,string>{{"public",publicSecret}},web);
    await ExpectAsyncFailure(async()=>{await unsafeProvider.GetSecretAsync("public");});
    if(!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(secret,UnixFileMode.UserRead|UnixFileMode.GroupRead);
        await ExpectAsyncFailure(async()=>{await provider.GetSecretAsync("allowed");});
    }
    Console.WriteLine("Secret allowlist, private location and file permissions: passed");
}
finally{Directory.Delete(root,true);}
static void ExpectFailure(Action action){try{action();}catch(Exception e)when(e is InvalidOperationException or IOException or InvalidDataException or FormatException){return;}throw new Exception("Expected rejection.");}
static async Task ExpectAsyncFailure(Func<Task> action){try{await action();}catch(InvalidOperationException){return;}throw new Exception("Expected secret rejection.");}
