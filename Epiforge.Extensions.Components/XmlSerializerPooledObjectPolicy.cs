namespace Epiforge.Extensions.Components;

[RequiresUnreferencedCode("Constructs serializers, which reflect over the type")]
[RequiresDynamicCode("Constructs serializers, which generate code at run time")]
class XmlSerializerPooledObjectPolicy(Type type) :
    IPooledObjectPolicy<XmlSerializer>
{
    public XmlSerializer Create() =>
        new(type);

    public bool Return(XmlSerializer obj) =>
        true;
}
