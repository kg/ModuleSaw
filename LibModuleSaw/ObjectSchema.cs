using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public delegate void PayloadWriter<T> (AbstractModuleBuilder builder, ref T value);

    public interface ITypeSchema {
        List<SchemaEntry> Entries { get; }

        void WriteAbstract (AbstractModuleBuilder amb, object value);
    }

    public class TypeSchema<T> : ITypeSchema {
        public List<SchemaEntry> Entries
            { get; private set; } = new List<SchemaEntry>();

        public PayloadWriter<T> WritePayload = null;

        public void Write (AbstractModuleBuilder amb, ref T value) {
            foreach (var e in Entries) {
                var s = amb.Configuration.GetSchema(e.Type);
                var m = e.Type.GetMember(
                    e.Name, 
                    MemberTypes.Field | MemberTypes.Property,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                ).First();
                var fi = m as FieldInfo;
                var pi = m as PropertyInfo;

                if (fi != null)
                    s.WriteAbstract(amb, fi.GetValue(value));
                else if (pi != null)
                    s.WriteAbstract(amb, pi.GetValue(value));
            }

            if (WritePayload != null)
                WritePayload(amb, ref value);
        }

        public void WriteAbstract (AbstractModuleBuilder amb, object value) {
            var unboxed = (T)value;
            Write(amb, ref unboxed);
        }
    }

    public struct SchemaEntry {
        public Type Type;
        public string Name;
    }
}
