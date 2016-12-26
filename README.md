# ManagedHook
Managed Hook allows you to hook any methods with a handler that is called before and after the method hooked is called.

How to use:

1- Create a class that implements this interface IHookerHandler
public class MyHookHandler : IHookHandler
{
    public void Before(object instanceHooked)
    {
        // do whatever you want here
    }
    
    public void After(object instanceHooked)
    {
        // do whatever you want here
    }
}

2- Get the methodInfo for the method to be hooked:

MethodInfo methodHooked = typeof(Foo).GetMethod("MethodHooked");

3- Hook the method!
HookManager.Instance.HookFunction(methodHooked, new MyHookHandler());

4 - (opt) When finished, you can unhook the method:
HookManager.Instance.UnHookFunction(methodHooked);
