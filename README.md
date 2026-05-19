# UJectInit
UJectInit is a helper library to initialize [UJect](https://github.com/oopsitsacoder/UJect).

**NOTE**: You do not have to use this library to use UJect. It's just an example/starting point. 

UJect is intentionally missing initialization code for flexibility, and there are many possible ways to initialize it.

## Installation

First, install UJect. See [UJect](https://github.com/oopsitsacoder/UJect/blob/main/README.md).

Open the Unity package manager, choose "Install package from git URL", and input `https://github.com/oopsitsacoder/UJectInit.git`

## How to use

### Basic Usage

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

### More advanced usage

One of the benefits of UJect is the ability to setup child containers when you want to be able to reset state (for example, resetting a level, or resetting data on player login).

You can set up custom DiBind Attributes to support this functionality if you so choose.

For example, say I want to use DiBind methods for a specific "kind" of container. 

You can create a custom `DiBindAttribute` like this:
```csharp
public static class BindAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class Login : DiBindAttribute
    {
    }
    
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class Game : DiBindAttribute
    {
    }
}
```

and then split your initialization into multiple parts:
```csharp
public static class TestInit
{
    [BindAttributes.Login]
    public static void LoginBind(DiContainer loginContainer) => ...
    
    [BindAttributes.Game]
    public static void GameBind(DiContainer gameContainer) => ...
    
    public static void Init()
    {
        var playerLoginContainer = new DiContainer("Login");
        var gameContainer = playerLoginContainer.CreateChildContainer("Game");

        var diBindImpl = ReflectionDiBindImpl.Instance;
    
        // Collect methods and filter to that container kind
        var bindMethods = diBindImpl.CollectBindMethodsByAttributeType();

        if (bindMethods.TryGetValue(typeof(BindAttributes.Login), out var loginBindMethodCollection))
        {
            loginBindMethodCollection.RunBindMethods(playerLoginContainer);
        }

        if (bindMethods.TryGetValue(typeof(BindAttributes.Game), out var gameBindMethodCollection))
        {
            gameBindMethodCollection.RunBindMethods(gameContainer);
        }
    }
}
```