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

builder.Add(() => Wait(100), "base");
builder.Add(() => Wait(20));
builder.Add(() => Wait(97));
builder.Add(() => Wait(98));
builder.Add(() => Wait(99));
builder.Add(() => Wait(100));
builder.Add(() => Wait(101));
builder.Add(() => Wait(102));
builder.Add(() => Wait(103));
builder.Add(() => Wait(200));
builder.UseOverhead(() => Wait(0));

await builder.RunAsync(args).ConfigureAwait(false);

static object? Wait(int it)
{
    for (int i = 0; i < it; i++)
    {
        // do nothing
    }
    return null;
}
