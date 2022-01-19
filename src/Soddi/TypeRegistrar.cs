using Microsoft.Extensions.DependencyInjection;

namespace Soddi;

public sealed class TypeRegistrar : ITypeRegistrar, IDisposable
{
    private IServiceCollection Services { get; }
    private IList<IDisposable> BuiltProviders { get; }

    public TypeRegistrar(IServiceCollection builder)
    {
        Services = builder;
        BuiltProviders = new List<IDisposable>();
    }

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

public sealed class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public object? Resolve(Type? type)
    {
        return type != null ? _provider.GetRequiredService(type) : null;
    }
}