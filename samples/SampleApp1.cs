#:property TargetFramework=net10.0
#:property LangVersion=14.0
#:property PublishAot=false
#:project ..\src\AppleDust\AppleDust.csproj

using AppleDust;

var builder = new BenchmarkBuilder();

// The first one is the baseline

// builder.Add(new Random(1).Next, "new Random(1).Next");
// builder.Add(new Random(2).Next, "new Random(2).Next");
// #pragma warning disable RS0030 // Do not use banned APIs
// builder.Add(Random.Shared.Next, "Random.Shared.Next");
// #pragma warning restore RS0030 // Do not use banned APIs
// builder.Add(() => null as object, "null");

builder.Add(Work, [100], "base");
builder.Add(Work, [20, 98, 99, 100, 101, 102, 200]);
builder.UseOverhead(() => Work(0));

await builder.RunAsync(args).ConfigureAwait(false);

static object? Work(int it)
{
    for (int i = 0; i < it * 10; i++)
    {
    }
    return null;
}
