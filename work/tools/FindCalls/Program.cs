using Mono.Cecil;
using Mono.Cecil.Cil;

string dll = args[0];
string needle = args[1];
var module = ModuleDefinition.ReadModule(dll);
foreach (TypeDefinition type in module.Types)
    Walk(type, needle);

static void Walk(TypeDefinition type, string needle)
{
    foreach (MethodDefinition method in type.Methods)
    {
        if (!method.HasBody) continue;
        foreach (Instruction instruction in method.Body.Instructions)
        {
            string text = instruction.Operand?.ToString() ?? "";
            if (text.IndexOf(needle, StringComparison.Ordinal) < 0) continue;
            Console.WriteLine(type.FullName + "::" + method.Name + " -> " + text);
            break;
        }
    }
    foreach (TypeDefinition nested in type.NestedTypes)
        Walk(nested, needle);
}
