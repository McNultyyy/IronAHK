using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection.Emit;
using System.Reflection;

namespace IronAHK.Scripting
{
    internal partial class MethodWriter
    {
        Type EmitMethodInvoke(CodeMethodInvokeExpression Invoke)
        {
            if(Invoke.Method.MethodName == string.Empty)
                throw new CompileException(Invoke, "Empty method name");

            Depth++;
            Debug("Emitting method invoke expression for "+Invoke.Method.MethodName);

            var Type = Invoke.Method.TargetObject as CodeTypeReferenceExpression;
            MethodInfo Info;

            ArgType[] Args = new ArgType[Invoke.Parameters.Count];

                for(int i = 0; i < Args.Length; i++)
                    Args[i] = ArgType.Expression;

            if(Type == null)
            {
                Info = Lookup.BestMatch(Invoke.Method.MethodName, Args);

                if(Info == null)
                    throw new CompileException(Invoke, "Could not look up method "+Invoke.Method.MethodName);
            }
            else Info = ResolveCannedMethod(Type, Invoke);

            ParameterInfo[] Parameters = Info.GetParameters();

            Depth++;
            for(int i = 0; i < Args.Length; i++)
            {
                Debug("Emitting parameter "+i);
                Type Generated = EmitExpression(Invoke.Parameters[i]);
                Args[i] = ArgType.Expression;

                ForceTopStack(Generated, Parameters[i].ParameterType);
            }
            Depth--;

            Generator.Emit(OpCodes.Call, Info);
            Depth--;

            return Info.ReturnType;
        }

        // Method to quickly resolve methods emitted frequently by parser
        MethodInfo ResolveCannedMethod(CodeTypeReferenceExpression Type, CodeMethodInvokeExpression Invoke)
        {
            Depth++;
            Type target, rusty = typeof(Rusty.Core);
            if (Type.Type.BaseType.Equals(rusty.FullName, StringComparison.OrdinalIgnoreCase))
                target = rusty;
            else
            {
                try
                {
                    target = System.Type.GetType(Type.Type.BaseType, true, false);
                }
                catch (Exception)
                {
                    throw new CompileException(Type, "Could not access type " + Type.Type.BaseType);
                }
            }

            try
            {
                Type[] types = null;
                if (target.FullName == typeof(string).FullName && Invoke.Method.MethodName == "Concat")
                    types = new Type[] { typeof(string[]) };
                MethodInfo method = types == null ? target.GetMethod(Invoke.Method.MethodName) : target.GetMethod(Invoke.Method.MethodName, types);
                if (method == null)
                    throw new ArgumentNullException();

                Depth--;
                return method;
            }
            catch (Exception)
            {
                throw new CompileException(Invoke, string.Format("Could not find method {0} in type {1}", Invoke.Method.MethodName, Type.Type.BaseType));
            }
        }
    }
}
