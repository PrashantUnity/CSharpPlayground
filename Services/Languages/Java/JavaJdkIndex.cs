using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

public static class JavaJdkIndex
{
    public static readonly IReadOnlyList<CSharpCompletionItem> Keywords =
    [
        new() { DisplayText = "public", InsertionText = "public ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Visible to all classes." },
        new() { DisplayText = "private", InsertionText = "private ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Visible only within the enclosing class." },
        new() { DisplayText = "protected", InsertionText = "protected ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Visible to package and subclasses." },
        new() { DisplayText = "static", InsertionText = "static ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Belongs to the class rather than instances." },
        new() { DisplayText = "final", InsertionText = "final ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Non-modifiable variable, un-overridable method, or un-extendable class." },
        new() { DisplayText = "abstract", InsertionText = "abstract ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares a class or method without implementation." },
        new() { DisplayText = "class", InsertionText = "class ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares a class definition." },
        new() { DisplayText = "record", InsertionText = "record ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares an immutable data carrier class (Java 16+)." },
        new() { DisplayText = "interface", InsertionText = "interface ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares an interface contract." },
        new() { DisplayText = "enum", InsertionText = "enum ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares an enumerated type." },
        new() { DisplayText = "extends", InsertionText = "extends ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Extends a superclass or interface." },
        new() { DisplayText = "implements", InsertionText = "implements ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Implements interfaces." },
        new() { DisplayText = "void", InsertionText = "void ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Indicates that a method does not return a value." },
        new() { DisplayText = "var", InsertionText = "var ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Local variable type inference (Java 10+)." },
        new() { DisplayText = "int", InsertionText = "int ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "32-bit signed two's complement integer." },
        new() { DisplayText = "long", InsertionText = "long ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "64-bit signed two's complement integer." },
        new() { DisplayText = "double", InsertionText = "double ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Double-precision 64-bit IEEE 754 floating point." },
        new() { DisplayText = "float", InsertionText = "float ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Single-precision 32-bit IEEE 754 floating point." },
        new() { DisplayText = "boolean", InsertionText = "boolean ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Represents true or false." },
        new() { DisplayText = "char", InsertionText = "char ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Single 16-bit Unicode character." },
        new() { DisplayText = "byte", InsertionText = "byte ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "8-bit signed two's complement integer." },
        new() { DisplayText = "short", InsertionText = "short ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "16-bit signed two's complement integer." },
        new() { DisplayText = "String", InsertionText = "String ", Kind = CompletionItemKind.Class, Priority = 850, Documentation = "Immutable sequence of characters in java.lang." },
        new() { DisplayText = "return", InsertionText = "return ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Returns control and optionally a value to the caller." },
        new() { DisplayText = "new", InsertionText = "new ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Creates an instance of an object or array." },
        new() { DisplayText = "if", InsertionText = "if (", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Conditional branch execution." },
        new() { DisplayText = "else", InsertionText = "else {\n    $0\n}", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Branch executed when condition is false." },
        new() { DisplayText = "for", InsertionText = "for (", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Executes a loop with initialization, condition, and update." },
        new() { DisplayText = "while", InsertionText = "while (", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Executes while boolean condition is true." },
        new() { DisplayText = "do", InsertionText = "do {\n    $0\n} while ();", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Executes loop body before evaluating condition." },
        new() { DisplayText = "switch", InsertionText = "switch (", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Matches an expression against multiple cases." },
        new() { DisplayText = "case", InsertionText = "case ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Defines a branch in a switch statement." },
        new() { DisplayText = "default", InsertionText = "default:\n    ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Default branch in a switch statement." },
        new() { DisplayText = "break", InsertionText = "break;", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Terminates the innermost switch or loop." },
        new() { DisplayText = "continue", InsertionText = "continue;", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Skips the rest of the current loop iteration." },
        new() { DisplayText = "try", InsertionText = "try {\n    $0\n} catch (Exception ex) {\n    \n}", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Executes a block with exception handling." },
        new() { DisplayText = "catch", InsertionText = "catch (", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Handles an exception thrown in a try block." },
        new() { DisplayText = "finally", InsertionText = "finally {\n    $0\n}", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Block always executed after try-catch." },
        new() { DisplayText = "throw", InsertionText = "throw ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Throws an exception explicitly." },
        new() { DisplayText = "throws", InsertionText = "throws ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares checked exceptions thrown by a method." },
        new() { DisplayText = "import", InsertionText = "import ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Imports a package or type into the compilation unit." },
        new() { DisplayText = "package", InsertionText = "package ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares the package for this compilation unit." },
        new() { DisplayText = "instanceof", InsertionText = "instanceof ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Tests whether an object is an instance of a type." },
        new() { DisplayText = "super", InsertionText = "super", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Refers to the immediate superclass." },
        new() { DisplayText = "this", InsertionText = "this", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Refers to the current instance." },
        new() { DisplayText = "null", InsertionText = "null", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Null literal reference." },
        new() { DisplayText = "true", InsertionText = "true", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Boolean true." },
        new() { DisplayText = "false", InsertionText = "false", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Boolean false." }
    ];

    public static readonly IReadOnlyList<CSharpCompletionItem> Snippets =
    [
        new()
        {
            DisplayText = "sout",
            InsertionText = "System.out.println($0);",
            Kind = CompletionItemKind.Snippet,
            Priority = 950,
            Signature = "System.out.println(...)",
            Documentation = "Prints an expression or string to standard output with a newline."
        },
        new()
        {
            DisplayText = "serr",
            InsertionText = "System.err.println($0);",
            Kind = CompletionItemKind.Snippet,
            Priority = 950,
            Signature = "System.err.println(...)",
            Documentation = "Prints an expression or error message to standard error with a newline."
        },
        new()
        {
            DisplayText = "psvm",
            InsertionText = "public static void main(String[] args) {\n    $0\n}",
            Kind = CompletionItemKind.Snippet,
            Priority = 950,
            Signature = "public static void main(String[] args)",
            Documentation = "Main entry point method declaration."
        },
        new()
        {
            DisplayText = "main",
            InsertionText = "public static void main(String[] args) {\n    $0\n}",
            Kind = CompletionItemKind.Snippet,
            Priority = 950,
            Signature = "public static void main(String[] args)",
            Documentation = "Main entry point method declaration."
        },
        new()
        {
            DisplayText = "fori",
            InsertionText = "for (int i = 0; i < length; i++) {\n    $0\n}",
            Kind = CompletionItemKind.Snippet,
            Priority = 950,
            Signature = "for (int i = 0; i < length; i++)",
            Documentation = "Standard index-based for loop."
        },
        new()
        {
            DisplayText = "fore",
            InsertionText = "for (var item : collection) {\n    $0\n}",
            Kind = CompletionItemKind.Snippet,
            Priority = 950,
            Signature = "for (var item : collection)",
            Documentation = "Enhanced for-each loop over an Iterable or array."
        },
        new()
        {
            DisplayText = "tryc",
            InsertionText = "try {\n    $0\n} catch (Exception ex) {\n    ex.printStackTrace();\n}",
            Kind = CompletionItemKind.Snippet,
            Priority = 940,
            Signature = "try-catch block",
            Documentation = "Exception handling try-catch block."
        }
    ];

    public static readonly IReadOnlyList<CSharpCompletionItem> StandardLibraryTypes =
    [
        new() { DisplayText = "System", InsertionText = "System", Kind = CompletionItemKind.Class, Priority = 820, ReturnType = "java.lang", Documentation = "Provides standard input/output streams and system properties." },
        new() { DisplayText = "Math", InsertionText = "Math", Kind = CompletionItemKind.Class, Priority = 820, ReturnType = "java.lang", Documentation = "Mathematical functions: min, max, sqrt, pow, abs, sin, cos." },
        new() { DisplayText = "Integer", InsertionText = "Integer", Kind = CompletionItemKind.Class, Priority = 810, ReturnType = "java.lang", Documentation = "Wraps a value of the primitive type int." },
        new() { DisplayText = "Double", InsertionText = "Double", Kind = CompletionItemKind.Class, Priority = 810, ReturnType = "java.lang", Documentation = "Wraps a value of the primitive type double." },
        new() { DisplayText = "Long", InsertionText = "Long", Kind = CompletionItemKind.Class, Priority = 810, ReturnType = "java.lang", Documentation = "Wraps a value of the primitive type long." },
        new() { DisplayText = "Boolean", InsertionText = "Boolean", Kind = CompletionItemKind.Class, Priority = 810, ReturnType = "java.lang", Documentation = "Wraps a value of the primitive type boolean." },
        new() { DisplayText = "StringBuilder", InsertionText = "StringBuilder", Kind = CompletionItemKind.Class, Priority = 810, ReturnType = "java.lang", Documentation = "A mutable sequence of characters." },
        new() { DisplayText = "Thread", InsertionText = "Thread", Kind = CompletionItemKind.Class, Priority = 800, ReturnType = "java.lang", Documentation = "A thread of execution in a program." },
        new() { DisplayText = "Object", InsertionText = "Object", Kind = CompletionItemKind.Class, Priority = 800, ReturnType = "java.lang", Documentation = "Class Object is the root of the class hierarchy." },
        new() { DisplayText = "Exception", InsertionText = "Exception", Kind = CompletionItemKind.Class, Priority = 800, ReturnType = "java.lang", Documentation = "The class Exception and its subclasses indicate conditions that a reasonable application might want to catch." },
        new() { DisplayText = "List", InsertionText = "List", Kind = CompletionItemKind.Interface, Priority = 830, ReturnType = "java.util", Documentation = "An ordered collection (also known as a sequence)." },
        new() { DisplayText = "ArrayList", InsertionText = "ArrayList", Kind = CompletionItemKind.Class, Priority = 830, ReturnType = "java.util", Documentation = "Resizable-array implementation of the List interface." },
        new() { DisplayText = "LinkedList", InsertionText = "LinkedList", Kind = CompletionItemKind.Class, Priority = 810, ReturnType = "java.util", Documentation = "Doubly-linked list implementation of the List and Deque interfaces." },
        new() { DisplayText = "Map", InsertionText = "Map", Kind = CompletionItemKind.Interface, Priority = 830, ReturnType = "java.util", Documentation = "An object that maps keys to values." },
        new() { DisplayText = "HashMap", InsertionText = "HashMap", Kind = CompletionItemKind.Class, Priority = 830, ReturnType = "java.util", Documentation = "Hash table based implementation of the Map interface." },
        new() { DisplayText = "Set", InsertionText = "Set", Kind = CompletionItemKind.Interface, Priority = 820, ReturnType = "java.util", Documentation = "A collection that contains no duplicate elements." },
        new() { DisplayText = "HashSet", InsertionText = "HashSet", Kind = CompletionItemKind.Class, Priority = 820, ReturnType = "java.util", Documentation = "Hash table implementation of the Set interface." },
        new() { DisplayText = "Queue", InsertionText = "Queue", Kind = CompletionItemKind.Interface, Priority = 810, ReturnType = "java.util", Documentation = "A collection designed for holding elements prior to processing." },
        new() { DisplayText = "PriorityQueue", InsertionText = "PriorityQueue", Kind = CompletionItemKind.Class, Priority = 810, ReturnType = "java.util", Documentation = "An unbounded priority queue based on a priority heap." },
        new() { DisplayText = "Arrays", InsertionText = "Arrays", Kind = CompletionItemKind.Class, Priority = 820, ReturnType = "java.util", Documentation = "Static utility methods for manipulating arrays (sort, search, fill)." },
        new() { DisplayText = "Collections", InsertionText = "Collections", Kind = CompletionItemKind.Class, Priority = 820, ReturnType = "java.util", Documentation = "Static utility methods that operate on or return collections." },
        new() { DisplayText = "Optional", InsertionText = "Optional", Kind = CompletionItemKind.Class, Priority = 810, ReturnType = "java.util", Documentation = "A container object which may or may not contain a non-null value." },
        new() { DisplayText = "Scanner", InsertionText = "Scanner", Kind = CompletionItemKind.Class, Priority = 810, ReturnType = "java.util", Documentation = "A simple text scanner which can parse primitive types and strings." },
        new() { DisplayText = "UUID", InsertionText = "UUID", Kind = CompletionItemKind.Class, Priority = 800, ReturnType = "java.util", Documentation = "An immutable universally unique identifier." },
        new() { DisplayText = "File", InsertionText = "File", Kind = CompletionItemKind.Class, Priority = 810, ReturnType = "java.io", Documentation = "An abstract representation of file and directory pathnames." },
        new() { DisplayText = "Path", InsertionText = "Path", Kind = CompletionItemKind.Interface, Priority = 810, ReturnType = "java.nio.file", Documentation = "An object that may be used to locate a file in a file system." },
        new() { DisplayText = "Paths", InsertionText = "Paths", Kind = CompletionItemKind.Class, Priority = 810, ReturnType = "java.nio.file", Documentation = "Static factory methods to create a Path from a String URI or path." },
        new() { DisplayText = "Files", InsertionText = "Files", Kind = CompletionItemKind.Class, Priority = 810, ReturnType = "java.nio.file", Documentation = "Static utility methods that operate exclusively on files and directories." },
        new() { DisplayText = "LocalDate", InsertionText = "LocalDate", Kind = CompletionItemKind.Class, Priority = 800, ReturnType = "java.time", Documentation = "A date-time without a time-zone in the ISO-8601 calendar system." },
        new() { DisplayText = "Instant", InsertionText = "Instant", Kind = CompletionItemKind.Class, Priority = 800, ReturnType = "java.time", Documentation = "An instantaneous point on the time-line." },
        new() { DisplayText = "Duration", InsertionText = "Duration", Kind = CompletionItemKind.Class, Priority = 800, ReturnType = "java.time", Documentation = "A time-based amount of time, such as '34.5 seconds'." },
        new() { DisplayText = "BigInteger", InsertionText = "BigInteger", Kind = CompletionItemKind.Class, Priority = 800, ReturnType = "java.math", Documentation = "Immutable arbitrary-precision integers." },
        new() { DisplayText = "BigDecimal", InsertionText = "BigDecimal", Kind = CompletionItemKind.Class, Priority = 800, ReturnType = "java.math", Documentation = "Immutable, arbitrary-precision signed decimal numbers." }
    ];

    public static readonly IReadOnlyList<CSharpCompletionItem> SystemOutMembers =
    [
        new() { DisplayText = "println", InsertionText = "println($0);", Kind = CompletionItemKind.Method, Priority = 900, Signature = "void println(Object x)", Documentation = "Prints an Object and terminates the line." },
        new() { DisplayText = "print", InsertionText = "print($0);", Kind = CompletionItemKind.Method, Priority = 890, Signature = "void print(Object x)", Documentation = "Prints an Object without terminating the line." },
        new() { DisplayText = "printf", InsertionText = "printf(\"$0\", );", Kind = CompletionItemKind.Method, Priority = 880, Signature = "PrintStream printf(String format, Object... args)", Documentation = "A convenience method to write a formatted string to this output stream." },
        new() { DisplayText = "flush", InsertionText = "flush();", Kind = CompletionItemKind.Method, Priority = 850, Signature = "void flush()", Documentation = "Flushes the stream." }
    ];

    public static readonly IReadOnlyList<CSharpCompletionItem> MathMembers =
    [
        new() { DisplayText = "max", InsertionText = "max(", Kind = CompletionItemKind.Method, Priority = 900, Signature = "static int max(int a, int b)", Documentation = "Returns the greater of two values." },
        new() { DisplayText = "min", InsertionText = "min(", Kind = CompletionItemKind.Method, Priority = 900, Signature = "static int min(int a, int b)", Documentation = "Returns the smaller of two values." },
        new() { DisplayText = "abs", InsertionText = "abs(", Kind = CompletionItemKind.Method, Priority = 900, Signature = "static int abs(int a)", Documentation = "Returns the absolute value of an argument." },
        new() { DisplayText = "sqrt", InsertionText = "sqrt(", Kind = CompletionItemKind.Method, Priority = 890, Signature = "static double sqrt(double a)", Documentation = "Returns the correctly rounded positive square root of a double value." },
        new() { DisplayText = "pow", InsertionText = "pow(", Kind = CompletionItemKind.Method, Priority = 890, Signature = "static double pow(double a, double b)", Documentation = "Returns the value of the first argument raised to the power of the second argument." },
        new() { DisplayText = "random", InsertionText = "random()", Kind = CompletionItemKind.Method, Priority = 880, Signature = "static double random()", Documentation = "Returns a double value with a positive sign, greater than or equal to 0.0 and less than 1.0." },
        new() { DisplayText = "floor", InsertionText = "floor(", Kind = CompletionItemKind.Method, Priority = 870, Signature = "static double floor(double a)", Documentation = "Returns the largest double value that is less than or equal to the argument." },
        new() { DisplayText = "ceil", InsertionText = "ceil(", Kind = CompletionItemKind.Method, Priority = 870, Signature = "static double ceil(double a)", Documentation = "Returns the smallest double value that is greater than or equal to the argument." },
        new() { DisplayText = "PI", InsertionText = "PI", Kind = CompletionItemKind.Field, Priority = 860, Signature = "static final double PI", Documentation = "The double value that is closer than any other to pi, the ratio of the circumference of a circle to its diameter." },
        new() { DisplayText = "E", InsertionText = "E", Kind = CompletionItemKind.Field, Priority = 860, Signature = "static final double E", Documentation = "The double value that is closer than any other to e, the base of the natural logarithms." }
    ];

    public static readonly IReadOnlyList<CSharpCompletionItem> ArraysMembers =
    [
        new() { DisplayText = "sort", InsertionText = "sort(", Kind = CompletionItemKind.Method, Priority = 900, Signature = "static void sort(int[] a)", Documentation = "Sorts the specified array into ascending numerical order." },
        new() { DisplayText = "binarySearch", InsertionText = "binarySearch(", Kind = CompletionItemKind.Method, Priority = 890, Signature = "static int binarySearch(int[] a, int key)", Documentation = "Searches the specified array for the specified value using binary search." },
        new() { DisplayText = "asList", InsertionText = "asList(", Kind = CompletionItemKind.Method, Priority = 890, Signature = "static <T> List<T> asList(T... a)", Documentation = "Returns a fixed-size list backed by the specified array." },
        new() { DisplayText = "fill", InsertionText = "fill(", Kind = CompletionItemKind.Method, Priority = 880, Signature = "static void fill(int[] a, int val)", Documentation = "Assigns the specified value to each element of the specified array." },
        new() { DisplayText = "equals", InsertionText = "equals(", Kind = CompletionItemKind.Method, Priority = 870, Signature = "static boolean equals(int[] a, int[] a2)", Documentation = "Returns true if the two specified arrays are equal to one another." },
        new() { DisplayText = "toString", InsertionText = "toString(", Kind = CompletionItemKind.Method, Priority = 870, Signature = "static String toString(int[] a)", Documentation = "Returns a string representation of the contents of the specified array." },
        new() { DisplayText = "copyOf", InsertionText = "copyOf(", Kind = CompletionItemKind.Method, Priority = 860, Signature = "static <T> T[] copyOf(T[] original, int newLength)", Documentation = "Copies the specified array, truncating or padding with zeros." }
    ];

    public static readonly IReadOnlyList<CSharpCompletionItem> CollectionsMembers =
    [
        new() { DisplayText = "sort", InsertionText = "sort(", Kind = CompletionItemKind.Method, Priority = 900, Signature = "static <T extends Comparable> void sort(List<T> list)", Documentation = "Sorts the specified list into ascending order." },
        new() { DisplayText = "reverse", InsertionText = "reverse(", Kind = CompletionItemKind.Method, Priority = 890, Signature = "static void reverse(List<?> list)", Documentation = "Reverses the order of the elements in the specified list." },
        new() { DisplayText = "max", InsertionText = "max(", Kind = CompletionItemKind.Method, Priority = 880, Signature = "static <T> T max(Collection<? extends T> coll)", Documentation = "Returns the maximum element of the given collection." },
        new() { DisplayText = "min", InsertionText = "min(", Kind = CompletionItemKind.Method, Priority = 880, Signature = "static <T> T min(Collection<? extends T> coll)", Documentation = "Returns the minimum element of the given collection." },
        new() { DisplayText = "emptyList", InsertionText = "emptyList()", Kind = CompletionItemKind.Method, Priority = 870, Signature = "static <T> List<T> emptyList()", Documentation = "Returns an empty immutable list." },
        new() { DisplayText = "unmodifiableList", InsertionText = "unmodifiableList(", Kind = CompletionItemKind.Method, Priority = 860, Signature = "static <T> List<T> unmodifiableList(List<? extends T> list)", Documentation = "Returns an unmodifiable view of the specified list." }
    ];

    public static readonly IReadOnlyList<CSharpCompletionItem> CommonInstanceMembers =
    [
        new() { DisplayText = "size", InsertionText = "size()", Kind = CompletionItemKind.Method, Priority = 900, Signature = "int size()", Documentation = "Returns the number of elements in this collection or map." },
        new() { DisplayText = "length", InsertionText = "length()", Kind = CompletionItemKind.Method, Priority = 900, Signature = "int length()", Documentation = "Returns the length of this character sequence or string." },
        new() { DisplayText = "isEmpty", InsertionText = "isEmpty()", Kind = CompletionItemKind.Method, Priority = 890, Signature = "boolean isEmpty()", Documentation = "Returns true if this collection or string contains no elements." },
        new() { DisplayText = "add", InsertionText = "add($0);", Kind = CompletionItemKind.Method, Priority = 880, Signature = "boolean add(E e)", Documentation = "Appends the specified element to this collection." },
        new() { DisplayText = "get", InsertionText = "get($0)", Kind = CompletionItemKind.Method, Priority = 880, Signature = "E get(int index)", Documentation = "Returns the element at the specified position or mapped key." },
        new() { DisplayText = "put", InsertionText = "put($0, );", Kind = CompletionItemKind.Method, Priority = 880, Signature = "V put(K key, V value)", Documentation = "Associates the specified value with the specified key in this map." },
        new() { DisplayText = "remove", InsertionText = "remove($0)", Kind = CompletionItemKind.Method, Priority = 870, Signature = "E remove(int index)", Documentation = "Removes the element at the specified position or matching object." },
        new() { DisplayText = "contains", InsertionText = "contains($0)", Kind = CompletionItemKind.Method, Priority = 870, Signature = "boolean contains(Object o)", Documentation = "Returns true if this collection or string contains the specified element." },
        new() { DisplayText = "clear", InsertionText = "clear();", Kind = CompletionItemKind.Method, Priority = 860, Signature = "void clear()", Documentation = "Removes all elements from this collection or map." },
        new() { DisplayText = "equals", InsertionText = "equals($0)", Kind = CompletionItemKind.Method, Priority = 850, Signature = "boolean equals(Object obj)", Documentation = "Indicates whether some other object is 'equal to' this one." },
        new() { DisplayText = "hashCode", InsertionText = "hashCode()", Kind = CompletionItemKind.Method, Priority = 840, Signature = "int hashCode()", Documentation = "Returns a hash code value for the object." },
        new() { DisplayText = "toString", InsertionText = "toString()", Kind = CompletionItemKind.Method, Priority = 840, Signature = "String toString()", Documentation = "Returns a string representation of the object." },
        new() { DisplayText = "substring", InsertionText = "substring($0)", Kind = CompletionItemKind.Method, Priority = 830, Signature = "String substring(int beginIndex)", Documentation = "Returns a string that is a substring of this string." },
        new() { DisplayText = "charAt", InsertionText = "charAt($0)", Kind = CompletionItemKind.Method, Priority = 830, Signature = "char charAt(int index)", Documentation = "Returns the char value at the specified index." },
        new() { DisplayText = "split", InsertionText = "split(\"$0\")", Kind = CompletionItemKind.Method, Priority = 820, Signature = "String[] split(String regex)", Documentation = "Splits this string around matches of the given regular expression." },
        new() { DisplayText = "stream", InsertionText = "stream()", Kind = CompletionItemKind.Method, Priority = 820, Signature = "Stream<E> stream()", Documentation = "Returns a sequential Stream with this collection as its source." }
    ];
}
