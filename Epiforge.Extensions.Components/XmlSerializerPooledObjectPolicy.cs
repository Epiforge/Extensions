namespace Epiforge.Extensions.Components;

class XmlSerializerPooledObjectPolicy :
    IPooledObjectPolicy<XmlSerializer>
{
    public XmlSerializerPooledObjectPolicy(Type type) =>
        this.type = type;

    readonly Type type;

    public XmlSerializer Create() =>
        new(type);

    public bool Return(XmlSerializer obj) =>
        true;
}
