# UJectInit
UJectInit is a helper library to initialize [UJect](https://github.com/oopsitsacoder/UJect).

## Installation

First, install UJect. See [UJect](https://github.com/oopsitsacoder/UJect/blob/main/README.md).

Open the Unity package manager, choose "Install package from git URL", and input `https://github.com/oopsitsacoder/UJectInit.git`

## How to use

Import the package, and then set up an initialization class somewhere in your project. You can use a GameObject, or one of Unity's built-in attributes:

```csharp
class SomeInitializationClass
{
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RuntimeInitialize()
        {
            var container = new DiContainer("MyContainer");
            // This will collect all methods marked by the DiBind attribute and run them
            ReflectionDiBindImpl.CollectAndRunBindMethods(container);
        }
}
```

Next, bind your classes wherever you want:
```csharp
class SomeClassInProject
{
        [DiBind]
        public static void DiBind(DiContainer diContainer)
        {
            diContainer.Bind<IInterface1>().ToNewInstance<Impl1>();
        }
}
```

Now whenever your game is run, all bind methods will be fired, and you'll be ready to use your DiContainer!
