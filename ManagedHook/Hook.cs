using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.CSharp;

namespace ManagedHook
{
    /// <summary>
    /// The hook class that creates a bridge for the original function to another function that
    /// calls the hook handler before and after methods.
    /// </summary>
    public unsafe class Hook
    {
        #region Private Fields

        private readonly byte[] _originalFunction;
        private readonly List<Type> _functionParameterTypes;
        private readonly MethodInfo _hookFunction;
        private readonly IntPtr _hookFunctionPointer;
        private IHookHandler _hookHandler;
        private IFunctionReplacer _functionReplacer;
        private readonly MethodBase _function;
        private readonly IntPtr _functionPointer;
        private readonly Type _functionReturnType;
        private readonly bool _isConstructor;

        #endregion

        #region Constructor
        public Hook(MethodBase function, IHookHandler hookHandler)
            : this(function, hookHandler, null)
        {
        }

        public Hook(MethodBase function, IFunctionReplacer functionReplacer)
           : this(function, null, functionReplacer)
        {
        }

        private Hook(MethodBase function, IHookHandler hookHandler, IFunctionReplacer functionReplacer)
        {
            RuntimeHelpers.PrepareConstrainedRegions();
            _originalFunction = new byte[15];
            _function = function;
            _functionPointer = _function.MethodHandle.GetFunctionPointer();
            _functionParameterTypes = _function.GetParameters().Select(x => x.ParameterType).ToList();
            _isConstructor = _function.IsConstructor;
            if (!_function.IsConstructor)
            {
                _functionReturnType = ((MethodInfo)_function).ReturnType;
            }

            _hookFunction = CreateHookFunction(!function.IsStatic);
            _hookFunctionPointer = _hookFunction.MethodHandle.GetFunctionPointer();

            // force jit-compile these functions
            RuntimeHelpers.PrepareMethod(_function.MethodHandle);
            RuntimeHelpers.PrepareMethod(_hookFunction.MethodHandle);

            if (functionReplacer != null)
            {
                FunctionReplacer = functionReplacer;
            }
            else
            {
                HookHandler = hookHandler;
            }

            UpdateOriginalFunction();
        }

        static Hook()
        {
            // Rewrite rbx function
            var rbxMethod = typeof(Hook).GetMethod("GetRBX");
            RuntimeHelpers.PrepareMethod(rbxMethod.MethodHandle);

            uint dwOldProtect;
            IntPtr getRbxFunction = rbxMethod.MethodHandle.GetFunctionPointer();
            VirtualProtect(getRbxFunction, 4, (uint)Protection.PAGE_EXECUTE_READWRITE, out dwOldProtect);

            byte* getRbxFunctionPtr = (byte*)getRbxFunction.ToPointer();

            getRbxFunctionPtr[0] = 0x48; //mov    rax,rbx
            getRbxFunctionPtr[1] = 0x89;
            getRbxFunctionPtr[2] = 0xD8;
            getRbxFunctionPtr[3] = 0xC3; //ret
        }

        #endregion

        #region Properties

        /// <summary>
        /// Don't remove, it is called by the generated script
        /// </summary>
        public MethodBase Function
        {
            get { return _function; }
        }

        public IntPtr MethodPointer
        {
            get { return _functionPointer; }
        }

        public IntPtr HookFunctionPointer
        {
            get { return _hookFunctionPointer; }
        }

        /// <summary>
        /// Don't remove, it is called by the generated script
        /// </summary>
        public IHookHandler HookHandler
        {
            get { return _hookHandler; }
            set
            {
                if (value != null && _hookHandler != value)
                {
                    var beforeMethod = typeof(IHookHandler).GetMethod("Before");
                    var before = (Action<object>)Delegate.CreateDelegate(typeof(Action<object>), value, beforeMethod);

                    var afterMethod = typeof(IHookHandler).GetMethod("After");
                    var after = (Action<object>)Delegate.CreateDelegate(typeof(Action<object>), value, afterMethod);

                    RuntimeHelpers.PrepareMethod(before.Method.MethodHandle);
                    RuntimeHelpers.PrepareMethod(after.Method.MethodHandle);

                    _hookHandler = value;
                }
            }
        }

        /// <summary>
        /// Don't remove, it is called by the generated script
        /// </summary>
        public IFunctionReplacer FunctionReplacer
        {
            get { return _functionReplacer; }
            set
            {
                if (value != null && _functionReplacer != value)
                {
                    var newFunctionMethod = typeof(IFunctionReplacer).GetMethod("NewFunction");
                    var newFunction = (Action<object>)Delegate.CreateDelegate(typeof(Action<object>), value, newFunctionMethod);
                   
                    RuntimeHelpers.PrepareMethod(newFunction.Method.MethodHandle);

                    _functionReplacer = value;
                }
            }
        }

        #endregion

        #region Private Members

        private MethodInfo CreateHookFunction(bool isInstance)
        {
            string hookCode = @"
            using System;
            using System.Runtime.CompilerServices;
            using ManagedHook;

            public class MyProgram
            {
                [MethodImpl(MethodImplOptions.NoInlining)]
                private replace_me_with_return_type MyInstanceHook(replace_me_with_parameters_types)
                {
                    var hook = HookManager.Instance.GetHook((IntPtr)Hook.GetRBX());
                    if (hook.FunctionReplacer != null)
                    {
                         hook.FunctionReplacer.NewFunction(this);
                         return replace_me_with_default_return_type;
                    }

                    hook.HookHandler.Before(this); // The ""this"" here is the caller instance, this is not the current class ""MyProgram""
                    
                    HookManager.Instance.UnHookFunction(hook);
                    var ret = (replace_me_with_return_type)hook.Function.Invoke(this, new object[] {replace_me_with_parameters});

                    HookManager.Instance.HookFunction(hook.Function, hook.HookHandler);

                    hook.HookHandler.After(this);

                    return ret;
                }

                [MethodImpl(MethodImplOptions.NoInlining)]
                private static replace_me_with_return_type MyStaticHook(replace_me_with_parameters_types)
                {
                    var hook = HookManager.Instance.GetHook((IntPtr)Hook.GetRBX());
                    hook.HookHandler.Before(null); // the instance is null for static methods
                    
                    HookManager.Instance.UnHookFunction(hook);
                    var ret = (replace_me_with_return_type)hook.Function.Invoke(null, new object[] {replace_me_with_parameters});

                    HookManager.Instance.HookFunction(hook.Function, hook.HookHandler);

                    hook.HookHandler.After(null);

                    return ret;
                }
            }
            ";

            string programName = $"Program_{Guid.NewGuid().ToString("N")}";

            hookCode = hookCode.Replace("MyProgram", programName);

            hookCode = UpdateParameterTypes(hookCode);
            hookCode = UpdateReturnType(hookCode);

            hookCode = UpdateFunctionParams(hookCode);

            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters();

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic).ToList();
            foreach (var loadedAssembly in loadedAssemblies)
            {
                parameters.ReferencedAssemblies.Add(loadedAssembly.Location);
            }

            parameters.GenerateInMemory = true;
            parameters.IncludeDebugInformation = true; //.pdb
            parameters.GenerateExecutable = false; //dll

            CompilerResults results = provider.CompileAssemblyFromSource(parameters, hookCode);

            if (results.Errors.HasErrors)
            {
                throw new Exception(results.Errors[0].ErrorText);
            }

            Assembly assembly = results.CompiledAssembly;
            Type program = assembly.GetType(programName);

            MethodInfo myHook;
            if (isInstance)
            {
                myHook = program.GetMethod("MyInstanceHook", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            else
            {
                myHook = program.GetMethod("MyStaticHook", BindingFlags.Static | BindingFlags.NonPublic);
            }

            return myHook;
        }

        private string UpdateFunctionParams(string hookCode)
        {
            string functionParams = string.Empty;
            var paramCount = 0;
            for (int i = 0; i < _functionParameterTypes.Count; ++i)
            {
                functionParams += "p";
                functionParams += paramCount++;
                functionParams += ", ";
            }

            if (_functionParameterTypes.Count > 0)
            {
                functionParams = functionParams.Remove(functionParams.Length - 2);
                hookCode = hookCode.Replace("replace_me_with_parameters", functionParams);
            }
            else
            {
                hookCode = hookCode.Replace("replace_me_with_parameters", string.Empty);
            }
            return hookCode;
        }

        private string UpdateParameterTypes(string code)
        {
            string functionParamTypes = string.Empty;
            int paramCount = 0;
            foreach (var parameterType in _functionParameterTypes)
            {
                if (parameterType.IsPublic)
                {
                    functionParamTypes += parameterType.FullName.Replace("+", ".");
                }
                else
                {
                    functionParamTypes += "object";
                }
                
                functionParamTypes += " ";
                functionParamTypes += "p";
                functionParamTypes += paramCount++;
                functionParamTypes += ", ";
            }

            if (_functionParameterTypes.Count > 0)
            {
                functionParamTypes = functionParamTypes.Remove(functionParamTypes.Length - 2);
                code = code.Replace("replace_me_with_parameters_types", functionParamTypes);
            }
            else
            {
                code = code.Replace("replace_me_with_parameters_types", string.Empty);
            }

            return code;
        }

        private string UpdateReturnType(string code)
        {
            string returnType;
            if (_functionReturnType != null && !_functionReturnType.IsValueType && !_functionReturnType.IsVisible)
            {
                returnType = "object";
            }
            else
            {
                returnType = _functionReturnType?.FullName.Replace("+", ".");
            }

            if (returnType == "System.Void" || _isConstructor)
            {
                code = code.Replace("var ret = (replace_me_with_return_type)", "");
                code = code.Replace("replace_me_with_default_return_type", "");
                code = code.Replace("return ret;", "");
                returnType = "void";
            }

            code = code.Replace("replace_me_with_return_type", returnType);
            code = code.Replace("replace_me_with_default_return_type", $"default({returnType})");

            return code;
        }

        #endregion

        #region Public API

        public void UpdateOriginalFunction()
        {
            byte* functionPtr = (byte*)MethodPointer.ToPointer();
            byte* hookFunctionPtr = (byte*)HookFunctionPointer.ToPointer();

            long functionPtrLong = MethodPointer.ToInt64();

            if (functionPtrLong != 0)
            {
                for (int i = 0; i < 15; ++i) // make a copy of the 5 bytes 
                {
                    _originalFunction[i] = functionPtr[i];
                }

                uint dwOldProtect;
                VirtualProtect(MethodPointer, 5, (uint)Protection.PAGE_EXECUTE_READWRITE, out dwOldProtect);

                *functionPtr = 0x48; //mov rbx, functionPointer
                functionPtr++;
                *functionPtr = 0xBB;
                functionPtr++;

                *(long*)(functionPtr) = functionPtrLong;

                functionPtr += 8;

                *functionPtr = 0xE9; //jmp hookFunctionPointer
                functionPtr++;
                int jumpRelative = (int)hookFunctionPtr - (int)(functionPtr + 4);

                *(int*)(functionPtr) = jumpRelative;
            }
        }

        public void UnHook()
        {
            byte* functionPtr = (byte*)MethodPointer.ToPointer();
            for (int i = 0; i < 15; ++i)
            {
                functionPtr[i] = _originalFunction[i];
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// This is where we store the original function pointer that is called. 
        /// Let this method empty, it will be modified at runtime to get the rbx register.
        /// </summary>
        /// <returns>RBX value</returns>
        public static ulong GetRBX()
        {
            //mov    rax,rbx
            return 0; // assembly: only ret (0xC3)
        }

        #endregion

        #region DLLImport

        [DllImport("kernel32.dll")]
        static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        public enum Protection
        {
            PAGE_NOACCESS = 0x01,
            PAGE_READONLY = 0x02,
            PAGE_READWRITE = 0x04,
            PAGE_WRITECOPY = 0x08,
            PAGE_EXECUTE = 0x10,
            PAGE_EXECUTE_READ = 0x20,
            PAGE_EXECUTE_READWRITE = 0x40,
            PAGE_EXECUTE_WRITECOPY = 0x80,
            PAGE_GUARD = 0x100,
            PAGE_NOCACHE = 0x200,
            PAGE_WRITECOMBINE = 0x400
        }

        #endregion
    }
}
