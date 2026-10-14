# Run a cell

Open a `.frynb` file, or run **FrySharp: New Polyglot Notebook**. FrySharp is picked as the kernel for you.

Type some C# in a cell and press `Shift+Enter`:

```csharp
var people = new[] { new { Name = "Ada", Age = 36 }, new { Name = "Linus", Age = 55 } };
people
```

The result opens in the **Object Explorer**: expand and collapse it. Text the cell prints is one output, colours included.

While you type, errors are underlined as the compiler finds them, with the cells above in scope.
