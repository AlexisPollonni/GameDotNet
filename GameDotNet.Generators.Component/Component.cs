using Microsoft.CodeAnalysis;

namespace GameDotNet.Generators.Component
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