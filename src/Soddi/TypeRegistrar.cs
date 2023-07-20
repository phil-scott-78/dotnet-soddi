using Microsoft.Extensions.DependencyInjection;

namespace Soddi;

public sealed class TypeRegistrar(IServiceCollection builder) : ITypeRegistrar, IDisposable
{
    private IServiceCollection Services { get; } = builder;
    private IList<IDisposable> BuiltProviders { get; } = new List<IDisposable>();

    public ITypeResolver Build()
    {
        var buildServiceProvider = Services.BuildServiceProvider();
        BuiltProviders.Add(buildServiceProvider);
        return new TypeResolver(buildServiceProvider);
    }

    public void Register(Type service, Type implementation)
    {
        Services.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        Services.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> func)
    {
        if (func is null)
        {
            throw new ArgumentNullException(nameof(func));
        }

        Services.AddSingleton(service, (_) => func());
    }

    public void Dispose()
    {
        foreach (var provider in BuiltProviders)
        {
            provider.Dispose();
        }
    }
}

public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver
{
    private readonly IServiceProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public object? Resolve(Type? type)
    {
        return type != null ? _provider.GetRequiredService(type) : null;
    }
}