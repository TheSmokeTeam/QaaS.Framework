# QaaS.Framework.Serialization

Supports different types of serialization (serialize/deserialize functions) that may be used in QaaS.

[TOC]

## Supported Serializations

Each type of serialization has a `serializer` and `deserializer`.

The `serializer` converts a C# object to a byte[] representing it, if it receives null returns null.

The  `deserializer` converts a byte[] o C# object it represents, if it receives null returns null.

Both of those functionalities might sometimes require OR optionally be able to use a configuration called `TypeSpecificSerializationConfig`
that tells them with what C# object type they work.

### ProtobufMessage

#### Serializer

Serializes Serializable C# Protobuf Message object to a byte[] representing the object.

`TypeSpecificSerializationConfig` - **Unused**.

#### Deserializer

Deserializes a byte[] to the C# Protobuf Message object it represents.

`TypeSpecificSerializationConfig` - **Required**, If no available type is specified will throw an exception.

### Json

#### Serializer

Serializes any C# object to a byte[] representing Json.
:warning: does not work well with `JToken`, if you want to serialize a json object use `JsonNode` from `System.Text.Json`.

`TypeSpecificSerializationConfig` - **Unused**.

#### Deserializer

Deserializes a byte[] to a C# object representing json.

`TypeSpecificSerializationConfig` - **Optional**, if not given deserializes to `JsonNode` but a specific type can be specified.

### MessagePack

#### Serializer

Serializes any messagepack serializable C# object to a byte[] representing the messagepack.

`TypeSpecificSerializationConfig` - **Unused**.

#### Deserializer

Deserializes a byte[] to a C# object serializable to messagepack.

`TypeSpecificSerializationConfig` - **Optional**, if not given deserializes to `Dictionary<object, object>` or `object[]`
depending on the deserialized messagepack object but a specific type can be specified.

### Yaml

#### Serializer

Serializes any C# object to a byte[] representing yaml.

`TypeSpecificSerializationConfig` - **Unused**.

#### Deserializer

Deserializes a byte[] to a C# object representing yaml.

`TypeSpecificSerializationConfig` - **Optional**, if not given deserializes to `Dictionary<object, object>` but a specific type can be specified.

### Xml

#### Serializer

Serializes `XDocument` C# object to a byte[] representing xml.

`TypeSpecificSerializationConfig` - **Unused**.

#### Deserializer

Deserializes a byte[] to an `XDocument` C# object representing xml.

`TypeSpecificSerializationConfig` - **Unused**.

### XmlElement

#### Serializer

Serializes `XElement` C# object to a byte[] representing xml.

`TypeSpecificSerializationConfig` - **Unused**.

#### Deserializer

Deserializes a byte[] to an `XElement` C# object representing xml.

`TypeSpecificSerializationConfig` - **Unused**.

### Binary

Since binary serialization support was dropped on .net8 you need to add the following
 property to your `.csproj` file to use the `Binary` serializer/deserializer:

`.csproj`

```xml
<PropertyGroup>
    <EnableUnsafeBinaryFormatterSerialization>true</EnableUnsafeBinaryFormatterSerialization>
</PropertyGroup>
```

#### Serializer

Serializes Serializable C# object to a byte[] representing the object.

`TypeSpecificSerializationConfig` - **Unused**.

#### Deserializer

Deserializes a byte[] to the C# object it represents.

`TypeSpecificSerializationConfig` - **Unused**.
