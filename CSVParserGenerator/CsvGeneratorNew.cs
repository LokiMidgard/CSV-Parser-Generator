using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CsvReader;
[Generator]
public class CsvGeneratord : ISourceGenerator {

    private const string AttributeName = "CSVParser";
    private const string AttributeTransformerName = "CSVPTransformer";
    private const string AttributeNamespace = "Parser";

    private const string AttributeText = $$"""
#nullable enable
using System;
namespace {{AttributeNamespace}}
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    [System.Diagnostics.Conditional("AutoNotifyGenerator_DEBUG")]
    sealed class {{AttributeName}}Attribute : Attribute
    {
        public {{AttributeName}}Attribute(params string?[] columns)
        {
        }

        /// <summary>
        /// If true, will search for additional characters that represent a linebreak.
        /// </summary>
        public bool ExtendedLineFeed {get; set;}
        
        public bool HasHeader {get; set;}

        public char SeperatorSymbol {get; set;}
        public char QuoteSymbol {get; set;}
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    [System.Diagnostics.Conditional("AutoNotifyGenerator_DEBUG")]
    sealed class {{AttributeTransformerName}}Attribute : Attribute
    {
        public {{AttributeTransformerName}}Attribute(string columnName, string transformMethod)
        {
        }
        public {{AttributeTransformerName}}Attribute(Type type, string transformMethod)
        {
        }
    }

    internal delegate string StringFactory<T>(ReadOnlySpan<T> bytes) where T : struct;
    internal class Options<T>  where T : struct
    {
        /// <summary>
        /// The number of Elements the result List is initilized.
        /// For Fixsized List the rows must not exceed this.
        /// </summary>
        public int? NumberOfElements { get; init; }

        /// <summary>
        /// The mehtod used for creation of strings.
        /// You can set over this the encoding.
        /// </summary>
        public StringFactory<T>? StringFactory { get; init; }

        /// <summary>
        /// The culture to use when Parsing.
        /// </summary>
        public System.Globalization.CultureInfo? Culture { get; init; }

        public Action<LineError>? OnError { get; init; }
    }


    internal abstract record LineError(int Line)
    {
        public static LineErrorUnexpectedEnd UnexpectedEnd(int line)=> new(line);
        public static LineErrorParseError ParseError(int lineIndex,int columnIndex,System.Exception e, String? parsedElement) => new(lineIndex, columnIndex + 1, e, parsedElement);
        public static LineErrorToManyColumns ToManyColumns(int lineIndex,int expectedColumns)=> new(lineIndex, expectedColumns);
        public static LineErrorNotEnoghColumns NotEnoghColumns(int lineIndex,int columnIndex, int expectedColumns)=> new(lineIndex, columnIndex + 1,expectedColumns);
    }

    internal record LineErrorUnexpectedEnd(int Line) : LineError(Line);
    internal record LineErrorParseError(int Line, int Column, System.Exception Exception, String? ParsedElement) : LineError(Line);
    internal record LineErrorToManyColumns(int Line, int ExpectedColumns) : LineError(Line);
    internal record LineErrorNotEnoghColumns(int Line, int Column,  int ExpectedColumns) : LineError(Line);
}
""";


    public void Initialize(GeneratorInitializationContext context) {
        // Register the attribute source
        context.RegisterForPostInitialization((i) => i.AddSource($"{AttributeName}Attribute.g.cs", AttributeText));

        // Register a syntax receiver that will be created for each generation pass
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context) {
        // retrieve the populated receiver 
        if (context.SyntaxContextReceiver is not SyntaxReceiver receiver)
            return;

        // get the added attribute, and INotifyPropertyChanged
        INamedTypeSymbol attributeSymbol = context.Compilation.GetTypeByMetadataName($"{AttributeNamespace}.{AttributeName}Attribute") ?? throw new InvalidOperationException("Cant find expected Type");
        INamedTypeSymbol attributeTransformerSymbol = context.Compilation.GetTypeByMetadataName($"{AttributeNamespace}.{AttributeTransformerName}Attribute") ?? throw new InvalidOperationException("Cant find expected Type");
        INamedTypeSymbol notifySymbol = context.Compilation.GetTypeByMetadataName("System.ComponentModel.INotifyPropertyChanged") ?? throw new InvalidOperationException("Cant find expected Type");

        // group the fields by class, and generate the source
        foreach (IGrouping<INamedTypeSymbol, IMethodSymbol> group in receiver.Methods.GroupBy<IMethodSymbol, INamedTypeSymbol>(f => f.ContainingType, SymbolEqualityComparer.Default)) {
            string classSource;
            try {
                classSource = ProcessClass(group.Key, group.ToList(), attributeSymbol, attributeTransformerSymbol, notifySymbol, context);

            } catch (System.Exception e) {
                classSource = "#error\n" + e.ToString();

            }
            context.AddSource($"{group.Key.Name}_csvparse.g.cs", SourceText.From(classSource, System.Text.Encoding.UTF8));
        }
    }

    private string ProcessClass(INamedTypeSymbol classSymbol, List<IMethodSymbol> methods, ISymbol attributeSymbol, ISymbol attributeTransformerSymbol, ISymbol notifySymbol, GeneratorExecutionContext context) {
        if (!classSymbol.ContainingSymbol.Equals(classSymbol.ContainingNamespace, SymbolEqualityComparer.Default)) {
            return "FAIL"; //TODO: issue a diagnostic that it must be top level
        }

        string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

        // begin building the generated source
        StringBuilder source = new StringBuilder($$"""
#nullable restore
using System;

namespace {{namespaceName}}
{


   {{GetVisibility(classSymbol.DeclaredAccessibility)}}  {{(classSymbol.IsStatic ? " static " : "")}} partial class {{classSymbol.Name}} 
    {

""");

        // // if the class doesn't implement INotifyPropertyChanged already, add it
        // if (!classSymbol.Interfaces.Contains(notifySymbol, SymbolEqualityComparer.Default)) {
        //     source.AppendLine("public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;");
        // }

        // create properties for each field 
        foreach (var methodSymbol in methods) {
            ProcessMethod(source, methodSymbol, attributeSymbol, attributeTransformerSymbol, context);
        }

        source.AppendLine("} }");
        return source.ToString();
    }




    private void ProcessMethod(StringBuilder source, IMethodSymbol methodSymbol, ISymbol attributeSymbol, ISymbol attributeTransformerSymbol, GeneratorExecutionContext context) {
        // get the name and type of the field
        // string fieldName = methodSymbol.Name;
        // ITypeSymbol fieldType = methodSymbol.Type;

        // get the AutoNotify attribute from the field, and any associated data
        AttributeData attributeData = methodSymbol.GetAttributes().Single(ad => ad.AttributeClass?.Equals(attributeSymbol, SymbolEqualityComparer.Default) ?? false);
        var attributeTransformData = methodSymbol.GetAttributes().Where(ad => ad.AttributeClass?.Equals(attributeTransformerSymbol, SymbolEqualityComparer.Default) ?? false).ToList();

        // TypedConstant overridenNameOpt = attributeData.NamedArguments.SingleOrDefault(kvp => kvp.Key == "PropertyName").Value;

        var argument = attributeData.ConstructorArguments[0];


        var hasHeader = (attributeData.NamedArguments.FirstOrDefault(x => x.Key == "HasHeader").Value.Value as bool?) ?? false;
        var extendedLineFeed = (attributeData.NamedArguments.FirstOrDefault(x => x.Key == "ExtendedLineFeed ").Value.Value as bool?) ?? false;
        var seperatorSymbol = (attributeData.NamedArguments.FirstOrDefault(x => x.Key == "SeperatorSymbol").Value.Value as char?) ?? ',';
        var quoteSymbol = (attributeData.NamedArguments.FirstOrDefault(x => x.Key == "QuoteSymbol").Value.Value as char?) ?? '"';


        // string propertyName = chooseName(fieldName, overridenNameOpt);
        // if (propertyName.Length == 0 || propertyName == fieldName) {
        //     //TODO: issue a diagnostic that we can't process this field
        //     return;
        // }





        ITypeSymbol targetType;
        ITypeSymbol resultCollection = methodSymbol.ReturnType;
        if (resultCollection is INamedTypeSymbol returnTypeSymbol && returnTypeSymbol.IsGenericType && returnTypeSymbol.TypeArguments.Length > 0) {
            targetType = returnTypeSymbol.TypeArguments[0];
        } else {
            source.AppendLine($"#error \"It is assumed that the instances are the first generic Argument of the return type {resultCollection}\"");
            return;
        }



        var resultType = GetFullMetadataName(targetType);

        string resultCollectionInstantiation;
        string resultCollectionFinish;


        var supportedCollections = new INamedTypeSymbol[] {
            context.Compilation.GetTypeByMetadataName($"System.Collections.Immutable.ImmutableArray`1") ?? throw new InvalidOperationException("Expected type dose not exist System.Collections.Immutable.ImmutableArray<{resultType}>"),
            context.Compilation.GetTypeByMetadataName($"System.Collections.Immutable.ImmutableHashSet`1") ?? throw new InvalidOperationException("Expected type dose not exist System.Collections.Immutable.ImmutableHashSet<{resultType}>"),
            context.Compilation.GetTypeByMetadataName($"System.Collections.Immutable.ImmutableList`1") ?? throw new InvalidOperationException("Expected type dose not exist System.Collections.Immutable.ImmutableList<{resultType}>"),
            context.Compilation.GetTypeByMetadataName($"System.Collections.Generic.List`1") ?? throw new InvalidOperationException("Expected type dose not exist System.Collections.Generic.List<{resultType}"),
            context.Compilation.GetTypeByMetadataName($"System.Collections.Generic.HashSet`1") ?? throw new InvalidOperationException("Expected type dose not exist System.Collections.Generic.HashSet<{resultType}>"),
            context.Compilation.GetTypeByMetadataName($"System.Collections.Generic.HashSet`1") ?? throw new InvalidOperationException("Expected type dose not exist System.Collections.Generic.HashSet<{resultType}>"),
        };

        if (resultCollection.TypeKind == TypeKind.Interface && (supportedCollections.FirstOrDefault(x => x.Interfaces.Any(y => GetFullMetadataName(y) == GetFullMetadataName(resultCollection))) is INamedTypeSymbol implementingType)) {
            resultCollection = implementingType;
        } else if (resultCollection.TypeKind == TypeKind.Interface) {
            source.AppendLine($"#error \"Unsupported Interface type {resultCollection}\"");
            return;
        }

        if (GetFullMetadataName(resultCollection) == "System.Collections.Immutable.ImmutableArray") {
            resultCollectionInstantiation = $$"""System.Collections.Immutable.ImmutableArray.CreateBuilder<{{resultType}}>""";
            resultCollectionFinish = ".ToImmutable()";
        } else if (GetFullMetadataName(resultCollection) == "System.Collections.Immutable.ImmutableHashSet") {
            resultCollectionInstantiation = $$"""System.Collections.Immutable.ImmutableHashSet.CreateBuilder<{{resultType}}>""";
            resultCollectionFinish = ".ToImmutable()";
        } else if (GetFullMetadataName(resultCollection) == "System.Collections.Immutable.ImmutableList") {
            resultCollectionInstantiation = $$"""System.Collections.Immutable.ImmutableList.CreateBuilder<{{resultType}}>""";
            resultCollectionFinish = ".ToImmutable()";
        } else if (resultCollection.GetMembers("Add").FirstOrDefault() is IMethodSymbol addMethod
              && addMethod.Parameters.Length == 1
              && GetFullMetadataName(addMethod.Parameters[0].Type) == resultType) {
            resultCollectionInstantiation = $"new {resultCollection}";
            resultCollectionFinish = "";
        } else {
            source.AppendLine($"#error \"Cant handle return type {resultCollection}\"");
            return;
        }


        string rawDataType;


        if (methodSymbol.Parameters.Length > 0) {

            if (methodSymbol.Parameters[0].Type is not INamedTypeSymbol namedParameter || GetFullMetadataName(namedParameter) != "System.ReadOnlySpan" || (GetFullMetadataName(namedParameter.TypeArguments[0]) != "System.Byte" && GetFullMetadataName(namedParameter.TypeArguments[0]) != "System.Char")) {
                source.AppendLine($"#error \"Frist Parameter must be ReadOnlySpan<char> or ReadOnlySpan<byte>\"");
                return;
            }

            rawDataType = GetFullMetadataName(namedParameter.TypeArguments[0]);
        } else {
            source.AppendLine($"#error \"Frist Parameter must be ReadOnlySpan<char> or ReadOnlySpan<byte>\"");
            return;
        }


        var defaultStringFactory = rawDataType == "System.Byte"
            ? $"new global::{AttributeNamespace}.StringFactory<{rawDataType}>(System.Text.Encoding.UTF8.GetString)"
            : $"new global::{AttributeNamespace}.StringFactory<{rawDataType}>(x=>x.ToString())";

        bool handleError;
        if (methodSymbol.Parameters.Length == 1) {


            source.AppendLine($$"""
        {{GetVisibility(methodSymbol.DeclaredAccessibility)}} {{(methodSymbol.IsStatic ? " static " : "")}} partial {{methodSymbol.ReturnType}} {{methodSymbol.Name}}(global::System.ReadOnlySpan<{{rawDataType}}> raw)
        {
            var result = {{resultCollectionInstantiation}}(); ;
            var stringFactory = {{defaultStringFactory}};
            var culture = System.Globalization.CultureInfo.InvariantCulture;
        """);

            handleError = false;

        } else if (methodSymbol.Parameters.Length == 2) {
            source.AppendLine($$"""
        {{GetVisibility(methodSymbol.DeclaredAccessibility)}} {{(methodSymbol.IsStatic ? " static " : "")}} partial {{methodSymbol.ReturnType}} {{methodSymbol.Name}}(global::System.ReadOnlySpan<{{rawDataType}}> raw, global::{{AttributeNamespace}}.Options<{{rawDataType}}> option)
        {
            
            var result = option.NumberOfElements.HasValue ?  {{resultCollectionInstantiation}}(option.NumberOfElements.Value) : {{resultCollectionInstantiation}}();
            var stringFactory = option.StringFactory ?? {{defaultStringFactory}};
            var culture = option.Culture ?? System.Globalization.CultureInfo.InvariantCulture;
            var onError = option.OnError;
        """);
            handleError = true;

        } else {
            source.AppendLine($"#error \"We need exactly one or two parameters\"");
            return;

        }



        source.AppendLine($$"""
            var quoteSymbol = ({{rawDataType}})'{{quoteSymbol}}';
            var seperatorSymbol = ({{rawDataType}})'{{seperatorSymbol}}';
        """);

        if (!extendedLineFeed) {
            source.AppendLine($$"""
            {{rawDataType}} lineBreak = ({{rawDataType}})'\n', lineFeed = ({{rawDataType}})'\r';
            ReadOnlySpan<{{rawDataType}}> linebreaks = stackalloc {{rawDataType}}[] { lineBreak, lineFeed};
            ReadOnlySpan<{{rawDataType}}> seperators = stackalloc {{rawDataType}}[] { lineBreak, lineFeed, seperatorSymbol };
                
        """);

        } else
        if (rawDataType == "System.Byte") {
            source.AppendLine($$"""
            byte lineBreak = (byte)'\n', lineFeed = (byte)'\r', /*paragraphSeperator = (byte)'\u2029', lineSeperator = (byte)'\u2028',*/ nextLine = (byte)'\u0085', formFeed = (byte)'\f';
            ReadOnlySpan<{{rawDataType}}> linebreaks = stackalloc {{rawDataType}}[] { lineBreak, lineFeed, nextLine, formFeed};
            ReadOnlySpan<{{rawDataType}}> seperators = stackalloc {{rawDataType}}[] { lineBreak, lineFeed, nextLine, formFeed, seperatorSymbol };
                
        """);

        } else {
            source.AppendLine($$"""
            char lineBreak = '\n', lineFeed = '\r', paragraphSeperator = '\u2029', lineSeperator = '\u2028', nextLine = '\u0085', formFeed = '\f';
            ReadOnlySpan<{{rawDataType}}> linebreaks = stackalloc {{rawDataType}}[] { lineBreak, lineFeed, paragraphSeperator, lineSeperator, nextLine, formFeed};
            ReadOnlySpan<{{rawDataType}}> seperators = stackalloc {{rawDataType}}[] { lineBreak, lineFeed, paragraphSeperator, lineSeperator, nextLine, formFeed, seperatorSymbol };
        
        """);

        }

        source.AppendLine($$"""



    var rest = raw;

    var lineIndex = 0;
    while (true){

            lineIndex++;

            bool isQuoted;
            int isQuotedInt;
            int start, end;
            global::System.ReadOnlySpan<{{rawDataType}}> dataEntry;
            var propertyIndex = 0;
            
            int restLength;
""");
        if (hasHeader)
            source.AppendLine($$"""
            // Skip first line
            var next = rest.IndexOfAny(linebreaks);
            if (next == -1)
            {
                break;
            }

            rest = rest[next..];
            next = rest.IndexOfAnyExcept(linebreaks);
            if (next == -1)
            {
                break;
            }
            rest = rest[next..];
        """);

        string?[] properties = argument.Values.Select(x => x.Value).Where(x=>x is null || x is string).Cast<string?>().ToArray();
        foreach (var (property, index) in properties.Select((x, i) => (x, i))) {

            ITypeSymbol? propertyType;

            if (property is null) {
                propertyType = null;
            } else {

                propertyType = targetType.GetMembers(property).OfType<IPropertySymbol>().SingleOrDefault()?.Type;
                propertyType ??= targetType.GetMembers(property).OfType<IFieldSymbol>().SingleOrDefault()?.Type;

                if (propertyType is null) {
                    source.AppendLine($"""
                        #error "Property {property}" not found
                    """);
                }

            }




            var converter = attributeTransformData.Where(x => x.ConstructorArguments[0].Value is string propName && propName == property).SingleOrDefault()
            ?? attributeTransformData.SingleOrDefault(x => x.ConstructorArguments[0].Value is INamedTypeSymbol type && type.Equals(propertyType, SymbolEqualityComparer.Default));

            var converterMethod = converter?.ConstructorArguments[1].Value as string;

            source.AppendLine($$"""
                // BEGIN {{property ?? "IGNORED"}}
                restLength = rest.Length;
            """);

            bool errorOnNoMoreData = index != properties.Length - 1 || (propertyType is not null && propertyType.IsValueType && GetFullMetadataName(propertyType) != "System.Nullable");
            if (errorOnNoMoreData) {
                if (index > 0) { // for the first column it is not an error
                    source.AppendLine($$"""
                        if(restLength==0) { 
                            {{(handleError ? "onError?.Invoke(Parser.LineError.UnexpectedEnd(lineIndex));" : string.Empty)}}
                            break;
                        }
                        """);
                } else {
                    source.AppendLine($$"""
                        if(restLength==0) { 
                            break;
                        }
                        """);
                }

            } else {
                // This dose not make nothing!
                // Look closly, it is only the opening part.
                source.AppendLine($$"""
                 if(restLength!=0) { 
                    
                
                 """);

            }



            source.AppendLine($$"""
            
             // Find next entry


                
                isQuoted = rest[0] == quoteSymbol;
                isQuotedInt = isQuoted ? 1 : 0;
                start = isQuotedInt;
                //? 1
                //: 0;

                if (isQuoted)
            {
                end = rest[1..].IndexOf(quoteSymbol) + 2;
            }
            else
            {

                end = rest.IndexOfAny(seperators);
        """);
            if (index == properties.Length - 1) {

                source.AppendLine($$"""
                // ON LASt Column

                if (end == -1 || rest[end] != seperatorSymbol)
                {
                    // OK record end
                 
                }
                else
                {
                    // another column, NOT OK
                    {{(handleError ? $"onError?.Invoke(Parser.LineError.ToManyColumns(lineIndex, {properties.Length}));" : string.Empty)}}
                    var next = rest.IndexOfAny(linebreaks);
                    if(next == -1){
                        break;
                    }
                    rest = rest[next..];
                    next = rest.IndexOfAnyExcept(linebreaks);
                    if(next == -1){
                        break;
                    }
                    rest = rest[next..];
                    continue;
                }

                """);
            } else {

                source.AppendLine($$"""

                // on Other Column
                if (end == -1)
                {
                    // not enogh colums in last line
                    {{(handleError ? $"onError?.Invoke(Parser.LineError.NotEnoghColumns(lineIndex,{index}, {properties.Length}));" : string.Empty)}}

                    break;
                }
                else if (rest[end] != seperatorSymbol)
                {
                    // not enogh colums in last line
                    // find start of next line
                    {{(handleError ? $"onError?.Invoke(Parser.LineError.NotEnoghColumns(lineIndex,{index}, {properties.Length}));" : string.Empty)}}
                    rest = rest[end..];
                    var next = rest.IndexOfAnyExcept(linebreaks);
                    if (next == -1)
                    {
                        break;
                    }
                    rest = rest[next..];
                    continue;
                }
                else
                {
                    // OK we will process the next column

                }

                """);
            }
            source.AppendLine($$"""

            }

  
                {
                    var singn = end >>>31;
                    end = end * (1 - singn) + singn * restLength;
                }

                dataEntry = rest[start..(end - isQuotedInt)];
                if (end + 1 < restLength)
                {
                    rest = rest[(end + 1)..];
                }
                else
                {
                    rest = System.ReadOnlySpan<{{rawDataType}}>.Empty;
                }

                

            
            """);

            if (index == properties.Length - 1) {

                source.AppendLine($$"""
                // ON LASt Column
                {
                    // OK record end
                    var next = rest.IndexOfAnyExcept(linebreaks);
                    if(next !=-1){

                    rest = rest[next..];
                    }
                    else{
                rest = System.ReadOnlySpan<{{rawDataType}}>.Empty;
                    }
                }
                
                """);
            }

            if (!errorOnNoMoreData) {
                source.AppendLine($$"""
                
                    }        
                
                """);

            }

            if (property is not null && propertyType is not null) {


                source.AppendLine($$"""
                {{propertyType}} {{property}};
                try {
                    {{property}} =  
            """);

                HandleProperty(source, property, propertyType, converterMethod, attributeTransformData, context);

                source.Append($$"""
                ;
                    } catch(System.Exception e){
                        {{(handleError ? $"onError?.Invoke(Parser.LineError.ParseError(lineIndex,{index}, e,{(rawDataType == "System.Char" ? "dataEntry.ToString()" : "null")}));" : string.Empty)}}
                        
                

                """);
                if (index != properties.Length - 1) { // On last column we may not search for linebreaks
                    source.Append($$"""
                        {
                            var next = rest.IndexOfAny(linebreaks);
                            if(next == -1){
                                break;
                            }
                            rest = rest[next..];
                        }
                

                """);
                }
                source.Append($$"""
                        {
                            var next = rest.IndexOfAnyExcept(linebreaks);
                            if(next == -1){
                                break;
                            }
                            rest = rest[next..];
                        }
                        continue;
                    }

                """);
            }


            source.AppendLine($$"""
                
                propertyIndex++;
                // END {{property ?? "IGNORED"}}
            """);


        }

        source.AppendLine($$"""

        var instance = new {{resultType}}(){
            {{string.Join(",\n            ", argument.Values.Where(x => !x.IsNull).Select((v, i) => $"{v.Value} = {v.Value}"))}}
        };
        result.Add(instance);
    }

    return result{{resultCollectionFinish}};
}
""");

        static void HandleProperty(StringBuilder source, string property, ITypeSymbol propertyType, string? converterMethod, List<AttributeData> attributeTransformData, GeneratorExecutionContext context) {
            if (converterMethod is not null) {
                source.AppendLine($$"""  {{converterMethod}}(dataEntry)""");
            } else if (GetFullMetadataName(propertyType) == "System.String") {
                source.AppendLine($$""" stringFactory(dataEntry)""");
            } else if (GetFullMetadataName(propertyType) == "System.Nullable" && propertyType is INamedTypeSymbol namedPropertyType) {
                var actualType = namedPropertyType.TypeArguments[0];

                var converter = attributeTransformData.SingleOrDefault(x => x.ConstructorArguments[0].Value is Type type && (context.Compilation.GetTypeByMetadataName(type.FullName)?.Equals(propertyType, SymbolEqualityComparer.Default) ?? false));

                var converterMethodActual = converter?.ConstructorArguments[1].Value as string;


                source.Append($$""" dataEntry.Length == 0 ? null as {{GetFullMetadataName(actualType)}}? : """);

                HandleProperty(source, property, actualType, converterMethodActual, attributeTransformData, context);



            } else if (propertyType.Interfaces.Any(x => GetFullMetadataName(x) == "System.ISpanParsable")) {
                source.AppendLine($$""" {{GetFullMetadataName(propertyType)}}.Parse(stringFactory(dataEntry), culture)""");
            } else {
                source.AppendLine($$"""
            #error "{{property}} can't be parsed unsupported property Type {{GetFullMetadataName(propertyType)}}"
            """);

                foreach (var x in propertyType.Interfaces) {

                    source.AppendLine($$"""
                        #error "Interface {{GetFullMetadataName(x)}} "
                        """);
                }
            }
        }
    }

    /// <summary>
    /// Created on demand before each generation pass
    /// </summary>
    class SyntaxReceiver : ISyntaxContextReceiver {
        public List<IMethodSymbol> Methods { get; } = new();

        /// <summary>
        /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
        /// </summary>
        public void OnVisitSyntaxNode(GeneratorSyntaxContext context) {
            // any field with at least one attribute is a candidate for property generation
            if (context.Node is MethodDeclarationSyntax methondDeclarationSyntax
                && methondDeclarationSyntax.AttributeLists.Count > 0) {
                var symbol = context.SemanticModel.GetDeclaredSymbol(methondDeclarationSyntax);
                if (symbol is IMethodSymbol methodSymbol && methodSymbol.GetAttributes().Any(ad => ad.AttributeClass?.ToDisplayString() == $"{AttributeNamespace}.{AttributeName}Attribute")) {
                    Methods.Add(methodSymbol);
                }
            }
        }
    }


    public static string GetFullMetadataName(ISymbol s) {
        if (s == null || IsRootNamespace(s)) {
            return string.Empty;
        }

        var sb = new StringBuilder(s.Name);
        var last = s;

        s = s.ContainingSymbol;

        while (!IsRootNamespace(s)) {
            if (s is ITypeSymbol && last is ITypeSymbol) {
                sb.Insert(0, '+');
            } else {
                sb.Insert(0, '.');
            }

            sb.Insert(0, s.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            //sb.Insert(0, s.MetadataName);
            s = s.ContainingSymbol;
        }

        return sb.ToString();
    }

    private static string GetVisibility(Accessibility accessibility) {
        return accessibility switch {
            Accessibility.Private => "private",
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            _ => ""
        };
    }



    private static bool IsRootNamespace(ISymbol symbol) {
        return symbol is INamespaceSymbol s && s.IsGlobalNamespace;
    }
}
