namespace Epiforge.Extensions.Components;

class XmlSerializerPooledObjectPolicy(Type type) :
    IPooledObjectPolicy<XmlSerializer>
{
    public XmlSerializer Create() =>
        new(type);

    public bool Return(XmlSerializer obj) =>
        true;
}
