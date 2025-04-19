using Spectre.Console.Cli;
using Microsoft.Extensions.DependencyInjection;

public class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _builder;

    public TypeRegistrar(IServiceCollection builder) => _builder = builder;

    public ITypeResolver Build() => new TypeResolver(_builder.BuildServiceProvider());

    public void Register(Type service, Type implementation) => _builder.AddSingleton(service, implementation);
    public void RegisterInstance(Type service, object implementation) => _builder.AddSingleton(service, implementation);
    public void RegisterLazy(Type service, Func<object> factory) => _builder.AddSingleton(service, _ => factory());
}

public class TypeResolver : ITypeResolver, IDisposable
{
    private readonly ServiceProvider _provider;

    public TypeResolver(ServiceProvider provider) => _provider = provider;

    public object? Resolve(Type? type)
    {
        if (type == null) return null;
        return _provider.GetService(type);
    }

    public void Dispose() => _provider.Dispose();
}
