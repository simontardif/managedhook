using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Permissions;
using System.Runtime.Remoting.Proxies;
using System.Collections.Generic;
using System.Windows.Threading;
using System.Windows;

namespace ManagedHook.Tests
{
    public class HookTests
    {
        static void Main(string[] args)
        {
            CanBeHooked_NoCondition_ReturnTrue();
            HookStaticMethod_CallOriginalMethod_StaticMethodHooked();

            ReplaceFunction_InternalType_NewFunctionCalled();
            ReplaceFunction_LamdaExpression_LambdaCalled();
            ReplaceFunction_LamdaExpressionWithParameters_LambdaCalled();

            HookFunction_LamdaExpressionNoParameters_LambdaCalled();
            HookFunction_LamdaExpressionWithParameters_LambdaCalled();

            HookInstanceMethod_CallOriginalMethod_InstanceMethodHooked();
            HookInstanceMethod_HookTwice_InstanceMethodHooked();
            HookInstanceMethod02_HookTwice_InstanceMethodHooked();
            HookInstanceMethod03_CallOriginalMethod_InstanceMethodHooked();
            HookEventHandler_CallEventHandlerSubscription_EventHandlerHooked();

            HookInternalInstanceMethod_CallOriginalInternalMethod_InstanceMethodHooked();
            HookPrivateInstanceMethod_CallOriginalPrivateMethod_InstanceMethodHooked();

            HookConstructor_CallConstructor_IsHooked();

            Console.WriteLine("Unit Test Successful!");
            Console.ReadKey();
        }

        #region Tests

        private static void CanBeHooked_NoCondition_ReturnTrue()
        {
            var hookHandler = new HookHandler();
            var classParameter = new ClassParameter();
            classParameter.Method();
            var hookClass = new HookClass("First Hook"); // the instance
            var instanceMethod = hookClass.GetType().GetMethod("OriginalInstanceMethod");
            Assert.IsTrue(HookManager.Instance.CanBeHooked(instanceMethod), "We should be able to hook this method");

            var hook = HookManager.Instance.HookFunction(instanceMethod, hookHandler);

            Assert.IsFalse(HookManager.Instance.CanBeHooked(instanceMethod), "We should not be able to hook this method");

            HookManager.Instance.UnHookFunction(hook);

            Assert.IsTrue(HookManager.Instance.CanBeHooked(instanceMethod), "We should be able to hook this method");
        }

        private static void HookFunction_LamdaExpressionNoParameters_LambdaCalled()
        {
            Assembly framework = Assembly.GetAssembly(typeof(ContentElement));
            Type clmt = framework.GetType("System.Windows.ContextLayoutManager");
            MethodInfo mi = clmt.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic);
            object clm = mi.Invoke(null, new object[] { Dispatcher.CurrentDispatcher });
            PropertyInfo aepi = clm.GetType().GetProperty("AutomationEvents", BindingFlags.Instance | BindingFlags.NonPublic);
            object automationEvents = aepi.GetValue(clm, null);
            FieldInfo cfi = automationEvents.GetType().GetField("_count", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo hfi = automationEvents.GetType().GetField("_head", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo rimi = automationEvents.GetType().GetMethod("Remove", BindingFlags.Instance | BindingFlags.NonPublic);

            var addMethod = automationEvents.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic);
            addMethod.Invoke(automationEvents, new[] { new object() });

            int callCount = 0;
            var hook = HookManager.Instance.HookFunction(addMethod, (t) =>
            {
                callCount++;
            }, null);

            addMethod.Invoke(automationEvents, new[] { new object() });

            Assert.AreEqual(1, callCount);

            HookManager.Instance.UnHookFunction(hook);
        }

        private static void HookFunction_LamdaExpressionWithParameters_LambdaCalled()
        {
            Assembly framework = Assembly.GetAssembly(typeof(ContentElement));
            Type clmt = framework.GetType("System.Windows.ContextLayoutManager");
            MethodInfo mi = clmt.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic);
            object clm = mi.Invoke(null, new object[] { Dispatcher.CurrentDispatcher });
            PropertyInfo aepi = clm.GetType().GetProperty("AutomationEvents", BindingFlags.Instance | BindingFlags.NonPublic);
            object automationEvents = aepi.GetValue(clm, null);
            FieldInfo cfi = automationEvents.GetType().GetField("_count", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo hfi = automationEvents.GetType().GetField("_head", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo rimi = automationEvents.GetType().GetMethod("Remove", BindingFlags.Instance | BindingFlags.NonPublic);

            var addMethod = automationEvents.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic);
            addMethod.Invoke(automationEvents, new[] { new object() });

            int callCount = 0;
            int parametersLength = 0;
            var hook = HookManager.Instance.HookFunction(addMethod, (t, w) =>
            {
                callCount++;
                parametersLength = w.Length;
            }, null);

            addMethod.Invoke(automationEvents, new[] { new object() });

            Assert.AreEqual(1, callCount);
            Assert.AreEqual(1, parametersLength);

            HookManager.Instance.UnHookFunction(hook);
        }

        private static void ReplaceFunction_LamdaExpressionWithParameters_LambdaCalled()
        {
            Assembly framework = Assembly.GetAssembly(typeof(ContentElement));
            Type clmt = framework.GetType("System.Windows.ContextLayoutManager");
            MethodInfo mi = clmt.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic);
            object clm = mi.Invoke(null, new object[] { Dispatcher.CurrentDispatcher });
            PropertyInfo aepi = clm.GetType().GetProperty("AutomationEvents", BindingFlags.Instance | BindingFlags.NonPublic);
            object automationEvents = aepi.GetValue(clm, null);
            FieldInfo cfi = automationEvents.GetType().GetField("_count", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo hfi = automationEvents.GetType().GetField("_head", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo rimi = automationEvents.GetType().GetMethod("Remove", BindingFlags.Instance | BindingFlags.NonPublic);

            var addMethod = automationEvents.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic);
            addMethod.Invoke(automationEvents, new[] { new object() });

            int callCount = 0;
            int parametersLength = 0;
            var hook = HookManager.Instance.ReplaceFunction(addMethod, (t, w) =>
            {
                parametersLength = w.Length;
                callCount++;
            });

            addMethod.Invoke(automationEvents, new[] { new object() });

            Assert.AreEqual(1, callCount);
            Assert.AreEqual(1, parametersLength);

            HookManager.Instance.UnHookFunction(hook);
        }

        private static void ReplaceFunction_LamdaExpression_LambdaCalled()
        {
            Assembly framework = Assembly.GetAssembly(typeof(ContentElement));
            Type clmt = framework.GetType("System.Windows.ContextLayoutManager");
            MethodInfo mi = clmt.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic);
            object clm = mi.Invoke(null, new object[] { Dispatcher.CurrentDispatcher });
            PropertyInfo aepi = clm.GetType().GetProperty("AutomationEvents", BindingFlags.Instance | BindingFlags.NonPublic);
            object automationEvents = aepi.GetValue(clm, null);
            FieldInfo cfi = automationEvents.GetType().GetField("_count", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo hfi = automationEvents.GetType().GetField("_head", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo rimi = automationEvents.GetType().GetMethod("Remove", BindingFlags.Instance | BindingFlags.NonPublic);

            var addMethod = automationEvents.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic);
            addMethod.Invoke(automationEvents, new[] { new object() });

            int callCount = 0;
            var hook = HookManager.Instance.ReplaceFunction(addMethod, (t) =>
            {
                callCount++;
            });

            addMethod.Invoke(automationEvents, new[] { new object() });

            Assert.AreEqual(1, callCount);

            HookManager.Instance.UnHookFunction(hook);
        }

        private static void ReplaceFunction_InternalType_NewFunctionCalled()
        {
            Assembly framework = Assembly.GetAssembly(typeof(ContentElement));
            Type clmt = framework.GetType("System.Windows.ContextLayoutManager");
            MethodInfo mi = clmt.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic);
            object clm = mi.Invoke(null, new object[] { Dispatcher.CurrentDispatcher });
            PropertyInfo aepi = clm.GetType().GetProperty("AutomationEvents", BindingFlags.Instance | BindingFlags.NonPublic);
            object automationEvents = aepi.GetValue(clm, null);
            FieldInfo cfi = automationEvents.GetType().GetField("_count", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo hfi = automationEvents.GetType().GetField("_head", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo rimi = automationEvents.GetType().GetMethod("Remove", BindingFlags.Instance | BindingFlags.NonPublic);

            var addMethod = automationEvents.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic);
            addMethod.Invoke(automationEvents, new[] { new object() });

            var functionReplacer = new FunctionReplacer();

            var hook = HookManager.Instance.ReplaceFunction(addMethod, functionReplacer);

            addMethod.Invoke(automationEvents, new[] { new object() });

            Assert.AreEqual(1, functionReplacer.CallsCount);

            HookManager.Instance.UnHookFunction(hook);
        }

        private static void HookEventHandler_CallEventHandlerSubscription_EventHandlerHooked()
        {
            var hookHandler = new HookHandler();
            var classParameter = new ClassParameter();
            classParameter.Method();

            var hookClass = new HookClass("First Hook"); // the instance
            var instanceMethod = hookClass.GetType().GetMethod("add_MyEvent");
            int ret = hookClass.OriginalInstanceMethod(3, 5, 7, classParameter);

            var hook = HookManager.Instance.HookFunction(instanceMethod, hookHandler);

            hookClass.MyEvent += (o, e) => { }; // The hooked method should be called, then the original function

            Assert.AreEqual(1, hookHandler.BeforeCallsCount, "The method before hook should be called once");
            Assert.AreEqual(1, hookHandler.AfterCallsCount);

            HookManager.Instance.UnHookFunction(hook);

            hookClass.MyEvent += (o, e) => { }; // The original method is called

            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);
        }

        public class EventWatcher<T>
        {
            public void WatchEvent(Action<T> eventToWatch)
            {
                CustomProxy<T> proxy = new CustomProxy<T>(InvocationType.Event);
                T tester = (T)proxy.GetTransparentProxy();
                eventToWatch(tester);

                Console.WriteLine(string.Format("Event to watch = {0}", proxy.Invocations.First()));
            }
        }

        public enum InvocationType { Event }

        public class CustomProxy<T> : RealProxy
        {
            private List<string> invocations = new List<string>();
            private InvocationType invocationType;

            public CustomProxy(InvocationType invocationType) : base(typeof(T))
            {
                this.invocations = new List<string>();
                this.invocationType = invocationType;
            }

            public List<string> Invocations
            {
                get
                {
                    return invocations;
                }
            }

            [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.Infrastructure)]
            [DebuggerStepThrough]
            public override IMessage Invoke(IMessage msg)
            {
                String methodName = (String)msg.Properties["__MethodName"];
                Type[] parameterTypes = (Type[])msg.Properties["__MethodSignature"];
                MethodBase method = typeof(T).GetMethod(methodName, parameterTypes);

                switch (invocationType)
                {
                    case InvocationType.Event:
                        invocations.Add(ReplaceAddRemovePrefixes(method.Name));
                        break;
                        // You could deal with other cases here if needed
                }

                IMethodCallMessage message = msg as IMethodCallMessage;
                Object response = null;
                ReturnMessage responseMessage = new ReturnMessage(response, null, 0, null, message);
                return responseMessage;
            }

            private string ReplaceAddRemovePrefixes(string method)
            {
                if (method.Contains("add_"))
                    return method.Replace("add_", "");
                if (method.Contains("remove_"))
                    return method.Replace("remove_", "");
                return method;
            }
        }

        private static void MyHook<T>(T hookClass, Action<T> p)
        {
            var methodBody = p.GetMethodInfo().GetMethodBody();
            //throw new NotImplementedException();
        }

        private static void O_MyEvent(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private static void HookInstanceMethod_CallOriginalMethod_InstanceMethodHooked()
        {
            var hookHandler = new HookHandler();
            var classParameter = new ClassParameter();
            classParameter.Method();

            var hookClass = new HookClass("First Hook"); // the instance
            var instanceMethod = hookClass.GetType().GetMethod("OriginalInstanceMethod");
            int ret = hookClass.OriginalInstanceMethod(3, 5, 7, classParameter);

            var hook = HookManager.Instance.HookFunction(instanceMethod, hookHandler);

            ret = hookClass.OriginalInstanceMethod(3, 5, 7, classParameter); // The hooked method should be called, then the original function

            Assert.AreEqual(15, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount, "The method before hook should be called once");
            Assert.AreEqual(1, hookHandler.AfterCallsCount);

            HookManager.Instance.UnHookFunction(hook);

            ret = hookClass.OriginalInstanceMethod(3, 5, 7, classParameter); // The original method is called

            Assert.AreEqual(15, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);
        }

        private static void HookInstanceMethod03_CallOriginalMethod_InstanceMethodHooked()
        {
            // Arrange
            var hookHandler = new HookHandler();
            var structParameter = new StructParameter();

            var hookClass = new HookClass("First Hook"); // the instance
            var instanceMethod = hookClass.GetType().GetMethod("OriginalInstanceMethod03");

            // Act
            var hook = HookManager.Instance.HookFunction(instanceMethod, hookHandler);

            int ret = hookClass.OriginalInstanceMethod03(structParameter, 3, 5, 7); // The hooked method should be called, then the original function

            // Assert
            Assert.AreEqual(24, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);

            HookManager.Instance.UnHookFunction(hook);

            ret = hookClass.OriginalInstanceMethod03(structParameter, 3, 5, 7); // The original method is called

            Assert.AreEqual(24, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);
        }

        private static void HookInstanceMethod_HookTwice_InstanceMethodHooked()
        {
            // Arrange
            var hookHandler = new HookHandler();
            var classParameter = new ClassParameter();

            var hookClass = new HookClass("First Hook"); // the instance
            var instanceMethod = hookClass.GetType().GetMethod("OriginalInstanceMethod");

            // Act
            var hook = HookManager.Instance.HookFunction(instanceMethod, hookHandler);

            int ret = hookClass.OriginalInstanceMethod(3, 5, 7, classParameter); // The hooked method should be called, then the original function

            Assert.AreEqual(15, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);

            HookManager.Instance.UnHookFunction(hook);

            var secondHookHandler = new SecondHookHandler();
            HookManager.Instance.HookFunction(instanceMethod, secondHookHandler);

            ret = hookClass.OriginalInstanceMethod(3, 5, 7, classParameter); // The original method is called

            // Assert
            Assert.AreEqual(15, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);

            Assert.AreEqual(1, secondHookHandler.BeforeCallsCount);
            Assert.AreEqual(1, secondHookHandler.AfterCallsCount);

            HookManager.Instance.UnHookFunction(hook);
        }

        private static void HookInstanceMethod02_HookTwice_InstanceMethodHooked()
        {
            // Arrange
            var hookHandler = new HookHandler();
            var classParameter = new ClassParameter();

            var hookClass = new HookClass("First Hook"); // the instance
            var instanceMethod = hookClass.GetType().GetMethod("OriginalInstanceMethod02");

            // Act
            var hook = HookManager.Instance.HookFunction(instanceMethod, hookHandler);

            int ret = hookClass.OriginalInstanceMethod02(classParameter, 3, 5, 7); // The hooked method should be called, then the original function

            Assert.AreEqual(17, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);

            HookManager.Instance.UnHookFunction(hook);

            var secondHookHandler = new SecondHookHandler();
            hook = HookManager.Instance.HookFunction(instanceMethod, secondHookHandler);

            ret = hookClass.OriginalInstanceMethod02(classParameter, 3, 5, 7); // The original method is called

            // Assert
            Assert.AreEqual(17, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);

            Assert.AreEqual(1, secondHookHandler.BeforeCallsCount);
            Assert.AreEqual(1, secondHookHandler.AfterCallsCount);

            HookManager.Instance.UnHookFunction(hook);
        }

        private static void HookStaticMethod_CallOriginalMethod_StaticMethodHooked()
        {
            // Arrange
            var hookHandler = new HookHandler();
            var classParameter = new ClassParameter();

            var hookClass = new HookClass("First Hook"); // the instance
            var staticMethod = hookClass.GetType().GetMethod("OriginalStaticMethod");

            // Act
            var hook = HookManager.Instance.HookFunction(staticMethod, hookHandler);

            int ret = HookClass.OriginalStaticMethod(3, 5, 7, classParameter); // The hooked method should be called, then the original function

            // Assert
            Assert.AreEqual(8, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);

            HookManager.Instance.UnHookFunction(hook);

            ret = HookClass.OriginalStaticMethod(3, 5, 7, classParameter); // The original method is called

            Assert.AreEqual(8, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);
        }

        private static void HookInternalInstanceMethod_CallOriginalInternalMethod_InstanceMethodHooked()
        {
            // Arrange
            var hookHandler = new HookHandler();
            var classParameter = new ClassParameter();

            var hookClass = new HookClass("First Hook"); // the instance
            var instanceMethod = hookClass.GetType().GetMethod("OriginalInternalInstanceMethod", BindingFlags.Instance | BindingFlags.NonPublic);

            // Act
            var hook = HookManager.Instance.HookFunction(instanceMethod, hookHandler);

            int ret = hookClass.OriginalInternalInstanceMethod(3, 5, 7, classParameter); // The hooked method should be called, then the original function

            // Assert
            Assert.AreEqual(15, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);

            HookManager.Instance.UnHookFunction(hook);

            ret = hookClass.OriginalInternalInstanceMethod(3, 5, 7, classParameter); // The original method is called

            Assert.AreEqual(15, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);
        }

        private static void HookPrivateInstanceMethod_CallOriginalPrivateMethod_InstanceMethodHooked()
        {
            // Arrange
            var hookHandler = new HookHandler();
            var classParameter = new ClassParameter();

            var hookClass = new HookClass("First Hook"); // the instance
            var instanceMethod = hookClass.GetType().GetMethod("OriginalPrivateInstanceMethod", BindingFlags.Instance | BindingFlags.NonPublic);

            // Act
            var hook = HookManager.Instance.HookFunction(instanceMethod, hookHandler);

            // Methods called by reflection doesn't return good values, todo: fix that
            //ret = (int)instanceMethod.Invoke(hookClass, new object[] {3, 5, 7, classParameter}); // The hooked method should be called, then the original function
            int ret = hookClass.CallPrivate(3, 5, 7, classParameter);

            // Assert
            Assert.AreEqual(3, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);

            HookManager.Instance.UnHookFunction(hook);

            ret = (int)instanceMethod.Invoke(hookClass, new object[] { 3, 5, 7, classParameter }); // The original method is called

            Assert.AreEqual(3, ret);
            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);
        }

        private static void HookConstructor_CallConstructor_IsHooked()
        {
            var hookHandler = new HookHandler();

            var constructor = typeof(HookClass).GetConstructor(new[] { typeof(string) });

            var hook = HookManager.Instance.HookFunction(constructor, hookHandler);

            var hookClass = new HookClass("First Hook"); // The hooked constructor should be called, then the original function

            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);

            HookManager.Instance.UnHookFunction(hook);

            hookClass = new HookClass("First Hook"); // The original method is called

            Assert.AreEqual(1, hookHandler.BeforeCallsCount);
            Assert.AreEqual(1, hookHandler.AfterCallsCount);
        }

        #endregion

        #region Data for Unit Tests

        internal class SimpleHookClass
        {
            public void Method()
            {
                //Trace.WriteLine("Original Instance Method called");
            }
        }

        internal class HookClass: MarshalByRefObject
        {
            public HookClass(string instanceName)
            {
                InstanceName = instanceName;
            }

            public string InstanceName { get; private set; }
            public event EventHandler MyEvent;

            public int CallOriginalMethod(object a, object b, object c, object d)
            {
                return OriginalInstanceMethod((int) a, (int) b, (int) c, (ClassParameter) d);
            }

            public int OriginalInstanceMethod02(ClassParameter c, int test, int k, int m)
            {
                object a = c;
                object t = test;
                object s = k;
                object y = m;

                Trace.WriteLine("Original Instance Method called");

                return 5 + k + m;
            }

            public int OriginalInstanceMethod03(StructParameter c, int test, int k, int m)
            {
                object a = c;
                object t = test;
                object s = k;
                object y = m;

                Trace.WriteLine("Original Instance Method called");

                return 12 + k + m;
            }

            //[MethodImpl(MethodImplOptions.NoInlining)]
            public int OriginalInstanceMethod(int test, int k, int m, ClassParameter c)
            {
                object t = test;
                object s = k;
                object y = m;
                object a = c;
                Console.WriteLine("Original Instance Method called");


                return 3 + k + m;
            }

            internal int OriginalInternalInstanceMethod(int test, int k, int m, ClassParameter c)
            {
                Trace.WriteLine("Original Instance Method called");

                return 3 + k + m;
            }

            public int CallPrivate(int test, int k, int m, ClassParameter c)
            {
                return OriginalPrivateInstanceMethod(test, k, m, c);
            }

            private int OriginalPrivateInstanceMethod(int test, int k, int m, ClassParameter c)
            {
                Trace.WriteLine("Original Instance Method called");

                return 3;
            }

            public static int OriginalStaticMethod(int test, int k, int m, ClassParameter c)
            {
                Trace.WriteLine("Original Static Method called");

                return 8;
            }
        }

        internal class ClassParameter
        {
            public ClassParameter()
            {
                Console.WriteLine("Class Parameter Constructor called");
            }

            public void Method()
            {
                Console.WriteLine("Class Parameter Method called");
            }
        }

        internal struct StructParameter
        {
        }

        public class FunctionReplacer : IFunctionReplacer
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public FunctionReplacer()
            {

            }
            private int _callsCount;

            public int CallsCount
            {
                get { return _callsCount; }
            }

            public void NewFunction(object instanceHooked, object[] parameters)
            {
                _callsCount++;
                Trace.WriteLine("New Function Called!");
            }
        }

        public class HookHandler : IHookHandler
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public HookHandler()
            {

            }
            private int _beforeCallsCount;
            private int _afterCallsCount;

            public int BeforeCallsCount
            {
                get { return _beforeCallsCount; }
            }

            public int AfterCallsCount
            {
                get { return _afterCallsCount; }
            }

            public void Before(object instanceHooked, object[] parameters)
            {
                _beforeCallsCount++;
                Trace.WriteLine("Before Method Called!");
            }

            public void After(object instanceHooked, object[] parameters)
            {
                _afterCallsCount++;
                Trace.WriteLine("After Method Called!");
            }
        }

        public class SecondHookHandler : IHookHandler
        {
            public SecondHookHandler()
            {

            }
            private int _beforeCallsCount;
            private int _afterCallsCount;

            public int BeforeCallsCount
            {
                get { return _beforeCallsCount; }
            }

            public int AfterCallsCount
            {
                get { return _afterCallsCount; }
            }

            public void Before(object instanceHooked, object[] parameters)
            {
                _beforeCallsCount++;
                Trace.WriteLine("Before Method Called!");
            }

            public void After(object instanceHooked, object[] parameters)
            {
                _afterCallsCount++;
                Trace.WriteLine("After Method Called!");
            }
        }

        #endregion
    }
}
