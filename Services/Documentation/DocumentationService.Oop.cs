using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public partial class DocumentationService
{
    private DocCategory BuildOopCategory()
    {
        return new DocCategory
        {
            Id = "oop",
            Title = "Object-Oriented Programming",
            IconKind = MaterialIconKind.Sitemap,
            AccentColor = "#A78BFA",
            Badge = "OOP",
            Description = "Classes, encapsulation, inheritance, and polymorphism in C#.",
            Articles = new List<DocArticle>
            {
                CreateOopArticle()
            }
        };
    }

    private DocArticle CreateOopArticle()
    {
        return new DocArticle
        {
            Id = "learn_oop",
            Title = "Object-Oriented Programming in C#",
            Subtitle = "Classes, encapsulation, inheritance, polymorphism, and abstraction — the pillars C# is built around.",
            ReadingTime = "9 min read",
            Summary = "C# is an object-oriented language at its core. This chapter walks through the four pillars of OOP with a single running Shape example, then covers the practical decisions you'll make constantly: interface vs abstract class, and overloading vs overriding.",
            Keywords = new List<string> { "oop", "class", "object", "encapsulation", "inheritance", "polymorphism", "abstraction", "interface", "abstract class", "overloading", "overriding", "virtual", "override", "sealed", "base" },
            Sections = new List<DocSection>
            {
                new()
                {
                    Heading = "Classes & Objects",
                    Content = "A class is a blueprint that defines the fields (state), properties, and methods (behavior) a family of objects will share. An object is a concrete instance of that blueprint, created with new, with its own independent copy of the instance fields. Everything in C# outside of the built-in value types is typically modeled as a class — it's the fundamental unit of organization for object-oriented code.",
                    BulletPoints = new List<string>
                    {
                        "class Dog { public string Name; public void Bark() { ... } } defines the blueprint.",
                        "var rex = new Dog { Name = \"Rex\" }; creates one object from it.",
                        "Each object has its own copy of instance fields; static members are shared by the whole type instead."
                    }
                },
                new()
                {
                    Heading = "Encapsulation",
                    Content = "Encapsulation means bundling an object's data with the methods that operate on it, and hiding the internal representation behind a controlled public surface. In practice this means private fields with public properties (or methods) that validate input, so the object can never be left in an invalid state from outside code.",
                    CalloutType = DocCalloutType.Tip,
                    CalloutText = "Prefer a property with validation (public int Age { get => _age; set => _age = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value)); }) over a public field — a public field can be set to anything, bypassing any invariant the class relies on."
                },
                new()
                {
                    Heading = "Inheritance",
                    Content = "Inheritance lets a derived class (a subclass) reuse and extend the members of a base class, expressing an 'is-a' relationship. class Circle : Shape means every Circle is a Shape and automatically gets its non-private members, while adding or overriding behavior of its own. C# supports single inheritance for classes (one base class only) but a class can implement any number of interfaces.",
                    BulletPoints = new List<string>
                    {
                        "A derived class inherits public and protected members from its base class.",
                        "protected members are visible to the class itself and its derived classes, but not to outside code.",
                        "Use base(...) in a derived constructor to run the base class's constructor first."
                    }
                },
                new()
                {
                    Heading = "Polymorphism",
                    Content = "Polymorphism ('many forms') means code written against a base type can work with any derived type, and the correct overridden behavior runs automatically at runtime. A List<Shape> can hold Circle and Square objects interchangeably; calling shape.Area() on each one runs Circle's Area() or Square's Area() depending on the object's actual runtime type, not its declared compile-time type — this is sometimes called dynamic dispatch.",
                    CalloutType = DocCalloutType.Info,
                    CalloutText = "Polymorphism is what makes the virtual/override mechanism (covered later in this article) useful: without it, every caller would need to know and check the concrete type of every object it works with."
                },
                new()
                {
                    Heading = "Abstraction",
                    Content = "Abstraction means exposing only what a consumer needs to know to use a type, while hiding the how. An abstract class or interface describes a contract — 'anything that is a Shape can report an Area' — without necessarily saying how each shape computes it. Consumers of Shape never need to know whether they're holding a Circle or a Square to call Area() correctly.",
                    BulletPoints = new List<string>
                    {
                        "Abstraction is about the design of the contract; encapsulation is about protecting an object's internal state.",
                        "abstract class Shape { public abstract double Area(); } declares a contract with no implementation for Area.",
                        "Good abstractions let you change an implementation later without breaking any code that depends only on the contract."
                    }
                },
                new()
                {
                    Heading = "Interfaces vs Abstract Classes",
                    Content = "Both interfaces and abstract classes let you define a contract that other types must fulfill, but they serve different purposes. An abstract class can hold shared state (fields), provide a base constructor, and mix fully-implemented members with abstract ones — but a class can inherit from only one. An interface defines a pure contract (historically no state, though C# 8+ allows default method implementations) and a class can implement any number of them.",
                    BulletPoints = new List<string>
                    {
                        "Use an abstract class when derived types share common state or implementation, and form a true 'is-a' hierarchy.",
                        "Use an interface when unrelated types need to promise the same capability (IDisposable, IComparable<T>) regardless of their inheritance hierarchy.",
                        "A class can implement many interfaces (class Circle : Shape, IComparable<Circle>) but extend only one class."
                    }
                },
                new()
                {
                    Heading = "Method Overloading vs Overriding",
                    Content = "Overloading means defining multiple methods with the same name but different parameter lists in the same type — resolved at compile time based on the arguments you pass. Overriding means a derived class supplying its own implementation of a method the base class marked virtual (or abstract) — resolved at runtime based on the object's actual type. They solve different problems: overloading offers convenience (Add(int) vs Add(int, int)); overriding enables polymorphism.",
                    CalloutType = DocCalloutType.Warning,
                    CalloutText = "A method in a derived class with the same name and signature as a base method that is NOT marked virtual will hide it (shadow it) rather than override it — a common source of subtle bugs. The compiler warns with 'CS0108: member hides inherited member' and suggests new or override; always choose override unless hiding is truly intended."
                },
                new()
                {
                    Heading = "virtual, override, sealed & base",
                    Content = "Mark a base class method virtual to allow — but not require — derived classes to replace its behavior with override. sealed on an override prevents any further derived class from overriding it again, locking that behavior in place for the rest of the hierarchy. base.MemberName() calls the base class's version of a member from within an override, which is essential when you want to extend the base behavior rather than fully replace it.",
                    BulletPoints = new List<string>
                    {
                        "virtual — a base class opts a member into being overridable.",
                        "override — a derived class supplies a new implementation for a virtual (or abstract) member.",
                        "sealed override — allowed to override once more, but no further subclass may override it again.",
                        "base.Method() / base.Property — calls the base class's implementation, often to augment rather than replace it."
                    }
                }
            },
            ApiSignatures = new List<DocApiSignature>
            {
                new() { MethodName = "object.Equals", ReturnType = "bool", Parameters = "object? obj", Description = "Override to define value-based equality for a type; the default implementation compares references for classes." },
                new() { MethodName = "object.GetHashCode", ReturnType = "int", Parameters = "", Description = "Override alongside Equals so equal objects always produce the same hash code — required for correct behavior in dictionaries and hash sets." }
            },
            CodeSnippets = new List<DocCodeSnippet>
            {
                new()
                {
                    Id = "snip_learn_oop_shape_hierarchy",
                    Title = "Encapsulation, Inheritance & Polymorphism: a Shape Hierarchy",
                    Description = "A small Shape/Circle/Square hierarchy that demonstrates encapsulated state, an inherited base class, and polymorphic dispatch through a common Area() method.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;

                    abstract class Shape
                    {
                        // Encapsulation: the label is only settable through a validating property.
                        private string _label = "Shape";
                        public string Label
                        {
                            get => _label;
                            set => _label = string.IsNullOrWhiteSpace(value) ? "Shape" : value;
                        }

                        public abstract double Area();

                        public override string ToString() => $"{Label}: area = {Area():F2}";
                    }

                    class Circle : Shape
                    {
                        public double Radius { get; }

                        public Circle(double radius)
                        {
                            Radius = radius;
                            Label = "Circle";
                        }

                        public override double Area() => Math.PI * Radius * Radius;
                    }

                    class Square : Shape
                    {
                        public double Side { get; }

                        public Square(double side)
                        {
                            Side = side;
                            Label = "Square";
                        }

                        public override double Area() => Side * Side;
                    }

                    // Polymorphism: each element runs its own overridden Area(), chosen at runtime.
                    List<Shape> shapes = new()
                    {
                        new Circle(3),
                        new Square(4),
                        new Circle(1.5)
                    };

                    foreach (var shape in shapes)
                    {
                        Console.WriteLine(shape);
                    }

                    double totalArea = 0;
                    foreach (var shape in shapes) totalArea += shape.Area();
                    Console.WriteLine($"Total area: {totalArea:F2}");
                    """
                },
                new()
                {
                    Id = "snip_learn_oop_interface_vs_abstract",
                    Title = "Interface vs Abstract Class",
                    Description = "The same capability expressed two ways: an interface contract implemented by unrelated types, and an abstract class sharing common state and implementation.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;
                    using System.Collections.Generic;

                    // Interface: a pure contract that unrelated types can all implement.
                    interface IAudible
                    {
                        string MakeSound();
                    }

                    // Abstract class: shared state (Name) and a shared implementation (Describe),
                    // plus one abstract member every derived type must supply.
                    abstract class Animal : IAudible
                    {
                        public string Name { get; }

                        protected Animal(string name) => Name = name;

                        public abstract string MakeSound();

                        public string Describe() => $"{Name} says '{MakeSound()}'";
                    }

                    class Dog : Animal
                    {
                        public Dog(string name) : base(name) { }
                        public override string MakeSound() => "Woof";
                    }

                    class Cat : Animal
                    {
                        public Cat(string name) : base(name) { }
                        public override string MakeSound() => "Meow";
                    }

                    // A type with no relation to Animal can still implement IAudible.
                    class SmokeAlarm : IAudible
                    {
                        public string MakeSound() => "Beep!";
                    }

                    List<IAudible> audibleThings = new()
                    {
                        new Dog("Rex"),
                        new Cat("Whiskers"),
                        new SmokeAlarm()
                    };

                    foreach (var thing in audibleThings)
                    {
                        Console.WriteLine(thing.MakeSound());
                    }

                    List<Animal> animals = new() { new Dog("Rex"), new Cat("Whiskers") };
                    foreach (var animal in animals)
                    {
                        Console.WriteLine(animal.Describe());
                    }
                    """
                },
                new()
                {
                    Id = "snip_learn_oop_overload_override",
                    Title = "Overloading vs Overriding: virtual, override, sealed, base",
                    Description = "Overloaded Combine methods resolved at compile time, contrasted with a virtual/override/sealed/base chain resolved at runtime.",
                    TargetKind = WorkspaceItemKind.Script,
                    Code = """
                    using System;

                    class Calculator
                    {
                        // Overloading: same name, different parameter lists, chosen at compile time.
                        public int Combine(int a, int b) => a + b;
                        public string Combine(string a, string b) => a + b;
                        public double Combine(double a, double b, double c) => a + b + c;
                    }

                    class Greeter
                    {
                        public virtual string Greet() => "Hello!";
                    }

                    class FormalGreeter : Greeter
                    {
                        // Overriding: replaces the base implementation; callable via polymorphism.
                        public override string Greet() => "Good day to you.";
                    }

                    class FormalGreeterWithTitle : FormalGreeter
                    {
                        // Extends the base behavior using base.Greet() instead of fully replacing it,
                        // and seals it so no further subclass can override Greet() again.
                        public sealed override string Greet() => base.Greet() + " (Dr.)";
                    }

                    var calculator = new Calculator();
                    Console.WriteLine(calculator.Combine(2, 3));          // int overload
                    Console.WriteLine(calculator.Combine("foo", "bar"));  // string overload
                    Console.WriteLine(calculator.Combine(1.5, 2.5, 3.0)); // double overload

                    Greeter[] greeters = { new Greeter(), new FormalGreeter(), new FormalGreeterWithTitle() };
                    foreach (var greeter in greeters)
                    {
                        Console.WriteLine(greeter.Greet());
                    }
                    """
                }
            }
        };
    }
}
