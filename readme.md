[![NuGet](https://img.shields.io/nuget/v/CSVParserGenerator.svg?style=flat-square)](https://www.nuget.org/packages/CSVParserGenerator/)
[![GitHub license](https://img.shields.io/github/license/LokiMidgard/CSV-Parser-Generator.svg?style=flat-square)](https://tldrlegal.com/license/mit-license#summary)


# CSV Parser Generator

A Parser for CSV with support for uncommon line separators (e.g. Unicode) and instantiation of read-only objects and working nullable handling.

## Getting started


Assuming you want to populate following Data Type

```c#
class Person{
    public Guid Id {get;init;}
    public required string Name {get;init;}
    public DateOnly Birthdate {get;init;}
    public int Score {get;init;}
}
```

and following data

```csv
B2DDC789-A793-4BEA-9A0B-D00FFCDD1FB5,"John Doe",2000-03-01,50
BE4015A0-9B37-4A1E-A92A-405496A1FF96,"Max Mustermann",2001-04-01,64
```

1. Include the NuGet package
1. Add following Method to one of your classes (the class must be partial)
    ```c#
    [Parser.CSVParser(nameof(Person.Id), nameof(Person.Name), nameof(Person.Birthdate), nameof(Person.Score))]
    internal partial IEnumerable<Person> ParseData(ReadOnlySpan<byte> data);
    ```
1. Call your `ParseData` and run your program, that's it.


You can use `ReadOnlySpan<byte>` or `ReadOnlySpan<char>` as input.

The result Type must have a parameter less constructor, the properties are set
with `new MyType() {Property1 = …}` this allows of the `required` keyword and
`init` property's which should led to less problems with non nullable
properties.

Container Types (return type of the Method) must satisfy following if they are not an Interface:
- Have a generic argument that defines the element type
- Have an `Add(T toadd)` Method

If they are Interfaces, they must be implemented by one of the following classes
- `System.Collections.Immutable.ImmutableArray`
- `System.Collections.Immutable.ImmutableHashSet`
- `System.Collections.Immutable.ImmutableList`
- `System.Collections.Generic.List`
- `System.Collections.Generic.HashSet`
- `System.Collections.Generic.HashSet`

For `ImmutableArray`, `ImmutableList` and `ImmutableHashSet` a builder is used to populate the collection

The Properties of the result type must either be string or need to implement
`ISpanParsable<T>`. The Attribute has a list of Property names that will be set.
Starting with the first column up to the last. If some columns should be
ignored, use null for that columns property.

## Configuration

There are several ways the parser can be configured

### CSVParse Attribute 

Settings you can change via the Attribute

#### HasHeader

If the first line should be ignored, normally because it contains a header, set this Property to `true`, default `false`

```c#
 [Parser.CSVParser(new string[]{nameof(Person.Id), nameof(Person.Name), nameof(Person.Birthdate), nameof(Person.Score)}, HasHeader = true )]
```

#### SeperatorSymbol

The symbol that separates the columns. Default `,`

```c#
 [Parser.CSVParser(new string[]{nameof(Person.Id), nameof(Person.Name), nameof(Person.Birthdate), nameof(Person.Score)}, SeperatorSymbol = ';' )]
```


#### QuoteSymbol

The symbol that sets the quote character. Default `"`

```c#
 [Parser.CSVParser(new string[]{nameof(Person.Id), nameof(Person.Name), nameof(Person.Birthdate), nameof(Person.Score)}, QuoteSymbol = '"' )]
```


#### ExtendedLineFeed

Normally the parser will only recognize `U+000A` New Line and `U+000D` Carriage return as line separators.
If your CSV file contains uncommon line breaks like `U+0085` *Next Line (Nel)*, you need to enable this setting. Default is `false`.

Supported are
- `U+0085` *Next Line (Nel)*
- `U+000C` *Form Feed (FF)*
- `U+2028` *Line Separator (LS)* **Only when parsing chars**
- `U+2029` *Paragraph Separator* **Only when parsing chars**


### Runtime Options

You can add a second parameter to the method, to pass several additional
options. The generic parameter of the `Option<T>` class must be the same as the
input

```c#
internal partial IEnumerable<Person> ParseData(ReadOnlySpan<byte> data, Parser.Option<byte> options);
// or…
internal partial IEnumerable<Person> ParseData(ReadOnlySpan<char> data, Parser.Option<char> options);
```

#### NumberOfElements

To give the parser a hint how many elements will be parsed, use this setting. It
will pass the int in the constructor as only parameter when creating the
collection. This normally sets the capacity.

```c#
Parser.Options<char> options = new() { 
    NumberOfElements = 1_000_000
};
ParseData(data, options);
```

#### StringFactory

This method will be used to create stings from `ReadOnlySpan` (either `byte` or
`char`). You can use it to define which encoding is used when reading bytes.
Default for `bytes` is `UTF8` for `chars` the `ToString()`-Method is called.

You can also use this factory to deduplicate strings. On large datasets with
repeating entries this can speed up time significant.

```c#
Parser.Options<char> options = new() { 
    StringFactory = System.Text.Encoding.Latin1.GetString
};
ParseData(data, options);
```


#### OnError

The parse method should not throw errors. Lines that do not match your
definition, will be ignored. You can register a Callback with the `OnError`
Property to be notified every time a row is ignored.

Depending on the Error different classes will be returned. The Type describes
the kind of error and hold additional information. E.g. `LineErrorParseError`
has ParsedElement that holds the string that could not be parsed (with the
limitation that it only works for char data).

```c#
Parser.Options<char> options = new() { 
    Culture = System.Globalization.CultureInfo.CurrentCulture
};
ParseData(data, options);
```

#### Culture

When parsing the fields to the configured `CultureInfo` is passed in the parse
method of `ISpanParsable`. Default is the `InvariantCulture`.

```c#
Parser.Options<char> options = new() { 
    Culture = System.Globalization.CultureInfo.CurrentCulture
};
ParseData(data, options);
```


### Parse Types that do not implement ISpanParsable and control parsing

If you need to deserialize a Type that dose not implement the `ISpanParsable`
interface or the default parse method provided by it dose not work for you, yod
can override the default behavior by adding an additional attribute to the pares method.

To change the parsing for one property, pass the name of the Property as first argument to `CSVPTransformer`.

```c#
[Parser.CSVPTransformer(nameof(Person.Birthdate),nameof(ToDate) )]
```

To change the parsing for all instances of one types, pass the desired type as first argument to `CSVPTransformer`.
```c#
[Parser.CSVPTransformer(typeof(DateOnly),nameof(ToDate) )]
```

The second parameter is a method that will be called. It returns the desired object and accepts either `ReadOnlySpan<char>` or `ReadOnlySpan<byte>`.

A complete sample
```c#
[Parser.CSVParser(nameof(Person.Id), nameof(Person.Name), nameof(Person.Birthdate), nameof(Person.Score))]
[Parser.CSVPTransformer(nameof(Person.Birthdate),nameof(ToDate) )]
public static partial IEnumerable<Person> ParseData(ReadOnlySpan<char> data);

private static DateOnly ToDate(ReadOnlySpan<char> data)
{
    return DateOnly.ParseExact(data, "dd-MM-yyyy");
}
```


