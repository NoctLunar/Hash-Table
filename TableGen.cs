using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;

using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace TableGen {
    internal class Program {
        static void Main() {
            try{
                var dat = new List<string>();
                foreach (var ty in ModuleDefMD.Load("KIT.dll").Types)
                    foreach (var me in ty.Methods.Where(met => met.HasBody)) {
                        var bo = me.Body;
                        var In = bo.Instructions;

                        string HBGen(byte[] dats) => BitConverter.ToString(SHA256.Create().ComputeHash(dats)).Replace("-", "").ToLower();
                        string HTGen(string dats) => HBGen(Encoding.UTF8.GetBytes(dats));
                        string HLGen(IEnumerable<string> dats) => HTGen(string.Join(",", dats));

                        var IL = In.SelectMany(op => BitConverter.GetBytes((ushort)op.OpCode.Value)).ToArray();
                        var FLG = string.Join(",", new[] {
                          me.IsStatic ? "Static" : null,
                          me.CustomAttributes.Any(at =>
                          at.AttributeType.FullName == "System.Runtime.CompilerServices.AsyncStateMachineAttribute")
                          ? "Async" : null,
                          me.IsVirtual ? "Virtual" : null,
                          me.IsAbstract ? "Abstract" : null
                        }.Where(f => f != null));


                        dat.Add($"{ty.FullName}.{me.Name} | {me.ReturnType.FullName}({string.Join(",", me.Parameters.Select(p => p.Type.FullName))}) | " +
                            $"IL:{IL.Length} | Hash:{HBGen(IL)} | MaxStack:{bo.MaxStack} | " +
                            $"Locals:{HLGen(bo.Variables.Select(v => v.Type.FullName))} | Exception:{(bo.ExceptionHandlers.Count > 0 ? "HasHandlers" : "None")} | " +
                            $"Flags:{FLG} | RVA:{me.RVA} | " +
                            $"Hooked:{In.Any(op => op.OpCode == OpCodes.Jmp || op.OpCode == OpCodes.Ldftn)} | " +
                            $"Patched:{In.All(op => op.OpCode == OpCodes.Nop || op.OpCode == OpCodes.Ret)} | " +
                            $"Calls:{HLGen(In.Where(op => op.OpCode == OpCodes.Call || op.OpCode == OpCodes.Callvirt).Select(i => i.Operand?.ToString() ?? ""))} | " +
                            $"Types:{HLGen(In.Where(typ => typ.Operand is ITypeDefOrRef).Select(typ => typ.Operand.ToString()))} | " +
                            $"Fields:{HLGen(In.Where(fil => fil.Operand is IField).Select(fil => fil.Operand.ToString()))} | " +
                            $"Attributes:{HLGen(me.CustomAttributes.Select(a => a.AttributeType.FullName))} | " +
                            $"Generics:{string.Join(",", me.GenericParameters.Select(gp => gp.FullName))} | CallingConv:{me.CallingConvention}");
                    }

                File.WriteAllLines("generated_table.txt", dat);
                Console.WriteLine("Finished, Output hash table in the same dir :D");
            } catch (Exception ex) { Console.WriteLine($"Generation error -> {ex}"); }
            Console.ReadKey();
        }
    }
}
