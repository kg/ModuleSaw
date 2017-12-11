using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleSaw {
    public class Configuration {
        private readonly Dictionary<Type, ITypeSchema> Schemata = 
            new Dictionary<Type, ITypeSchema>();

        public bool Varints;
        public bool ExcludePrimitivesFromPartitioning;

        public Configuration () {
            RegisterDefaultSchemata(this);
        }

        public void AddSchema<T> (TypeSchema<T> schema) {
            Schemata.Add(typeof(T), schema);
        }

        private static void RegisterPrimitiveSchema<T> (
            Configuration config, PayloadWriter<T> writer
        ) {
            config.Schemata.Add(typeof(T), new TypeSchema<T> {
                WritePayload = writer
            });
        }

        private static void RegisterDefaultSchemata (Configuration config) {
            var s = config.Schemata;

            RegisterPrimitiveSchema(
                config, (AbstractModuleBuilder mb, ref int i) => mb.Write(i)
            );
            RegisterPrimitiveSchema(
                config, (AbstractModuleBuilder mb, ref uint i) => mb.Write(i)
            );
            RegisterPrimitiveSchema(
                config, (AbstractModuleBuilder mb, ref long i) => mb.Write(i)
            );
            RegisterPrimitiveSchema(
                config, (AbstractModuleBuilder mb, ref ulong i) => mb.Write(i)
            );
            RegisterPrimitiveSchema(
                config, (AbstractModuleBuilder mb, ref bool b) => mb.Write(b)
            );
        }

        public TypeSchema<T> GetSchema<T> () {
            return (TypeSchema<T>)GetSchema(typeof(T));
        }

        public ITypeSchema GetSchema (Type t) {
            return Schemata[t];
        }
    }
}
