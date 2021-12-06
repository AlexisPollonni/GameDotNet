using Microsoft.CodeAnalysis;

namespace Generator
{
    internal class Component
    {
        public INamedTypeSymbol ComponentType { get; }
        public string VariableName { get; internal set; }

        public Component(INamedTypeSymbol componentType)
        {
            ComponentType = componentType;
        }
    }
}