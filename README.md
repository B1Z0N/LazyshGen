# Lazysh source generator

See [this](Usage/Program.cs) for detailed tutorial.

# Limitations

Unfortunately, as this project helped me to find out, we can't be more implicit about source generation.
Compiler can't run through all usages of LazyshFactory<ISomeInterface>, get all such interfaces and generate lazies for them on demand.
It won't work as the C++ templates do. You should always register it with some kind of static compile-time field/attribute/declaration and so on.

### BUT

If you think it's possible and have some hints on how to do it(how to actually emulate C++ tempaltes in C# source generation + C# generics),

### THEN

please contact [me](https://linktr.ee/b1z0n).
