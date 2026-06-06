using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        string dllPath = @"D:\Steam\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice_Data\Managed\Assembly-CSharp.dll";
        Assembly asm = Assembly.LoadFrom(dllPath);
        
        foreach (var typeName in new string[] { "AsyncKeyCode", "AnyKeyCode" })
        {
            Type t = asm.GetType(typeName);
            if (t == null)
            {
                // Try to find it in any type of the assembly
                t = asm.GetTypes().FirstOrDefault(x => x.Name == typeName);
            }
            if (t != null)
            {
                Console.WriteLine(String.Format("=== {0} ===", t.FullName));
                Console.WriteLine("Fields:");
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    Console.WriteLine(String.Format("  {0} {1}", f.FieldType.Name, f.Name));
                }
                Console.WriteLine("Properties:");
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    Console.WriteLine(String.Format("  {0} {1}", p.PropertyType.Name, p.Name));
                }
            }
            else
            {
                Console.WriteLine(String.Format("Could not find type {0}", typeName));
            }
        }
    }
}
