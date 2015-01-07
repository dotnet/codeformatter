# CodeFormatter

CodeFormatter is a tool that uses Roslyn to automatically rewrite the source to
follow our coding styles, which are [documented here][corefx-coding-style].

[corefx-coding-style]: https://github.com/dotnet/corefx/wiki/Contributing#c-coding-style

## Usage

In order get the usage, simply invoke the tool with no arguments:

```
$ .\CodeFormatter.exe
CodeFormatter <solution> [<rule types>] [/file <filename>]
    <rule types> - Rule types to use in addition to the default ones.
                   Use ConvertTests to convert MSTest tests to xUnit.
    <filename> - Only apply changes to files with specified name.
```

## Contributing

We follow the same contribution process that [corefx is using][corefx-contributing].

[corefx-contributing]: https://github.com/dotnet/corefx/wiki/Contributing