# Bundlr
Bundlr is a lightweight C# library for bundling css and javascript. It can also minify your javascript files. It works by replacing script tags in your HTML file.

### How to use
- Drop [Bundlr.cs](https://raw.githubusercontent.com/aus10/Bundlr/master/Bundlr.cs) in your project
- Add the using statement to the top of your file
```c#
using Bundlr;
```
- Example usage:
```c#
 ScriptBundler.GetBundler("www/profile/index.html")
                    .AddFile("www/assets/js/authentication.js")
                    .AddJsDirectory("www/profile/scripts")
                    .BundleJs("www/profile/scripts/bundled.js");
```

### Thanks
- This project uses a port of Douglas Crockford's [JSMin](https://github.com/Taritsyn/JSMin.NET)
