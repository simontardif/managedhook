[![Build Status](https://travis-ci.com/simontardif/managedhook.svg?branch=master)](https://travis-ci.com/simontardif/managedhook)
[![NuGet](https://img.shields.io/nuget/v/ManagedHook.svg)](https://www.nuget.org/packages/ManagedHook)
# ManagedHook
Managed Hook allows you to hook any methods with a handler that is called before and after the hooked method is called. <br>
Tested with X64 architecture with non-inlined methods. <br>
(careful with optimized code with inlined methods, the pattern was not intended to be used in that case) <br>

How to use:

1- Create a class that implements the interface IHookerHandler:

```cs
public class MyHookHandler : IHookHandler
{
    public void Before(object instanceHooked, object[] parameters)
    {
        // do whatever you want here
    }
    
    public void After(object instanceHooked, object[] parameters)
    {
        // do whatever you want here
    }
}
```

2- Get the methodInfo for the method to be hooked:
```cs
MethodInfo methodHooked = typeof(Foo).GetMethod("MethodHooked");
```

3- Hook the method!
```cs
HookManager.Instance.HookFunction(methodHooked, new MyHookHandler());
```
Or hook the method with lamdas (before call and after call)
```cs
HookManager.Instance.HookFunction(methodHooked, (t) => { ... }, (t) => { ... });
```

4 - (opt) When finished, you can unhook the method:
```cs
HookManager.Instance.UnHookFunction(methodHooked);
```


# How does it work internally

```cs
HookManager.Instance.HookFunction(methodHooked, new MyHookHandler());
```
1. It gets the address of the original provided function. <br>
2. It creates a dynamic assembly with two methods (one for instance and one for static method) <br>
   that will be used to be called instead of the original function. <br>
   The dynamic methods are created with the exact same signature as the orignal. <br>
3. It will update the assembly code of the original function to point to one of the generated dynamic methods <br>
 (the first 16 bytes will be modified) <br>
   - mov rbx, functionPointer (keep a copy of the original function pointer in register RBX)
   - jmp dynamicMethod (do a relative jump to the dynamic method)
   
That's it! <br>
When the original method is called, it will automatically be forwarded to the created dynamic method. <br>
Inside the generated method, it calls the handler that the client created. <br>
The HookManager keeps all the hooks with the function pointer as the key. <br>
(that's why we used the RBX register to know in which original function it was called) <br>
