# Mix languages and share variables

A cell can start with `#!python`, `#!javascript`, `#!rust`, `#!go`, `#!cpp` or `#!csharp` to run in that language.

Copy a variable from another language's kernel with `#!share`:

```python
#!share --from csharp people
print(len(people))
```

Type `#!` in a cell to see the languages, and `#!share --from ` to see the variables a kernel holds.
