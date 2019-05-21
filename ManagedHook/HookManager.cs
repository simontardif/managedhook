using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ManagedHook
{
    /// <summary>
    /// The hook manager that handles all the hooks.
    /// </summary>
    public class HookManager
    {
        #region Private Fields

        private readonly Dictionary<IntPtr, Hook> _hooks = new Dictionary<IntPtr, Hook>(); // The effective list of used hooks
        private static HookManager _hookManager;

        private readonly Dictionary<IntPtr, Hook> _cacheHooks = new Dictionary<IntPtr, Hook>(); // Cache of hooks that was used
       
        #endregion

        #region Singleton Pattern

        private HookManager()
        {
        }

        public static HookManager Instance
        {
            get { return _hookManager ?? (_hookManager = new HookManager()); }
        }

        #endregion

        #region Public Api

        public Hook GetHook(IntPtr functionPointer)
        {
            Hook hook;
            if (_hooks.TryGetValue(functionPointer, out hook))
            {
                return hook;
            }

            return null;
        }

        /// <summary>
        /// Replace a function by a hook handler to be called instead of the hooked function.
        /// Please hook only once a function, otherwise the method returns an argument exception.
        /// </summary>
        /// <param name="function">The function to be replaced.</param>
        /// <param name="functionReplacer">The function replacer that is called instead of the hooked method.</param>
        public Hook ReplaceFunction(MethodBase function, Action<object> functionReplacer)
        {
            if (functionReplacer == null)
            {
                throw new ArgumentNullException(nameof(functionReplacer));
            }

            var functionReplacerInstance = new FunctionReplacerNoParameters(functionReplacer);
            return ReplaceFunction(function, functionReplacerInstance);
        }

        /// <summary>
        /// Replace a function by a hook handler to be called instead of the hooked function.
        /// Please hook only once a function, otherwise the method returns an argument exception.
        /// </summary>
        /// <param name="function">The function to be replaced.</param>
        /// <param name="functionReplacer">The function replacer with parameters that is called instead of the hooked method.</param>
        public Hook ReplaceFunction(MethodBase function, Action<object, object[]> functionReplacer)
        {
            if (functionReplacer == null)
            {
                throw new ArgumentNullException(nameof(functionReplacer));
            }

            var functionReplacerInstance = new FunctionReplacerWithParameters(functionReplacer);
            return ReplaceFunction(function, functionReplacerInstance);
        }

        /// <summary>
        /// Replace a function by a hook handler to be called instead of the hooked function.
        /// Please hook only once a function, otherwise the method returns an argument exception.
        /// </summary>
        /// <param name="function">The function to be replaced.</param>
        /// <param name="functionReplacer">The function replacer that is called instead of the hooked method.</param>
        public Hook ReplaceFunction(MethodBase function, IFunctionReplacer functionReplacer)
        {
            if (functionReplacer == null)
            {
                throw new ArgumentNullException(nameof(functionReplacer));
            }

            ValidateHookedFunction(function);
            RuntimeHelpers.PrepareConstrainedRegions();
            Hook hook;
            if (!_cacheHooks.TryGetValue(function.MethodHandle.GetFunctionPointer(), out hook))
            {
                hook = new Hook(function, functionReplacer);
                _cacheHooks.Add(hook.MethodPointer, hook);
            }
            else
            {
                hook.HookHandler = null;
                hook.FunctionReplacer = functionReplacer;
                hook.UpdateOriginalFunction();
            }

            _hooks.Add(hook.MethodPointer, hook); // Add the hook to the effective list of used hooks

            return hook;
        }

        /// <summary>
        /// Hook a function by a hook handler to be called before and after the function hooked.
        /// Please hook only once a function, otherwise the method returns an argument exception.
        /// </summary>
        /// <param name="function">The function to be hooked.</param>
        /// <param name="beforeCall">The action that is called just before the hooked method is called.</param>
        /// <param name="afterCall">The action that is called just after the hooked method is called.</param>
        public Hook HookFunction(MethodBase function, Action<object> beforeCall, Action<object> afterCall)
        {
            var hookHandlerInstance = new HookHandlerNoParameters(beforeCall, afterCall);
            return HookFunction(function, hookHandlerInstance);
        }

        /// <summary>
        /// Hook a function by a hook handler to be called before and after the function hooked.
        /// Please hook only once a function, otherwise the method returns an argument exception.
        /// </summary>
        /// <param name="function">The function to be hooked.</param>
        /// <param name="beforeCall">The action that is called just before the hooked method is called.</param>
        /// <param name="afterCall">The action that is called just after the hooked method is called.</param>
        public Hook HookFunction(MethodBase function, Action<object, object[]> beforeCall, Action<object, object[]> afterCall)
        {
            var hookHandlerInstance = new HookHandlerWithParameters(beforeCall, afterCall);
            return HookFunction(function, hookHandlerInstance);
        }

        /// <summary>
        /// Hook a function by a hook handler to be called before and after the function hooked.
        /// Please hook only once a function, otherwise the method returns an argument exception.
        /// </summary>
        /// <param name="function">The function to be hooked.</param>
        /// <param name="hookHandler">The hook handler that is called before and after the function hooked.</param>
        public Hook HookFunction(MethodBase function, IHookHandler hookHandler)
        {
            ValidateHookedFunction(function);
            RuntimeHelpers.PrepareConstrainedRegions();
            Hook hook;
            if (!_cacheHooks.TryGetValue(function.MethodHandle.GetFunctionPointer(), out hook))
            {
                hook = new Hook(function, hookHandler);
                _cacheHooks.Add(hook.MethodPointer, hook);
            }
            else
            {
                hook.HookHandler = hookHandler;
                hook.FunctionReplacer = null;
                hook.UpdateOriginalFunction();
            }

            _hooks.Add(hook.MethodPointer, hook); // Add the hook to the effective list of used hooks

            return hook;
        }

        /// <summary>65
        /// Unhook the previously hooked method
        /// </summary>
        /// <param name="function"></param>
        public bool UnHookFunction(Hook hook)
        {
            IntPtr functionPtr = hook.MethodPointer;
            return UnHookFunction(functionPtr);
        }

        /// <summary>
        /// Unhook the previously hooked method
        /// </summary>
        /// <param name="function"></param>
        public bool UnHookFunction(IntPtr functionPointer)
        {
            Hook hook;
            if (_hooks.TryGetValue(functionPointer, out hook))
            {
                hook.UnHook();
                _hooks.Remove(functionPointer);

                return true;
            }

            return false;
        }

        public bool CanBeHooked(MethodBase function)
        {
            return function != null &&
                   IntPtr.Size == 8 && // x64 assembly for the moment
                   !CanBeInlined(function) && // non-inlined methods
                   !_hooks.ContainsKey(function.MethodHandle.GetFunctionPointer()); // is not already hooked
        }

        #endregion

        #region Private Methods

        private void ValidateHookedFunction(MethodBase function)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }

            if (IntPtr.Size == 4)
            {
                throw new ArgumentException("Cannot hook in 32 bit assembly!");
            }

            if (CanBeInlined(function))
            {
                throw new ArgumentException("Cannot hook a function that can be possibly be inlined!");
            }

            if (_hooks.ContainsKey(function.MethodHandle.GetFunctionPointer()))
            {
                throw new ArgumentException("Cannot hook a function twice.", nameof(function));
            }
        }

        /// <summary>
        /// Return true if the function can be inlined (return false even if the function is in an optimized library)
        /// Non-Optimized library --> false
        /// No Inlining Attribute, Virtual --> false
        /// 32 bytes IL Code --> true (only condition that can make this function returns true)
        /// </summary>
        /// <param name="function">The function to be validated.</param>
        /// <returns>If the function can be inlined</returns>
        private static bool CanBeInlined(MethodBase function)
        {
            bool isJitOptimizer = IsJitOptimizerEnabled(function.Module.Assembly);
            if (!isJitOptimizer)
            {
                return false;
            }

            if (function.MethodImplementationFlags == MethodImplAttributes.NoInlining ||
               function.IsVirtual)
            {
                return false;
            }

            int ilSize = function.GetMethodBody().GetILAsByteArray().Length;
            if (ilSize <= 32)
            {
                return true;
            }

            return false;
        }

        private static bool IsJitOptimizerEnabled(Assembly assembly)
        {
            foreach (object att in assembly.GetCustomAttributes(false))
            {
                if (att.GetType() == Type.GetType("System.Diagnostics.DebuggableAttribute"))
                {
                    return !((System.Diagnostics.DebuggableAttribute)att).IsJITOptimizerDisabled;
                }
            }

            return false;
        }

        private class HookHandlerNoParameters : IHookHandler
        {
            private readonly Action<object> _beforeCall;
            private readonly Action<object> _afterCall;
            public HookHandlerNoParameters(Action<object> beforeCall, Action<object> afterCall)
            {
                _beforeCall = beforeCall;
                _afterCall = afterCall;
            }

            public void Before(object instanceHooked, object[] parameters)
            {
                _beforeCall?.Invoke(instanceHooked);
            }

            public void After(object instanceHooked, object[] parameters)
            {
                _afterCall?.Invoke(instanceHooked);
            }
        }

        private class HookHandlerWithParameters : IHookHandler
        {
            private readonly Action<object, object[]> _beforeCall;
            private readonly Action<object, object[]> _afterCall;
            public HookHandlerWithParameters(Action<object, object[]> beforeCall, Action<object, object[]> afterCall)
            {
                _beforeCall = beforeCall;
                _afterCall = afterCall;
            }

            public void Before(object instanceHooked, object[] parameters)
            {
                _beforeCall?.Invoke(instanceHooked, parameters);
            }

            public void After(object instanceHooked, object[] parameters)
            {
                _afterCall?.Invoke(instanceHooked, parameters);
            }
        }

        private class FunctionReplacerNoParameters : IFunctionReplacer
        {
            private readonly Action<object> _functionReplacer;
            public FunctionReplacerNoParameters(Action<object> functionReplacer)
            {
                _functionReplacer = functionReplacer;
            }

            public void NewFunction(object instanceHooked, object[] parameters)
            {
                _functionReplacer.Invoke(instanceHooked);
            }
        }

        private class FunctionReplacerWithParameters : IFunctionReplacer
        {
            private readonly Action<object, object[]> _functionReplacer;
            public FunctionReplacerWithParameters(Action<object, object[]> functionReplacer)
            {
                _functionReplacer = functionReplacer;
            }

            public void NewFunction(object instanceHooked, object[] parameters)
            {
                _functionReplacer.Invoke(instanceHooked, parameters);
            }
        }

        #endregion
    }
}
