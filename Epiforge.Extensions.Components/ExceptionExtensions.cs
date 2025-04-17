namespace Epiforge.Extensions.Components;

/// <summary>
/// Provides extension methods for exceptions
/// </summary>
public static class ExceptionExtensions
{
    static readonly Regex newLinePattern = new(@"(\r\n|\r|\n)", RegexOptions.Compiled);
    static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> propertiesByType = new();
    static readonly Regex stackTraceIndentation = new(@"^   ", RegexOptions.Compiled | RegexOptions.Multiline);
    static readonly ConcurrentDictionary<Type, DefaultObjectPool<XmlSerializer>> xmlSerializerObjectPoolByType = new();

    static IReadOnlyList<PropertyInfo> PropertiesByTypeValueFactory(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            .Where
            (
                property =>
                !(
                    property.PropertyType == typeof(string) &&
                    (
                        property.Name == nameof(Exception.Message) ||
                        property.Name == nameof(Exception.StackTrace)
                    )
                    ||
                    property.PropertyType == typeof(IDictionary) &&
                    property.Name == nameof(Exception.Data)
                    ||
                    property.PropertyType == typeof(Exception) &&
                    property.Name == nameof(Exception.InnerException)
                    ||
                    property.PropertyType == typeof(Exception[]) &&
                    property.Name == nameof(ReflectionTypeLoadException.LoaderExceptions)
                    ||
                    property.PropertyType == typeof(Type[]) &&
                    property.Name == nameof(ReflectionTypeLoadException.Types)
                    ||
                    property.PropertyType == typeof(ReadOnlyCollection<Exception>) &&
                    property.Name == nameof(AggregateException.InnerExceptions)
                )
            ).ToList().AsReadOnly();

    static IEnumerable<(string name, object? value)> GetAdditionalProperties(Exception ex, Type type) =>
        propertiesByType.GetOrAdd(type, PropertiesByTypeValueFactory).Select(property => (property.Name, property.FastGetValue(ex)));

    static string GetMethodDescription(MethodBase method) =>
        $"{method.Name}({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.FullName} {p.Name}"))})";

    /// <summary>
    /// Gets/sets the default <see cref="ExceptionFullDetailsFormat"/> used by <see cref="GetFullDetails"/>
    /// </summary>
    public static ExceptionFullDetailsFormat DefaultExceptionFullDetailsFormat { get; } = ExceptionFullDetailsFormat.PlainText;

    /// <summary>
    /// Creates a representation of an exception and all of its inner exceptions, including exception types, messages, and stack traces, and traversing multiple inner exceptions in the case of <see cref="AggregateException"/>
    /// </summary>
    /// <param name="ex">The exception for which to generate the representation</param>
    /// <param name="format">The format in which to create the representation</param>
    public static string GetFullDetails(this Exception ex, ExceptionFullDetailsFormat? format = null)
    {
        ArgumentNullException.ThrowIfNull(ex);
        try
        {
            ex = ex.Demystify();
        }
        catch
        {
            // there's nothing to do if this fails
        }
        switch (format ?? DefaultExceptionFullDetailsFormat)
        {
            case ExceptionFullDetailsFormat.Json:
                var bufferWriter = new ArrayBufferWriter<byte>();
                using (var json = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions { Indented = true }))
                {
                    GetFullDetailsInJson(ex, json);
                }
                return Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
            case ExceptionFullDetailsFormat.Xml:
                using (var str = new StringWriter())
                {
                    using (var xml = XmlWriter.Create(str, new XmlWriterSettings
                    {
                        Indent = true,
                        NewLineOnAttributes = true,
                        OmitXmlDeclaration = true
                    }))
                    {
                        xml.WriteStartDocument();
                        xml.WriteStartElement("exception");
                        GetFullDetailsInXml(ex ?? throw new ArgumentNullException(nameof(ex)), xml);
                        xml.WriteEndDocument();
                    }
                    return str.ToString();
                }
            default:
                return GetFullDetailsInPlainText(ex, 0);
        }
    }

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity")]
    static void GetFullDetailsInJson(Exception ex, Utf8JsonWriter json)
    {
        json.WriteStartObject();
        var type = ex.GetType();
        json.WriteString("type", type.FullName);
        json.WriteString("message", ex.Message);
        if (ex.Data?.Count > 0)
        {
            json.WritePropertyName("data");
            json.WriteStartArray();
            foreach (var key in ex.Data.Keys)
            {
                json.WriteStartObject();
                json.WritePropertyName("key");
                JsonSerializer.Serialize(json, key);
                object? value = null;
                try
                {
                    if (key is not null)
                        value = ex.Data[key];
                }
                catch (KeyNotFoundException)
                {
                }
                json.WritePropertyName("value");
                JsonSerializer.Serialize(json, value);
                json.WriteEndObject();
            }
            json.WriteEndArray();
        }
        var additionalProperties = GetAdditionalProperties(ex, type);
        if (additionalProperties.Any())
        {
            json.WritePropertyName("properties");
            json.WriteStartObject();
            foreach (var (name, value) in additionalProperties)
            {
                if (value is bool boolValue)
                    json.WriteBoolean(name, boolValue);
                else if (value is byte byteValue)
                    json.WriteNumber(name, byteValue);
                else if (value is sbyte sbyteValue)
                    json.WriteNumber(name, sbyteValue);
                else if (value is short shortValue)
                    json.WriteNumber(name, shortValue);
                else if (value is ushort ushortValue)
                    json.WriteNumber(name, ushortValue);
                else if (value is int intValue)
                    json.WriteNumber(name, intValue);
                else if (value is uint uintValue)
                    json.WriteNumber(name, uintValue);
                else if (value is long longValue)
                    json.WriteNumber(name, longValue);
                else if (value is ulong ulongValue)
                    json.WriteNumber(name, ulongValue);
                else if (value is float floatValue)
                    json.WriteNumber(name, floatValue);
                else if (value is double doubleValue)
                    json.WriteNumber(name, doubleValue);
                else if (value is decimal decimalValue)
                    json.WriteNumber(name, decimalValue);
                else if (value is char charValue)
                    json.WriteString(name, charValue.ToString());
                else if (value is string stringValue)
                    json.WriteString(name, stringValue);
                else if (value is DateTime dateTimeValue)
                    json.WriteString(name, dateTimeValue.ToString("o"));
                else if (value is DateTimeOffset dateTimeOffsetValue)
                    json.WriteString(name, dateTimeOffsetValue.ToString("o"));
                else if (value is TimeSpan timeSpanValue)
                    json.WriteString(name, timeSpanValue.ToString("c"));
                else if (value is Guid guidValue)
                    json.WriteString(name, guidValue.ToString());
                else if (value is Uri uriValue)
                    json.WriteString(name, uriValue.ToString());
                else if (value is Version versionValue)
                    json.WriteString(name, versionValue.ToString());
                else
                {
                    json.WritePropertyName(name);
                    try
                    {
                        JsonSerializer.Serialize(json, value);
                    }
                    catch (NotSupportedException)
                    {
                        JsonSerializer.Serialize(json, value?.ToString());
                    }
                }
            }
            json.WriteEndObject();
        }
        var stackTrace = new StackTrace(ex, true);
        json.WritePropertyName("stackTrace");
        json.WriteStartArray();
        foreach (var frame in stackTrace.GetFrames())
        {
            json.WriteStartObject();
            if (frame.GetMethod() is { } method)
            {
                if (method.DeclaringType is { } methodDeclaringType)
                    json.WriteString("type", methodDeclaringType.FullName);
                json.WriteString("method", GetMethodDescription(method));
            }
            if (frame.GetFileName() is string fileName)
            {
                json.WriteString("file", fileName);
                json.WriteNumber("line", frame.GetFileLineNumber());
                json.WriteNumber("column", frame.GetFileColumnNumber());
            }
            json.WriteNumber("offset", frame.GetILOffset());
            json.WriteEndObject();
        }
        json.WriteEndArray();
        if (ex is ReflectionTypeLoadException reflectedTypeLoad)
        {
            if (reflectedTypeLoad.LoaderExceptions?.Length > 0)
            {
                json.WritePropertyName("loaderExceptions");
                json.WriteStartArray();
                foreach (var loader in reflectedTypeLoad.LoaderExceptions)
                    if (loader is not null)
                        GetFullDetailsInJson(loader, json);
                json.WriteEndArray();
            }
            if (reflectedTypeLoad.Types?.Length > 0)
            {
                json.WritePropertyName("types");
                json.WriteStartArray();
                foreach (var reflectedTypeLoadType in reflectedTypeLoad.Types)
                    if (reflectedTypeLoadType is not null)
                        json.WriteStringValue(reflectedTypeLoadType.FullName);
                json.WriteEndArray();
            }
        }
        else if (ex is AggregateException aggregate && aggregate.InnerExceptions?.Count > 0)
        {
            json.WritePropertyName("innerExceptions");
            json.WriteStartArray();
            foreach (var inner in aggregate.InnerExceptions)
                GetFullDetailsInJson(inner, json);
            json.WriteEndArray();
        }
        else if (ex.InnerException is Exception inner)
        {
            json.WritePropertyName("innerException");
            GetFullDetailsInJson(inner, json);
        }
        json.WriteEndObject();
    }

    static string GetFullDetailsInPlainText(Exception? ex, int indent)
    {
        var exceptionDetails = new List<string>();
        var top = true;
        while (ex is not null)
        {
            var indentation = new string(' ', indent * 3);
            var additionalLineIndentation = new string(' ', (indent + 1) * 3);
            var additionalLines = new StringBuilder();
            var reflectionTypeLoad = ex as ReflectionTypeLoadException;
            if (reflectionTypeLoad?.Types.Length > 0)
                additionalLines.Append($"{Environment.NewLine}Types: {string.Join(", ", reflectionTypeLoad.Types.Where(t => t is not null).Select(t => t!.FullName))}");
            if (ex.Data?.Count > 0)
                foreach (var key in ex.Data.Keys)
                {
                    object? value = null;
                    try
                    {
                        if (key is not null)
                            value = ex.Data[key];
                    }
                    catch (KeyNotFoundException)
                    {
                        // do nothing
                    }
                    additionalLines.Append($"{Environment.NewLine}.Data[{key.ToObjectLiteral()}] = {value.ToObjectLiteral()}");
                }
            var additionalProperties = GetAdditionalProperties(ex, ex.GetType());
            if (additionalProperties.Any())
                foreach (var (name, value) in additionalProperties)
                    additionalLines.Append($"{Environment.NewLine}.{name} = {value.ToObjectLiteral()}");
            exceptionDetails.Add(newLinePattern.Replace($"{indentation}{(top ? "-- " : "   ")}{ex.GetType().Name}: {ex.Message}{additionalLines}{(string.IsNullOrWhiteSpace(ex.StackTrace) ? string.Empty : $"{Environment.NewLine}{stackTraceIndentation.Replace(ex.StackTrace, string.Empty)}")}", $"{Environment.NewLine}{additionalLineIndentation}"));
            if (reflectionTypeLoad?.LoaderExceptions?.Length > 0)
            {
                foreach (var loader in reflectionTypeLoad.LoaderExceptions)
                    if (loader is not null)
                        exceptionDetails.Add(GetFullDetailsInPlainText(loader, indent + 1));
                break;
            }
            else if (ex is AggregateException aggregate && aggregate.InnerExceptions?.Count > 0)
            {
                foreach (var inner in aggregate.InnerExceptions)
                    exceptionDetails.Add(GetFullDetailsInPlainText(inner, indent + 1));
                break;
            }
            else
                ex = ex.InnerException;
            top = false;
        }
        return string.Join($"{Environment.NewLine}{Environment.NewLine}", exceptionDetails);
    }

    [SuppressMessage("Maintainability", "CA1502: Avoid excessive complexity")]
    static void GetFullDetailsInXml(Exception ex, XmlWriter xml)
    {
        var type = ex.GetType();
        xml.WriteAttributeString("type", type.FullName);
        xml.WriteAttributeString("message", ex.Message);
        if (ex.Data?.Count > 0)
            foreach (var key in ex.Data.Keys)
            {
                xml.WriteStartElement("datum");
                xml.WriteStartElement("key");
                if (key is null)
                    xml.WriteAttributeString("null", "true");
                else
                {
                    var xmlSerializerObjectPool = xmlSerializerObjectPoolByType.GetOrAdd(key.GetType(), XmlSerializerObjectPoolByTypeValueFactory);
                    var xmlSerializer = xmlSerializerObjectPool.Get();
                    xmlSerializer.Serialize(xml, key);
                    xmlSerializerObjectPool.Return(xmlSerializer);
                }
                xml.WriteEndElement();
                object? value = null;
                try
                {
                    if (key is not null)
                        value = ex.Data[key];
                }
                catch (KeyNotFoundException)
                {
                    // do nothing
                }
                xml.WriteStartElement("value");
                if (value is null)
                    xml.WriteAttributeString("null", "true");
                else
                {
                    var xmlSerializerObjectPool = xmlSerializerObjectPoolByType.GetOrAdd(value.GetType(), XmlSerializerObjectPoolByTypeValueFactory);
                    var xmlSerializer = xmlSerializerObjectPool.Get();
                    xmlSerializer.Serialize(xml, value);
                    xmlSerializerObjectPool.Return(xmlSerializer);
                }
                xml.WriteEndElement();
                xml.WriteEndElement();
            }
        var additionalProperties = GetAdditionalProperties(ex, type);
        if (additionalProperties.Any())
            foreach (var (name, value) in additionalProperties)
            {
                xml.WriteStartElement("property");
                xml.WriteAttributeString("name", name);
                if (value is { })
                    xml.WriteAttributeString("value", value.ToString());
                xml.WriteEndElement();
            }
        var stackTrace = new StackTrace(ex, true);
        xml.WriteStartElement("stackTrace");
        foreach (var frame in stackTrace.GetFrames())
        {
            xml.WriteStartElement("frame");
            if (frame.GetMethod() is { } method)
            {
                if (method.DeclaringType is { } methodDeclaringType)
                    xml.WriteAttributeString("type", methodDeclaringType.FullName);
                xml.WriteAttributeString("method", GetMethodDescription(method));
            }
            if (frame.GetFileName() is string fileName)
            {
                xml.WriteAttributeString("file", fileName);
                xml.WriteAttributeString("line", frame.GetFileLineNumber().ToString());
                xml.WriteAttributeString("column", frame.GetFileColumnNumber().ToString());
            }
            xml.WriteAttributeString("offset", frame.GetILOffset().ToString());
            xml.WriteEndElement();
        }
        xml.WriteEndElement();
        if (ex is ReflectionTypeLoadException reflectedTypeLoad)
        {
            if (reflectedTypeLoad.LoaderExceptions?.Length > 0)
                foreach (var loader in reflectedTypeLoad.LoaderExceptions)
                    if (loader is not null)
                    {
                        xml.WriteStartElement("loaderException");
                        GetFullDetailsInXml(loader, xml);
                        xml.WriteEndElement();
                    }
            if (reflectedTypeLoad.Types?.Length > 0)
                foreach (var reflectedTypeLoadType in reflectedTypeLoad.Types)
                    if (reflectedTypeLoadType is not null)
                        xml.WriteElementString("type", reflectedTypeLoadType.FullName);
        }
        else if (ex is AggregateException aggregate && aggregate.InnerExceptions?.Count > 0)
            foreach (var inner in aggregate.InnerExceptions)
            {
                xml.WriteStartElement("innerException");
                GetFullDetailsInXml(inner, xml);
                xml.WriteEndElement();
            }
        else if (ex.InnerException is Exception inner)
        {
            xml.WriteStartElement("innerException");
            GetFullDetailsInXml(inner, xml);
            xml.WriteEndElement();
        }
    }

    static DefaultObjectPool<XmlSerializer> XmlSerializerObjectPoolByTypeValueFactory(Type type) =>
        new(new XmlSerializerPooledObjectPolicy(type));
}
