using System;
using System.Collections.Immutable;
using Alto.CodeAnalysis.Binding;
using Mono.Cecil;

namespace Alto.CodeAnalysis.Symbols
{
    public sealed class TypeSymbol : Symbol
    {
        public static readonly TypeSymbol Error = new TypeSymbol("?");
        public static readonly TypeSymbol Any = new TypeSymbol("any");
        public static readonly TypeSymbol Int = new TypeSymbol("int");
        public static readonly TypeSymbol Bool = new TypeSymbol("bool");
        public static readonly TypeSymbol String = new TypeSymbol("string");
        public static readonly TypeSymbol Void = new TypeSymbol("void");
        private BoundScope _scope;

        private ImmutableArray<VariableSymbol> _fields = new();
        private ImmutableArray<FunctionSymbol> _functions = new(); 
        private FunctionSymbol _constructor;

        internal TypeSymbol(string name, ImmutableArray<FunctionSymbol> functions,
                           ImmutableArray<VariableSymbol> fields, FunctionSymbol constructor)
            : base(name)
        {
            _functions = functions;
            _functions.Add(constructor);
            _constructor = constructor;
            Console.WriteLine($"Adding ctor... t: {name}");

            _fields = fields;
            _scope = new BoundScope(null);
            foreach (var function in functions)
            {
                _scope.TryDeclareFunction(function);
            }
        }

        private TypeSymbol(string name)
            : base(name)
        { 
            _fields = ImmutableArray<VariableSymbol>.Empty;
            _functions = ImmutableArray<FunctionSymbol>.Empty;

            var ctor = new FunctionSymbol(".ctor", ImmutableArray<ParameterSymbol>.Empty, this);
            _functions.Add(ctor);
            _constructor = ctor;
        }

        public override SymbolKind Kind => SymbolKind.Type;
        public override string ToString() => Name;
        public ImmutableArray<VariableSymbol> Fields => _fields;
        public ImmutableArray<FunctionSymbol> Functions => _functions;
        public FunctionSymbol Constructor => _constructor;
        internal BoundScope Scope => _scope;

        public override bool Equals(object obj)
        {
            if (obj is TypeSymbol type)
            {
                if (base.Name != type.Name || 
                    Functions.Length != type.Functions.Length)
                {
                    if (Name == "IO")
                    {
                        foreach (var f in Functions)
                        {
                            Console.WriteLine($"    ff: {f.Name}");
                        }
                        foreach (var tf in type.Functions)
                        {
                            Console.WriteLine($"    tf: {tf.Name}");
                        }

                        Console.WriteLine($"failed at first func.Length {Functions.Length} typeFuncLength: {type.Functions.Length} funcLengthComp: {Functions.Length != type.Functions.Length}");
                    }

                    return false;
                }

                // methods
                // TODO: Also compare fields
                for (int i = 0; i < Functions.Length; i++)
                {
                    var myFunc = Functions[i];
                    var otherFunc = type.Functions[i];
                    if (!myFunc.Equals(otherFunc))
                    {
                        if (Name == "IO")
                        {
                            Console.WriteLine($"failed at second {type.Name}");
                            Console.WriteLine($"{myFunc.Name} | {otherFunc.Name}");
                        }

                        return false;
                    }
                }
                
                return true;   
            }

            return Equals(obj);
        }
    }
}