# UJect

Uject is an attempt at a simplified dependency injection framework for Unity3d Games.

## Installation
Open the Unity package manager, choose "Install package from git URL", and input `https://github.com/oopsitsacoder/UJect.git`

## How to use

At its simplest, you can use a `DiContainer` as a dictionary of interfaces to concrete instances:

```
public class SampleClass
{
    //Create a new container
    private DiContainer container = new DiContainer("MyContainer");
    
    public SampleClass()
    {
        //Bind IInterface to an existing instance of impl
        var impl = new Impl();
        container.Bind<IInterface>().ToInstance(impl);

        //Retrieve from the container
        var dependency = container.Get<IInterface>();
    }

    private interface IInterface {}
    private class Impl : IInterface {}
}

```

## Binding

### Basic Bindings
UJect provides a simple binding interface for a few different use cases:

1. Bind to existing instance
```
        //Bind IInterface to an existing instance of impl
        var impl = new Impl();
        container.Bind<IInterface>().ToInstance(impl);
```
2. Bind to a new instance
```
        //Bind IInterface to a new instance of impl
        container.Bind<IInterface>().ToNewInstance<Impl>();
```
3. Bind to factory
```
        //Bind IInterface to a new instance, which will be created by the factory at resolution time
        IInstanceFactory<Impl> myFactory = ...;
        container.Bind<IInterface>().ToFactory(myFactory);
```
4. Bind to factory method
```
        //Bind IInterface to a new instance, which will be created by the factory method at resolution time
        Func<Impl> myFactoryFunc = ...;
        container.Bind<IInterface>().ToFactoryMethod(myFactoryFunc);
```
5. [Experimental] Bind to a Resource
```
        //Bind IInterface to a Unity resource (from a Resources folder)
        container.Bind<IInterface>().ToResource("MyResources/ImplAsset");
```

### Multi-Bindings
More than one interface can be bound to the same instance:
```
        class Impl : IInterface1, IInterface2 { ... }

        //Bind IInterface1 and IInterface2 to the same new instance of Impl
        var impl = new Impl();
        container.Bind<IInterface1, IInterface2>().ToNewInstance<Impl>();

        var interface1 = container.Get<IInterface1>();
        var interface2 = container.Get<IInterface2>();
        var isSameInstance = object.ReferenceEquals(interface1, interface2); // Is true

```

### Unbinding
You can unbind by calling
```
        container.Unbind<IInterface>();
```

### Custom IDs
Sometimes, you want to bind multiple instances of the same interface. In that case, you can give bind each with a custom `string` id:
```
        var impl1 = new Impl();
        var impl2 = new Impl();
        container.Bind<IInterface>().WithId("InstanceA").ToInstance(impl1);
        container.Bind<IInterface>().WithId("InstanceB").ToInstance(impl2);
```
They can be retrieved in a similar manner:
```
        var dependency1 = container.Get<IInterface>("InstanceA");
        var dependency2 = container.Get<IInterface>("InstanceB");
```

## Injection
Because calling `Get<TInterface>()` everywhere is annoying, UJect comes with an `[Inject]` attribute, which can be used to automatically inject dependencies into other classes via reflection.

### Field injection
You can mark fields to be injected automatically.
```
public class SampleClass
{
    //Create a new container
    private DiContainer container = new DiContainer("MyContainer");
    
    public SampleClass()
    {
        container.Bind<IInterface>().ToNewInstance<Impl>();
        container.Bind<IInterface2>().ToNewInstance<Impl2>();

        //Retrieve from the container
        var dependency = container.Get<IInterface>();
    }

    private interface IInterface {}
    private class Impl : IInterface 
    {
        [Inject] private readonly IInterface2 impl2;
    }
    
    private interface IInterface2 {}
    private class Impl2 : IInterface2 {}
}
```
In this example, `impl2` will be automatically injected into the `Impl` instance when `IInterface` is resolved.

### Constructor injection
You can also mark constructor parameters as injectable, and UJect will attempt to fill them in (or throw an exception if it cannot).
```
    private class Impl : IInterface 
    {
        private readonly IInterface2 impl2;

        // Constructor parameter will be automatically filled in
        public Impl([Inject] IInterface2 impl2) => this.impl2 = impl2;
    }
```

### Note:
If Unity's code stripping feature is turned on, it's possible that injected fields and constructors will be stripped, as they're only referenced via reflection. If you have code stripping turned on, you'll have to mark your injected memebers with `[Preserve]`
